using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PKOModelViewer.Mindpower;

namespace PKOModelViewer
{
    /// <summary>
    /// Simplified GLTF Exporter that doesn't depend on external libraries
    /// This version focuses on core functionality and avoids dependency issues
    /// </summary>
    public class GltfExporterSimplified
    {
        public static void ExportToGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath = null)
        {
            try
            {
                CreateManualGltf(geom, outputPath, modelName, modelPath);
                
                // Create info file
                string infoFile = Path.ChangeExtension(outputPath, ".txt");
                File.WriteAllText(infoFile, CreateExportInfo(geom, modelName, true));
            }
            catch (Exception ex)
            {
                // Log detailed error and fallback
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
                
                // Fallback to simple GLTF
                CreateManualGltf(geom, outputPath, modelName, modelPath);
            }
        }
        
        // Combined Character Export - Export all character parts as one GLTF file
        public static void ExportCombinedCharacterToGltf(List<lwGeomObjInfo> characterParts, List<string> partNames, List<string> partPaths, string outputPath, string characterName)
        {
            try
            {
                CreateCombinedGltf(characterParts, partNames, partPaths, outputPath, characterName);
                
                // Create analysis report
                string infoFile = Path.ChangeExtension(outputPath, ".txt");
                File.WriteAllText(infoFile, CreateCombinedExportInfo(characterParts, partNames, characterName, true));
            }
            catch (Exception ex)
            {
                // Enhanced error logging
                string errorFile = Path.ChangeExtension(outputPath, "_error.txt");
                File.WriteAllText(errorFile, CreateCombinedErrorReport(ex, characterParts, partNames, characterName));

                // Fallback to basic combined export
                CreateCombinedGltf(characterParts, partNames, partPaths, outputPath, characterName);
            }
        }
        
