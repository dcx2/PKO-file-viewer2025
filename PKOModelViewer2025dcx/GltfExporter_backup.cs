using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Numerics;
using Mindpower;

namespace PKOModelViewer
{
    public class GltfExporter
    {
        public static void ExportToGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath = null)
        {
            try
            {
                // For now, create a manual GLTF with proper structure
                CreateManualGltf(geom, outputPath, modelName, modelPath);
                
                // Create detailed info file
                string infoFile = Path.ChangeExtension(outputPath, ".txt");
                File.WriteAllText(infoFile, CreateExportInfo(geom, modelName, true));
                
            }
            catch (Exception ex)
            {
                // Log detailed error and fallback to manual GLTF
                string errorFile = Path.ChangeExtension(outputPath, "_error.txt");
                File.WriteAllText(errorFile, 
                    $"GLTF Export Error Details:\n" +
                    $"=========================\n" +
                    $"Error: {ex.Message}\n" +
                    $"Type: {ex.GetType().Name}\n" +
                    $"Stack trace:\n{ex.StackTrace}\n\n" +
                    $"Model Data:\n" +
                    $"Vertices: {geom?.mesh.header.vertex_num ?? 0}\n" +
                    $"Indices: {geom?.mesh.header.index_num ?? 0}\n" +
                    $"Subsets: {geom?.mesh.header.subset_num ?? 0}\n" +
                    $"FVF: 0x{geom?.mesh.header.fvf:X}\n");
                
                // Fallback to manual GLTF structure
                CreateManualGltf(geom, outputPath, modelName, modelPath);
            }
        }
        
        private static string CreateExportInfo(lwGeomObjInfo geom, string modelName, bool success)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKO Model Viewer - GLTF Export Report");
            sb.AppendLine("=====================================");
            sb.AppendLine($"Model Name: {modelName}");
            sb.AppendLine($"Export Status: {(success ? "SUCCESS" : "FALLBACK")}");
            sb.AppendLine($"Export Time: {DateTime.Now}");
            sb.AppendLine();
            
            if (geom != null && geom.mesh.vertex_seq != null)
            {
                sb.AppendLine("Model Statistics:");
                sb.AppendLine($"  Vertices: {geom.mesh.header.vertex_num}");
                sb.AppendLine($"  Indices: {geom.mesh.header.index_num}");
                sb.AppendLine($"  Triangles: {geom.mesh.header.index_num / 3}");
                sb.AppendLine($"  Materials/Subsets: {geom.mesh.header.subset_num}");
                sb.AppendLine($"  Vertex Format (FVF): 0x{geom.mesh.header.fvf:X}");
                sb.AppendLine();
                
                sb.AppendLine("Vertex Format Features:");
                sb.AppendLine($"  Has Positions: Yes (always)");
                sb.AppendLine($"  Has Normals: {((geom.mesh.header.fvf & 0x10) != 0 ? "Yes" : "No")}");
                sb.AppendLine($"  Has Texture Coordinates: {((geom.mesh.header.fvf & 0x100) != 0 ? "Yes" : "No")}");
                sb.AppendLine($"  Has Diffuse Color: {((geom.mesh.header.fvf & 0x40) != 0 ? "Yes" : "No")}");
            }
            
            sb.AppendLine();
            sb.AppendLine("Usage Instructions:");
            sb.AppendLine("==================");
            sb.AppendLine("1. Import the .gltf file into Blender using File > Import > glTF 2.0");
            sb.AppendLine("2. The model should appear with correct orientation and scale");
            sb.AppendLine("3. Materials will be basic PBR materials - customize as needed");
            sb.AppendLine("4. UV coordinates are preserved for texture mapping");
            sb.AppendLine();
            sb.AppendLine("GLTF Format Benefits:");
            sb.AppendLine("- Industry standard 3D format");
            sb.AppendLine("- Better Blender compatibility than OBJ");
            sb.AppendLine("- Preserves material information");
            sb.AppendLine("- Supports modern rendering pipelines");
            
            return sb.ToString();
        }

        
        private static void CreateManualGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath = null)
        {
            // Handle texture discovery and copying (same logic as OBJ export)
            string textureFileName = null;
            string textureSourcePath = null;
            
            // Determine subset count for multi-material support
            int subsetCount = 1; // Default to single material
            if (geom != null && geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
            {
                subsetCount = geom.mesh.subset_seq.Length;
            }
            
            // Create texture lists for multi-material support
            List<string> textureFilenames = new List<string>();
            List<string> processedTextures = new List<string>(); // Track unique textures
            
            if (!string.IsNullOrEmpty(modelPath))
            {
                string modelFilename = Path.GetFileNameWithoutExtension(modelPath);
                string basePath = modelPath.Substring(0, modelPath.LastIndexOf("\\model\\") + 1);
                string texturePath = "";
                
                // Determine object type from path and use appropriate texture folder
                if (modelPath.Contains("\\model\\item\\"))
                {
                    texturePath = basePath + "texture\\item\\";
                }
                else if (modelPath.Contains("\\model\\character\\"))
                {
                    texturePath = basePath + "texture\\character\\";
                }
                else
                {
                    // Default behavior
                    texturePath = Path.GetDirectoryName(modelPath).Replace("\\model\\", "\\texture\\") + "\\";
                }
                
                // Discover textures for each subset
                for (int i = 0; i < subsetCount; i++)
                {
                    string subsetTextureFile = null;
                    
                    // Try subset-specific texture first (for multi-material models)
                    string subsetTexturePath = texturePath + modelFilename + "_" + i.ToString("D2") + ".bmp";
                    if (File.Exists(subsetTexturePath))
                    {
                        subsetTextureFile = modelFilename + "_" + i.ToString("D2") + ".bmp";
                    }
                    else
                    {
                        // Fall back to main texture
                        string mainTexturePath = texturePath + modelFilename + ".bmp";
                        if (File.Exists(mainTexturePath))
                        {
                            subsetTextureFile = modelFilename + ".bmp";
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(subsetTextureFile))
                    {
                        textureFilenames.Add(subsetTextureFile);
                        
                        // Copy texture if not already processed
                        if (!processedTextures.Contains(subsetTextureFile))
                        {
                            processedTextures.Add(subsetTextureFile);
                            string sourceTexturePath = texturePath + subsetTextureFile;
                            string textureOutputPath = Path.Combine(Path.GetDirectoryName(outputPath), subsetTextureFile);
                            try
                            {
                                File.Copy(sourceTexturePath, textureOutputPath, true);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to copy texture {subsetTextureFile}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        textureFilenames.Add(null); // No texture for this subset
                    }
                }
                
                // Set legacy textureFileName for fallback compatibility
                if (textureFilenames.Count > 0 && !string.IsNullOrEmpty(textureFilenames[0]))
                {
                    textureFileName = textureFilenames[0];
                }
            }
            
            // Create a complete GLTF structure manually with actual data
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"scene\": 0,");
            sb.AppendLine("  \"scenes\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"Scene\",");
            sb.AppendLine("      \"nodes\": [0]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"nodes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{modelName}\",");
            sb.AppendLine("      \"mesh\": 0");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"meshes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{modelName}_mesh\",");
            sb.AppendLine("      \"primitives\": [");
            
            // Create separate primitives for each subset (material) - NO NORMALS for flat shading
            if (geom != null && geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
            {
                for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                {
                    sb.AppendLine("        {");
                    sb.AppendLine("          \"attributes\": {");
                    sb.AppendLine("            \"POSITION\": 0");
                    // SKIP NORMALS TO FORCE FLAT SHADING
                    if (geom != null && (geom.mesh.header.fvf & 0x100) != 0)
                        sb.AppendLine("            ,\"TEXCOORD_0\": 1"); // Use accessor 1 since normals are skipped
                    sb.AppendLine("          },");
                    sb.AppendLine($"          \"indices\": {2 + subsetIndex},"); // Adjust index accessors
                    sb.AppendLine($"          \"material\": {subsetIndex}");
                    if (subsetIndex < geom.mesh.subset_seq.Length - 1)
                        sb.AppendLine("        },");
                    else
                        sb.AppendLine("        }");
                }
            }
            else
            {
                // Fallback for models without subsets - NO NORMALS
                sb.AppendLine("        {");
                sb.AppendLine("          \"attributes\": {");
                sb.AppendLine("            \"POSITION\": 0");
                // SKIP NORMALS TO FORCE FLAT SHADING
                if (geom != null && (geom.mesh.header.fvf & 0x100) != 0)
                    sb.AppendLine("            ,\"TEXCOORD_0\": 1"); // Use accessor 1 since normals are skipped
                sb.AppendLine("          },");
                if (geom != null && geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                    sb.AppendLine("          \"indices\": 2,"); // Adjust index accessor
                sb.AppendLine("          \"material\": 0");
                sb.AppendLine("        }");
            }
            
            sb.AppendLine("      ]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            
            // Create materials for each subset
            sb.AppendLine("  \"materials\": [");
            if (geom != null && geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
            {
                for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                {
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"name\": \"{modelName}_material_{subsetIndex}\",");
                    sb.AppendLine("      \"pbrMetallicRoughness\": {");
                    
                    // Try to get texture for this subset
                    string subsetTextureFileName = null;
                    int textureIndex = -1;
                    
                    if (subsetIndex < textureFilenames.Count && !string.IsNullOrEmpty(textureFilenames[subsetIndex]))
                    {
                        subsetTextureFileName = textureFilenames[subsetIndex];
                        textureIndex = processedTextures.IndexOf(subsetTextureFileName);
                    }
                    
                    if (textureIndex >= 0)
                    {
                        sb.AppendLine("        \"baseColorTexture\": {");
                        sb.AppendLine($"          \"index\": {textureIndex}");
                        sb.AppendLine("        },");
                    }
                    
                    // Add material properties if available
                    if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && geom.mtl_seq[subsetIndex] != null)
                    {
                        float difR = Math.Max(0.5f, geom.mtl_seq[subsetIndex].mtl.dif.r);
                        float difG = Math.Max(0.5f, geom.mtl_seq[subsetIndex].mtl.dif.g);
                        float difB = Math.Max(0.5f, geom.mtl_seq[subsetIndex].mtl.dif.b);
                        sb.AppendLine($"        \"baseColorFactor\": [{difR.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}, {difG.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}, {difB.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}, 1.0],");
                    }
                    else
                    {
                        sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                    }
                    
                    sb.AppendLine("        \"metallicFactor\": 0.0,");
                    sb.AppendLine("        \"roughnessFactor\": 0.5");
                    sb.AppendLine("      },");
                    sb.AppendLine("      \"doubleSided\": true");
                    if (subsetIndex < geom.mesh.subset_seq.Length - 1)
                        sb.AppendLine("    },");
                    else
                        sb.AppendLine("    }");
                }
            }
            else
            {
                // Fallback single material
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{modelName}_material\",");
                sb.AppendLine("      \"pbrMetallicRoughness\": {");
                if (!string.IsNullOrEmpty(textureFileName))
                {
                    sb.AppendLine("        \"baseColorTexture\": {");
                    sb.AppendLine("          \"index\": 0");
                    sb.AppendLine("        },");
                }
                sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                sb.AppendLine("        \"metallicFactor\": 0.0,");
                sb.AppendLine("        \"roughnessFactor\": 0.5");
                sb.AppendLine("      },");
                sb.AppendLine("      \"doubleSided\": true");
                sb.AppendLine("    }");
            }
            sb.AppendLine("  ],");
            
            // Add textures, samplers, and images sections if we have any textures
            if (textureFilenames.Any(t => !string.IsNullOrEmpty(t)))
            {
                sb.AppendLine("  \"textures\": [");
                for (int i = 0; i < processedTextures.Count; i++)
                {
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"sampler\": {i},");
                    sb.AppendLine($"      \"source\": {i}");
                    if (i < processedTextures.Count - 1)
                        sb.AppendLine("    },");
                    else
                        sb.AppendLine("    }");
                }
                sb.AppendLine("  ],");
                
                sb.AppendLine("  \"samplers\": [");
                for (int i = 0; i < processedTextures.Count; i++)
                {
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"magFilter\": 9729,");
                    sb.AppendLine("      \"minFilter\": 9987,");
                    sb.AppendLine("      \"wrapS\": 10497,");
                    sb.AppendLine("      \"wrapT\": 10497");
                    if (i < processedTextures.Count - 1)
                        sb.AppendLine("    },");
                    else
                        sb.AppendLine("    }");
                }
                sb.AppendLine("  ],");
                
                sb.AppendLine("  \"images\": [");
                for (int i = 0; i < processedTextures.Count; i++)
                {
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"uri\": \"{processedTextures[i]}\"");
                    if (i < processedTextures.Count - 1)
                    {
                        sb.AppendLine("    },");
                    }
                    else
                    {
                        sb.AppendLine("    }");
                    }
                }
                sb.AppendLine("  ],");
            }
            
            // Add accessors and buffer views if we have actual data
            if (geom != null && geom.mesh.vertex_seq != null && geom.mesh.vertex_seq.Length > 0)
            {
                // Create binary file instead of embedding data
                string binFilePath = Path.ChangeExtension(outputPath, ".bin");
                CreateBinaryData(geom, binFilePath);
                string binFileName = Path.GetFileName(binFilePath);
                
                sb.AppendLine("  \"accessors\": [");
                
                int byteOffset = 0;
                
                // Position accessor (0)
                sb.AppendLine("    {");
                sb.AppendLine("      \"bufferView\": 0,");
                sb.AppendLine("      \"componentType\": 5126,");
                sb.AppendLine("      \"count\": " + geom.mesh.vertex_seq.Length + ",");
                sb.AppendLine("      \"type\": \"VEC3\",");
                sb.AppendLine("      \"byteOffset\": 0");
                sb.AppendLine("    }");
                
                int accessorIndex = 1;
                int bufferViewIndex = 1;
                
                // SKIP NORMALS - accessor 1 will be texture coordinates if available
                
                // Texture coordinate accessor (1)
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"bufferView\": " + bufferViewIndex + ",");
                    sb.AppendLine("      \"componentType\": 5126,");
                    sb.AppendLine("      \"count\": " + Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) + ",");
                    sb.AppendLine("      \"type\": \"VEC2\",");
                    sb.AppendLine("      \"byteOffset\": 0");
                    sb.AppendLine("    }");
                    accessorIndex++;
                    bufferViewIndex++;
                }
                
                // Index accessors - create separate accessor for each subset (starting from accessor 2)
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    // Create index accessor for each subset
                    for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                    {
                        var subset = geom.mesh.subset_seq[subsetIndex];
                        // Calculate indices for this subset only
                        uint subsetIndexCount = subset.primitive_num * 3; // primitive_num is triangle count
                        
                        sb.AppendLine("    ,{");
                        sb.AppendLine("      \"bufferView\": " + (bufferViewIndex + subsetIndex) + ",");
                        sb.AppendLine("      \"componentType\": 5123,"); // 5123 = UNSIGNED_SHORT
                        sb.AppendLine("      \"count\": " + subsetIndexCount + ",");
                        sb.AppendLine("      \"type\": \"SCALAR\",");
                        sb.AppendLine("      \"byteOffset\": 0");
                        sb.AppendLine("    }");
                    }
                }
                else
                {
                    // Fallback single index accessor for models without subsets
                    int validIndexCount = 0;
                    for (int i = 0; i < geom.mesh.index_seq.Length; i += 3)
                    {
                        if (i + 2 < geom.mesh.index_seq.Length)
                        {
                            validIndexCount += 3;
                        }
                    }
                    
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"bufferView\": " + bufferViewIndex + ",");
                    sb.AppendLine("      \"componentType\": 5123,"); // 5123 = UNSIGNED_SHORT
                    sb.AppendLine("      \"count\": " + validIndexCount + ",");
                    sb.AppendLine("      \"type\": \"SCALAR\",");
                    sb.AppendLine("      \"byteOffset\": 0");
                    sb.AppendLine("    }");
                }
                
                sb.AppendLine("  ],");
                
                // Buffer views
                sb.AppendLine("  \"bufferViews\": [");
                byteOffset = 0;
                bufferViewIndex = 0;
                
                // Position buffer view (0)
                sb.AppendLine("    {");
                sb.AppendLine("      \"buffer\": 0,");
                sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                sb.AppendLine("      \"byteLength\": " + (geom.mesh.vertex_seq.Length * 12) + ",");
                sb.AppendLine("      \"target\": 34962");
                sb.AppendLine("    }");
                byteOffset += geom.mesh.vertex_seq.Length * 12;
                
                // SKIP NORMALS - buffer view 1 will be texture coordinates if available
                
                // Texture coordinate buffer view (1)
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"buffer\": 0,");
                    sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                    sb.AppendLine("      \"byteLength\": " + (Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) * 8) + ",");
                    sb.AppendLine("      \"target\": 34962");
                    sb.AppendLine("    }");
                    byteOffset += Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) * 8;
                }
                
                // Index buffer views - create separate buffer view for each subset
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    if (geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
                    {
                        // Create separate buffer view for each subset
                        for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                        {
                            var subset = geom.mesh.subset_seq[subsetIndex];
                            uint subsetIndexCount = subset.primitive_num * 3; // primitive_num is triangle count
                            
                            sb.AppendLine("    ,{");
                            sb.AppendLine("      \"buffer\": 0,");
                            sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                            sb.AppendLine("      \"byteLength\": " + (subsetIndexCount * 2) + ","); // 2 bytes per ushort
                            sb.AppendLine("      \"target\": 34963");
                            sb.AppendLine("    }");
                            byteOffset += (int)(subsetIndexCount * 2);
                        }
                    }
                    else
                    {
                        // Fallback single buffer view for models without subsets
                        int validIndexCount = 0;
                        for (int i = 0; i < geom.mesh.index_seq.Length; i += 3)
                        {
                            if (i + 2 < geom.mesh.index_seq.Length)
                            {
                                validIndexCount += 3;
                            }
                        }
                        
                        sb.AppendLine("    ,{");
                        sb.AppendLine("      \"buffer\": 0,");
                        sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                        sb.AppendLine("      \"byteLength\": " + (validIndexCount * 2) + ","); // 2 bytes per ushort
                        sb.AppendLine("      \"target\": 34963");
                        sb.AppendLine("    }");
                        byteOffset += validIndexCount * 2;
                    }
                }
                
                sb.AppendLine("  ],");
                
                // Buffer reference to external .bin file
                sb.AppendLine("  \"buffers\": [");
                sb.AppendLine("    {");
                sb.AppendLine("      \"byteLength\": " + byteOffset + ",");
                sb.AppendLine("      \"uri\": \"" + binFileName + "\"");
                sb.AppendLine("    }");
                sb.AppendLine("  ]");
            }
            
            sb.AppendLine("}");
            
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private static void CreateBinaryData(lwGeomObjInfo geom, string binFilePath)
        {
            using (var writer = new BinaryWriter(File.Open(binFilePath, FileMode.Create)))
            {
                // Write vertex positions exactly like Python script (no scaling, no coordinate conversion)
                for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                {
                    var vertex = geom.mesh.vertex_seq[i];
                    
                    // Write vertices exactly as-is like the Python script
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                
                // SKIP NORMALS COMPLETELY - let 3D software calculate flat normals automatically
                
                // Write texture coordinates if available
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    int texCoordCount = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length);
                    for (int i = 0; i < texCoordCount; i++)
                    {
                        var texCoord = geom.mesh.texcoord0_seq[i];
                        writer.Write(texCoord.x);
                        writer.Write(texCoord.y); // Use original V coordinate (no flip) for correct texture orientation
                    }
                }
                
                // Write indices per subset - each subset gets its own index range
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    if (geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
                    {
                        // Write indices for each subset separately based on PKO engine logic
                        for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                        {
                            var subset = geom.mesh.subset_seq[subsetIndex];
                            uint startIndex = subset.start_index;
                            uint primitiveCount = subset.primitive_num;
                            
                            // Write indices for this subset only
                            for (uint i = 0; i < primitiveCount; i++)
                            {
                                uint triangleStart = startIndex + (i * 3);
                                if (triangleStart + 2 < geom.mesh.index_seq.Length)
                                {
                                    uint idx0 = geom.mesh.index_seq[triangleStart + 0];
                                    uint idx1 = geom.mesh.index_seq[triangleStart + 1];
                                    uint idx2 = geom.mesh.index_seq[triangleStart + 2];
                                    
                                    // Write triangle for this subset
                                    writer.Write((ushort)idx0);
                                    writer.Write((ushort)idx1);
                                    writer.Write((ushort)idx2);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Process all indices sequentially for models without subsets
                        for (int i = 0; i < geom.mesh.index_seq.Length; i += 3)
                        {
                            if (i + 2 < geom.mesh.index_seq.Length)
                            {
                                uint idx0 = geom.mesh.index_seq[i + 0];
                                uint idx1 = geom.mesh.index_seq[i + 1];
                                uint idx2 = geom.mesh.index_seq[i + 2];
                                
                                // Write triangle in original order exactly like Python script (no validation)
                                writer.Write((ushort)idx0);
                                writer.Write((ushort)idx1);
                                writer.Write((ushort)idx2);
                            }
                        }
                    }
                }
            }
        }
    }
}