        private static void CreateManualGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath)
        {
            var sb = new StringBuilder();
            
            // Validate geometry data
            if (geom == null || geom.mesh.vertex_seq == null || geom.mesh.vertex_seq.Length == 0)
            {
                CreateEmptyGltf(outputPath, modelName);
                return;
            }
            
            // Handle textures
            List<string> textureFilenames = DiscoverTextures(geom, modelPath);
            
            // Create GLTF structure
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer Simplified\"");
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
            
            // Create mesh
            CreateGltfMesh(sb, geom, modelName, textureFilenames);
            
            // Create materials
            CreateGltfMaterials(sb, geom, modelName, textureFilenames);
            
            // Create textures if any
            if (textureFilenames.Any(t => !string.IsNullOrEmpty(t)))
            {
                CreateGltfTextures(sb, textureFilenames);
            }
            
            // Create buffers and accessors
            CreateGltfBuffers(sb, geom, outputPath);
            
            sb.AppendLine("}");
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private static void CreateCombinedGltf(List<lwGeomObjInfo> characterParts, List<string> partNames, List<string> partPaths, string outputPath, string characterName)
        {
            var sb = new StringBuilder();
            
            if (characterParts == null || characterParts.Count == 0)
            {
                CreateEmptyGltf(outputPath, characterName);
                return;
            }
            
            // Create GLTF structure for combined character
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer - Combined Character\"");
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
            sb.AppendLine($"      \"name\": \"{characterName}\",");
            sb.AppendLine("      \"mesh\": 0");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            
            // Create combined mesh with multiple primitives
            CreateCombinedMesh(sb, characterParts, partNames, characterName);
            
            // Create materials for all parts
            CreateCombinedMaterials(sb, characterParts, partNames, characterName);
            
            sb.AppendLine("}");
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private static void CreateEmptyGltf(string outputPath, string modelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer - Empty Model\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"scene\": 0,");
            sb.AppendLine("  \"scenes\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"EmptyScene\",");
            sb.AppendLine("      \"nodes\": []");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private static List<string> DiscoverTextures(lwGeomObjInfo geom, string modelPath)
        {
            var textures = new List<string>();
            
            if (string.IsNullOrEmpty(modelPath))
                return textures;
                
            try
            {
                string modelFilename = Path.GetFileNameWithoutExtension(modelPath);
                string basePath = modelPath.Substring(0, modelPath.LastIndexOf("\\model\\") + 1);
                string texturePath = "";
                
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
                    texturePath = Path.GetDirectoryName(modelPath).Replace("\\model\\", "\\texture\\") + "\\";
                }
                
                // Try to find main texture
                string mainTexture = texturePath + modelFilename + ".bmp";
                if (File.Exists(mainTexture))
                {
                    textures.Add(modelFilename + ".bmp");
                    
                    // Copy texture
                    string destTexture = Path.Combine(Path.GetDirectoryName(modelPath), modelFilename + ".bmp");
                    File.Copy(mainTexture, destTexture, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Texture discovery error: {ex.Message}");
            }
            
            return textures;
        }
        
        private static void CreateGltfMesh(StringBuilder sb, lwGeomObjInfo geom, string modelName, List<string> textureFilenames)
        {
            sb.AppendLine("  \"meshes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{modelName}_mesh\",");
            sb.AppendLine("      \"primitives\": [");
            sb.AppendLine("        {");
            sb.AppendLine("          \"attributes\": {");
            sb.AppendLine("            \"POSITION\": 0");
            if ((geom.mesh.header.fvf & 0x100) != 0)
                sb.AppendLine("            ,\"TEXCOORD_0\": 1");
            sb.AppendLine("          },");
            if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                sb.AppendLine("          \"indices\": 2,");
            sb.AppendLine("          \"material\": 0");
            sb.AppendLine("        }");
            sb.AppendLine("      ]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
        }
        
        private static void CreateCombinedMesh(StringBuilder sb, List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName)
        {
            sb.AppendLine("  \"meshes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{characterName}_combined_mesh\",");
            sb.AppendLine("      \"primitives\": [");
            
            for (int i = 0; i < characterParts.Count; i++)
            {
                var part = characterParts[i];
                if (part?.mesh?.vertex_seq != null && part.mesh.vertex_seq.Length > 0)
                {
                    sb.AppendLine("        {");
                    sb.AppendLine("          \"attributes\": {");
                    sb.AppendLine($"            \"POSITION\": {i * 3}");
                    if ((part.mesh.header.fvf & 0x100) != 0)
                        sb.AppendLine($"            ,\"TEXCOORD_0\": {i * 3 + 1}");
                    sb.AppendLine("          },");
                    if (part.mesh.index_seq != null && part.mesh.index_seq.Length > 0)
                        sb.AppendLine($"          \"indices\": {i * 3 + 2},");
                    sb.AppendLine($"          \"material\": {i}");
                    
                    if (i < characterParts.Count - 1)
                        sb.AppendLine("        },");
                    else
                        sb.AppendLine("        }");
                }
            }
            
            sb.AppendLine("      ]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
        }
        
        private static void CreateGltfMaterials(StringBuilder sb, lwGeomObjInfo geom, string modelName, List<string> textureFilenames)
        {
            sb.AppendLine("  \"materials\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{modelName}_material\",");
            sb.AppendLine("      \"pbrMetallicRoughness\": {");
            
            if (textureFilenames.Count > 0 && !string.IsNullOrEmpty(textureFilenames[0]))
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
            sb.AppendLine("  ],");
        }
        
        private static void CreateCombinedMaterials(StringBuilder sb, List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName)
        {
            sb.AppendLine("  \"materials\": [");
            
            for (int i = 0; i < characterParts.Count; i++)
            {
                string partName = (i < partNames.Count) ? partNames[i] : $"Part{i}";
                
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{characterName}_{partName}_material\",");
                sb.AppendLine("      \"pbrMetallicRoughness\": {");
                sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                sb.AppendLine("        \"metallicFactor\": 0.0,");
                sb.AppendLine("        \"roughnessFactor\": 0.5");
                sb.AppendLine("      },");
                sb.AppendLine("      \"doubleSided\": true");
                
                if (i < characterParts.Count - 1)
                    sb.AppendLine("    },");
                else
                    sb.AppendLine("    }");
            }
            
            sb.AppendLine("  ],");
        }
        
        private static void CreateGltfTextures(StringBuilder sb, List<string> textureFilenames)
        {
            var uniqueTextures = textureFilenames.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
            
            if (uniqueTextures.Count == 0)
                return;
                
            sb.AppendLine("  \"textures\": [");
            for (int i = 0; i < uniqueTextures.Count; i++)
            {
                sb.AppendLine("    {");
                sb.AppendLine($"      \"sampler\": {i},");
                sb.AppendLine($"      \"source\": {i}");
                if (i < uniqueTextures.Count - 1)
                    sb.AppendLine("    },");
                else
                    sb.AppendLine("    }");
            }
            sb.AppendLine("  ],");
            
            sb.AppendLine("  \"samplers\": [");
            for (int i = 0; i < uniqueTextures.Count; i++)
            {
                sb.AppendLine("    {");
                sb.AppendLine("      \"magFilter\": 9729,");
                sb.AppendLine("      \"minFilter\": 9987,");
                sb.AppendLine("      \"wrapS\": 10497,");
                sb.AppendLine("      \"wrapT\": 10497");
                if (i < uniqueTextures.Count - 1)
                    sb.AppendLine("    },");
                else
                    sb.AppendLine("    }");
            }
            sb.AppendLine("  ],");
            
            sb.AppendLine("  \"images\": [");
            for (int i = 0; i < uniqueTextures.Count; i++)
            {
                sb.AppendLine("    {");
                sb.AppendLine($"      \"uri\": \"{uniqueTextures[i]}\"");
                if (i < uniqueTextures.Count - 1)
                    sb.AppendLine("    },");
                else
                    sb.AppendLine("    }");
            }
            sb.AppendLine("  ],");
        }
        
        private static void CreateGltfBuffers(StringBuilder sb, lwGeomObjInfo geom, string outputPath)
        {
            // Create binary file
            string binFilePath = Path.ChangeExtension(outputPath, ".bin");
            CreateBinaryData(geom, binFilePath);
            string binFileName = Path.GetFileName(binFilePath);
            
            sb.AppendLine("  \"accessors\": [");
            
            // Position accessor
            sb.AppendLine("    {");
            sb.AppendLine("      \"bufferView\": 0,");
            sb.AppendLine("      \"componentType\": 5126,");
            sb.AppendLine("      \"count\": " + geom.mesh.vertex_seq.Length + ",");
            sb.AppendLine("      \"type\": \"VEC3\"");
            sb.AppendLine("    }");
            
            // Texture coordinate accessor
            if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
            {
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"bufferView\": 1,");
                sb.AppendLine("      \"componentType\": 5126,");
                sb.AppendLine("      \"count\": " + Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) + ",");
                sb.AppendLine("      \"type\": \"VEC2\"");
                sb.AppendLine("    }");
            }
            
            // Index accessor
            if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
            {
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"bufferView\": 2,");
                sb.AppendLine("      \"componentType\": 5123,");
                sb.AppendLine("      \"count\": " + geom.mesh.index_seq.Length + ",");
                sb.AppendLine("      \"type\": \"SCALAR\"");
                sb.AppendLine("    }");
            }
            
            sb.AppendLine("  ],");
            
            // Buffer views
            sb.AppendLine("  \"bufferViews\": [");
            
            int byteOffset = 0;
            
            // Position buffer view
            int positionByteLength = geom.mesh.vertex_seq.Length * 12; // 3 floats * 4 bytes
            sb.AppendLine("    {");
            sb.AppendLine("      \"buffer\": 0,");
            sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
            sb.AppendLine("      \"byteLength\": " + positionByteLength);
            sb.AppendLine("    }");
            byteOffset += positionByteLength;
            
            // Texture coordinate buffer view
            if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
            {
                int texCoordCount = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length);
                int texCoordByteLength = texCoordCount * 8; // 2 floats * 4 bytes
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"buffer\": 0,");
                sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                sb.AppendLine("      \"byteLength\": " + texCoordByteLength);
                sb.AppendLine("    }");
                byteOffset += texCoordByteLength;
            }
            
            // Index buffer view
            if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
            {
                int indexByteLength = geom.mesh.index_seq.Length * 2; // ushort = 2 bytes
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"buffer\": 0,");
                sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                sb.AppendLine("      \"byteLength\": " + indexByteLength);
                sb.AppendLine("    }");
            }
            
            sb.AppendLine("  ],");
            
            // Buffers
            sb.AppendLine("  \"buffers\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"uri\": \"{binFileName}\",");
            sb.AppendLine("      \"byteLength\": " + new FileInfo(binFilePath).Length);
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
        }
        
        private static void CreateBinaryData(lwGeomObjInfo geom, string binFilePath)
        {
            using (var writer = new BinaryWriter(File.Create(binFilePath)))
            {
                // Write vertex positions
                foreach (var vertex in geom.mesh.vertex_seq)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                
                // Write texture coordinates
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    int texCoordCount = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length);
                    for (int i = 0; i < texCoordCount; i++)
                    {
                        var uv = geom.mesh.texcoord0_seq[i];
                        writer.Write(uv.x);
                        writer.Write(uv.y);
                    }
                }
                
                // Write indices
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    foreach (var index in geom.mesh.index_seq)
                    {
                        writer.Write((ushort)index);
                    }
                }
            }
        }
        
        private static string CreateExportInfo(lwGeomObjInfo geom, string modelName, bool success)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKO Model Viewer - Simplified GLTF Export Report");
            sb.AppendLine("===============================================");
            sb.AppendLine($"Model Name: {modelName}");
            sb.AppendLine($"Export Status: {(success ? "SUCCESS" : "FALLBACK")}");
            sb.AppendLine($"Export Time: {DateTime.Now}");
            sb.AppendLine();
            
            if (geom?.mesh?.vertex_seq != null)
            {
                sb.AppendLine("Model Statistics:");
                sb.AppendLine($"  Vertices: {geom.mesh.header.vertex_num}");
                sb.AppendLine($"  Indices: {geom.mesh.header.index_num}");
                sb.AppendLine($"  Triangles: {geom.mesh.header.index_num / 3}");
                sb.AppendLine($"  Materials/Subsets: {geom.mesh.header.subset_num}");
                sb.AppendLine($"  Vertex Format (FVF): 0x{geom.mesh.header.fvf:X}");
            }
            
            return sb.ToString();
        }
        
        private static string CreateCombinedExportInfo(List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName, bool success)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKO Model Viewer - Combined Character GLTF Export Report");
            sb.AppendLine("=====================================================");
            sb.AppendLine($"Character Name: {characterName}");
            sb.AppendLine($"Export Status: {(success ? "SUCCESS" : "FALLBACK")}");
            sb.AppendLine($"Export Time: {DateTime.Now}");
            sb.AppendLine($"Total Parts: {characterParts?.Count ?? 0}");
            sb.AppendLine();
            
            if (characterParts != null)
            {
                for (int i = 0; i < characterParts.Count; i++)
                {
                    var part = characterParts[i];
                    string partName = (i < partNames.Count) ? partNames[i] : $"Part{i}";
                    
                    sb.AppendLine($"Part {i + 1}: {partName}");
                    if (part?.mesh?.vertex_seq != null)
                    {
                        sb.AppendLine($"  Vertices: {part.mesh.header.vertex_num}");
                        sb.AppendLine($"  Indices: {part.mesh.header.index_num}");
                        sb.AppendLine($"  Triangles: {part.mesh.header.index_num / 3}");
                    }
                    else
                    {
                        sb.AppendLine("  No valid geometry data");
                    }
                    sb.AppendLine();
                }
            }
            
            return sb.ToString();
        }
        
        private static string CreateCombinedErrorReport(Exception ex, List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Combined Character GLTF Export Error Report");
            sb.AppendLine("=========================================");
            sb.AppendLine($"Character Name: {characterName}");
            sb.AppendLine($"Error Time: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("Error Details:");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Type: {ex.GetType().Name}");
            sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");
            sb.AppendLine();
            sb.AppendLine($"Character Data:");
            sb.AppendLine($"Total Parts: {characterParts?.Count ?? 0}");
            
            return sb.ToString();
        }
    }
}
