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
                // Based on PKO viewer analysis: Use EXACT same rendering logic as PKO viewer
                // PKO viewer uses GL.TexCoord2(t1.x, t1.y) directly - no transformations!
                CreateManualGltfWithStrategy(geom, outputPath, modelName, modelPath, UVStrategy.Default);
                
                // Create detailed info file with material analysis
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
                CreateManualGltfWithStrategy(geom, outputPath, modelName, modelPath, UVStrategy.Default);
            }
        }
        
        // Combined Character Export - Export all character parts as one GLTF file with multiple meshes
        public static void ExportCombinedCharacterToGltf(List<lwGeomObjInfo> characterParts, List<string> partNames, List<string> partPaths, string outputPath, string characterName, lwAnimDataBone boneData = null)
        {
            try
            {
                CreateCombinedCharacterGltf(characterParts, partNames, partPaths, outputPath, characterName, boneData);
                
                // Create detailed analysis report for combined character
                string infoFile = Path.ChangeExtension(outputPath, ".txt");
                File.WriteAllText(infoFile, CreateCombinedCharacterExportInfo(characterParts, partNames, characterName, true, boneData));
            }
            catch (Exception ex)
            {
                // Enhanced error logging for combined character export
                string errorFile = Path.ChangeExtension(outputPath, "_error.txt");
                File.WriteAllText(errorFile, CreateCombinedCharacterErrorReport(ex, characterParts, partNames, characterName, boneData));

                // Fallback to basic combined export
                CreateCombinedCharacterGltf(characterParts, partNames, partPaths, outputPath, characterName, boneData);
            }
        }
        
        private static void CreateTestVersions(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath)
        {
            // Create multiple test versions with different UV strategies
            string baseDir = Path.GetDirectoryName(outputPath);
            string baseName = Path.GetFileNameWithoutExtension(outputPath);
            
            // Test version 1: Flipped coordinates
            string test1Path = Path.Combine(baseDir, baseName + "_test_flipped.gltf");
            CreateManualGltfWithStrategy(geom, test1Path, modelName, modelPath, UVStrategy.Flipped);
            
            // Test version 2: Offset coordinates
            string test2Path = Path.Combine(baseDir, baseName + "_test_offset.gltf");
            CreateManualGltfWithStrategy(geom, test2Path, modelName, modelPath, UVStrategy.Offset);
            
            // Test version 3: Rotated coordinates
            string test3Path = Path.Combine(baseDir, baseName + "_test_rotated.gltf");
            CreateManualGltfWithStrategy(geom, test3Path, modelName, modelPath, UVStrategy.Rotated);
            
            // Test version 4: TexCoord1 if available
            if (geom.mesh.texcoord1_seq != null)
            {
                string test4Path = Path.Combine(baseDir, baseName + "_test_texcoord1.gltf");
                CreateManualGltfWithStrategy(geom, test4Path, modelName, modelPath, UVStrategy.TexCoord1);
            }
        }
        
        private enum UVStrategy
        {
            Default,
            Flipped,
            Offset,
            Rotated,
            TexCoord1
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
                sb.AppendLine();
                
                // Add UV mapping analysis for multi-material models
                if (geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 1)
                {
                    sb.AppendLine("UV Mapping Analysis:");
                    sb.AppendLine("====================");
                    
                    for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                    {
                        var subset = geom.mesh.subset_seq[subsetIndex];
                        sb.AppendLine($"Subset {subsetIndex}:");
                        sb.AppendLine($"  Triangles: {subset.primitive_num}");
                        sb.AppendLine($"  Start Index: {subset.start_index}");
                        sb.AppendLine($"  Min Vertex: {subset.min_index}");
                        sb.AppendLine($"  Vertex Count: {subset.vertex_num}");
                        
                        // Analyze UV ranges for this subset
                        if (geom.mesh.texcoord0_seq != null && geom.mesh.texcoord0_seq.Length > 0)
                        {
                            float minU = float.MaxValue, maxU = float.MinValue;
                            float minV = float.MaxValue, maxV = float.MinValue;
                            
                            for (uint i = 0; i < subset.primitive_num; i++)
                            {
                                uint triangleStart = subset.start_index + (i * 3);
                                if (triangleStart + 2 < geom.mesh.index_seq.Length)
                                {
                                    for (int j = 0; j < 3; j++)
                                    {
                                        uint vertexIndex = geom.mesh.index_seq[triangleStart + j];
                                        if (vertexIndex < geom.mesh.texcoord0_seq.Length)
                                        {
                                            var uv = geom.mesh.texcoord0_seq[vertexIndex];
                                            minU = Math.Min(minU, uv.x);
                                            maxU = Math.Max(maxU, uv.x);
                                            minV = Math.Min(minV, uv.y);
                                            maxV = Math.Max(maxV, uv.y);
                                        }
                                    }
                                }
                            }
                            
                            sb.AppendLine($"  UV Range: U({minU:0.0000}-{maxU:0.0000}) V({minV:0.0000}-{maxV:0.0000})");
                        }
                        
                        // Analyze PKO material/texture data for this subset
                        try
                        {
                            sb.AppendLine($"  Material Debug Info:");
                            sb.AppendLine($"    geom.mtl_seq null: {geom.mtl_seq == null}");
                            if (geom.mtl_seq != null)
                            {
                                sb.AppendLine($"    geom.mtl_seq.Length: {geom.mtl_seq.Length}");
                                sb.AppendLine($"    subsetIndex: {subsetIndex}");
                            }
                            sb.AppendLine($"    geom.mtl_num: {geom.mtl_num}");
                            
                            if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length)
                            {
                                var material = geom.mtl_seq[subsetIndex];
                                sb.AppendLine($"  Material Opacity: {material.opacity:0.0000}");
                                sb.AppendLine($"  Transparency Type: {material.transp_type}");
                                
                                // Check material colors
                                sb.AppendLine($"  Material Diffuse: R:{material.mtl.dif.r:0.00} G:{material.mtl.dif.g:0.00} B:{material.mtl.dif.b:0.00} A:{material.mtl.dif.a:0.00}");
                                sb.AppendLine($"  Material Ambient: R:{material.mtl.amb.r:0.00} G:{material.mtl.amb.g:0.00} B:{material.mtl.amb.b:0.00} A:{material.mtl.amb.a:0.00}");
                                
                                // Show computed transparency values for GLTF export
                                float finalAlpha = material.opacity * material.mtl.dif.a;
                                sb.AppendLine($"  Computed Final Alpha: {finalAlpha:0.0000} (opacity * diffuse.a)");
                                bool isTransparent = finalAlpha < 1.0f || material.transp_type != lwMtlTexInfoTransparencyTypeEnum.MTLTEX_TRANSP_FILTER;
                                sb.AppendLine($"  Will be transparent in GLTF: {isTransparent}");
                                
                                if (isTransparent)
                                {
                                    if (material.transp_type == lwMtlTexInfoTransparencyTypeEnum.MTLTEX_TRANSP_ADDITIVE ||
                                        material.transp_type == lwMtlTexInfoTransparencyTypeEnum.MTLTEX_TRANSP_ADDITIVE1 ||
                                        material.transp_type == lwMtlTexInfoTransparencyTypeEnum.MTLTEX_TRANSP_ADDITIVE2 ||
                                        material.transp_type == lwMtlTexInfoTransparencyTypeEnum.MTLTEX_TRANSP_ADDITIVE3)
                                    {
                                        sb.AppendLine($"  GLTF Alpha Mode: BLEND (additive)");
                                    }
                                    else if (finalAlpha < 0.5f)
                                    {
                                        sb.AppendLine($"  GLTF Alpha Mode: MASK (cutout)");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"  GLTF Alpha Mode: BLEND (smooth)");
                                    }
                                }
                                
                                if (material.tex_seq != null && material.tex_seq.Length > 0)
                                {
                                    sb.AppendLine($"  Texture Count: {material.tex_seq.Length}");
                                    for (int texIndex = 0; texIndex < material.tex_seq.Length; texIndex++)
                                    {
                                        var texture = material.tex_seq[texIndex];
                                        string fileName = new string(texture.file_name).Trim('\0');
                                        if (!string.IsNullOrEmpty(fileName))
                                        {
                                            sb.AppendLine($"  Texture {texIndex}: {fileName}");
                                            if (texture.data_pointer != 0)
                                            {
                                                sb.AppendLine($"    Data Pointer: 0x{texture.data_pointer:X}");
                                                sb.AppendLine($"    Width: {texture.width}, Height: {texture.height}");
                                                sb.AppendLine($"    Stage: {texture.stage}, Type: {texture.type}");
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"  Texture {texIndex}: [Empty filename]");
                                        }
                                    }
                                }
                                else
                                {
                                    sb.AppendLine($"  No textures in material (tex_seq null or empty)");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"  Cannot access material - index out of bounds or mtl_seq is null");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  Material Analysis Error: {ex.Message}");
                        }
                        
                        // Check if additional texture coordinate sets are available
                        if (geom.mesh.texcoord1_seq != null)
                        {
                            sb.AppendLine($"  Has TexCoord1: Yes ({geom.mesh.texcoord1_seq.Length} coordinates)");
                        }
                        if (geom.mesh.texcoord2_seq != null)
                        {
                            sb.AppendLine($"  Has TexCoord2: Yes ({geom.mesh.texcoord2_seq.Length} coordinates)");
                        }
                        if (geom.mesh.texcoord3_seq != null)
                        {
                            sb.AppendLine($"  Has TexCoord3: Yes ({geom.mesh.texcoord3_seq.Length} coordinates)");
                        }
                        
                        // Safe PKO Engine UV Matrix Analysis
                        try
                        {
                            if (geom.anim_data != null && geom.anim_data.anim_tex.mtl != null && 
                                subsetIndex < geom.anim_data.anim_tex.mtl.Length)
                            {
                                var uvAnim = geom.anim_data.anim_tex.mtl[subsetIndex];
                                if (uvAnim._mat_seq != null && uvAnim._mat_seq.Length > 0)
                                {
                                    sb.AppendLine($"  PKO UV Matrix: Available ({uvAnim._frame_num} frames)");
                                    
                                    // Show first frame matrix for debugging
                                    var matrix = uvAnim._mat_seq[0];
                                    sb.AppendLine($"  Matrix Frame 0:");
                                    sb.AppendLine($"    [{matrix.m[0]:0.0000}, {matrix.m[4]:0.0000}, {matrix.m[8]:0.0000}, {matrix.m[12]:0.0000}]");
                                    sb.AppendLine($"    [{matrix.m[1]:0.0000}, {matrix.m[5]:0.0000}, {matrix.m[9]:0.0000}, {matrix.m[13]:0.0000}]");
                                }
                                else
                                {
                                    sb.AppendLine($"  PKO UV Matrix: Not available");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"  PKO UV Matrix: Not available");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  PKO UV Matrix: Error accessing data ({ex.Message})");
                        }
                        
                        sb.AppendLine();
                    }
                }
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

        
        private static void CreateManualGltfWithStrategy(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath, UVStrategy uvStrategy)
        {
            // COMPREHENSIVE MATERIAL ANALYSIS - Same logic as PKO viewer uses
            System.Diagnostics.Debug.WriteLine("=== PKO MATERIAL SYSTEM ANALYSIS (1:1 with PKO Viewer) ===");
            
            try 
            {
                if (geom != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Model: {modelName}");
                    System.Diagnostics.Debug.WriteLine($"Geometry object: {geom != null}");
                    
                    if (geom.mesh.vertex_seq != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Mesh header - subset_num: {geom.mesh.header.subset_num}");
                        System.Diagnostics.Debug.WriteLine($"Mesh header - vertex_num: {geom.mesh.header.vertex_num}");
                        System.Diagnostics.Debug.WriteLine($"Mesh header - index_num: {geom.mesh.header.index_num}");
                        
                        // Check subset data (crucial for multi-material)
                        if (geom.mesh.subset_seq != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Subset sequence available: {geom.mesh.subset_seq.Length} subsets");
                            for (int s = 0; s < geom.mesh.subset_seq.Length; s++)
                            {
                                var subset = geom.mesh.subset_seq[s];
                                System.Diagnostics.Debug.WriteLine($"  Subset {s}: start_index={subset.start_index}, primitive_num={subset.primitive_num}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Subset sequence is NULL");
                        }
                    }
                    
                    // Material analysis - EXACTLY like PKO viewer MainForm.cs lines 515-527
                    System.Diagnostics.Debug.WriteLine($"Material count (mtl_num): {geom.mtl_num}");
                    if (geom.mtl_seq != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Material sequence available: {geom.mtl_seq.Length} materials");
                        for (int i = 0; i < geom.mtl_seq.Length; i++)
                        {
                            System.Diagnostics.Debug.WriteLine($"  Material {i}:");
                            var material = geom.mtl_seq[i];
                            if (material.tex_seq != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"    tex_seq length: {material.tex_seq.Length}");
                                for (int j = 0; j < material.tex_seq.Length; j++)
                                {
                                    if (material.tex_seq[j] != null && material.tex_seq[j].file_name != null)
                                    {
                                        // EXACT same logic as PKO viewer MainForm.cs line 520-525
                                        int length = 0;
                                        while (length < material.tex_seq[j].file_name.Length && material.tex_seq[j].file_name[length] != '\0') 
                                            length++;
                                        string filename = new string(material.tex_seq[j].file_name, 0, length);
                                        System.Diagnostics.Debug.WriteLine($"      Texture {j}: '{filename}'");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"    tex_seq is NULL");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Material sequence is NULL!");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Material analysis error: {ex.Message}");
            }
            
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
                
                // Discover textures for each subset using PKO material data - EXACT same method as PKO viewer
                System.Diagnostics.Debug.WriteLine($"=== TEXTURE DISCOVERY (PKO Viewer Method) ===");
                for (int i = 0; i < subsetCount; i++)
                {
                    string subsetTextureFile = null;
                    
                    // Get texture using EXACT same method as PKO viewer MainForm.cs DrawGeom function
                    try
                    {
                        if (geom.mtl_seq != null && i < geom.mtl_seq.Length && 
                            geom.mtl_seq[i].tex_seq != null && geom.mtl_seq[i].tex_seq.Length > 0)
                        {
                            // EXACT same logic as PKO viewer MainForm.cs line 520-525
                            if (geom.mtl_seq[i].tex_seq[0] != null && geom.mtl_seq[i].tex_seq[0].file_name != null)
                            {
                                int length = 0;
                                while (length < geom.mtl_seq[i].tex_seq[0].file_name.Length && geom.mtl_seq[i].tex_seq[0].file_name[length] != '\0') 
                                    length++;
                                string pkoTextureFile = new string(geom.mtl_seq[i].tex_seq[0].file_name, 0, length);
                                
                                // PKO viewer skips "1.BMP" (weapon effects) - same logic in DrawGeom
                                if (!string.IsNullOrEmpty(pkoTextureFile) && 
                                    !pkoTextureFile.Equals("1.BMP", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Use the exact texture file specified in PKO material
                                    subsetTextureFile = Path.GetFileName(pkoTextureFile);
                                    
                                    // Ensure .bmp extension
                                    if (!subsetTextureFile.ToLower().EndsWith(".bmp"))
                                    {
                                        subsetTextureFile += ".bmp";
                                    }
                                    
                                    System.Diagnostics.Debug.WriteLine($"PKO Material {i}: Found texture '{pkoTextureFile}' -> '{subsetTextureFile}'");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"PKO Material {i}: Texture file empty or is 1.BMP (effects)");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"PKO Material {i}: No material data available - mtl_seq null: {geom.mtl_seq == null}, length: {geom.mtl_seq?.Length ?? 0}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"PKO Material {i}: Error accessing material data: {ex.Message}");
                        // Fallback to file naming convention if PKO data access fails
                    }
                    
                    // Fallback to file naming convention if no PKO texture found
                    if (string.IsNullOrEmpty(subsetTextureFile))
                    {
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
            
            // Create a complete GLTF structure with Unreal Engine 5.6 compatibility
            var sb = new StringBuilder();
            
            // CRITICAL: Validate we have geometry data before creating GLTF
            if (geom == null || geom.mesh.vertex_seq == null || geom.mesh.vertex_seq.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: No geometry data available for GLTF export!");
                
                // Create minimal valid GLTF for Unreal compatibility
                sb.AppendLine("{");
                sb.AppendLine("  \"asset\": {");
                sb.AppendLine("    \"version\": \"2.0\",");
                sb.AppendLine("    \"generator\": \"PKO Model Viewer - No Geometry Error\",");
                sb.AppendLine("    \"minVersion\": \"2.0\"");
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
                return;
            }
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer\",");
            sb.AppendLine("    \"minVersion\": \"2.0\"");
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
            sb.AppendLine("      \"mesh\": 0,");
            sb.AppendLine("      \"translation\": [0.0, 0.0, 0.0],");
            sb.AppendLine("      \"rotation\": [0.0, 0.0, 0.0, 1.0],");
            sb.AppendLine("      \"scale\": [1.0, 1.0, 1.0]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"meshes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{modelName}_mesh\",");
            sb.AppendLine("      \"primitives\": [");
            
            // Create separate primitives for each subset (material) - SKIP 1.BMP subsets like PKO viewer
            if (geom != null && geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
            {
                List<int> validSubsets = new List<int>();
                
                // First pass: identify valid subsets (not using 1.BMP effects texture)
                for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                {
                    bool skipSubset = false;
                    
                    // Check if this subset uses 1.BMP (effects texture) - same logic as PKO viewer
                    try
                    {
                        if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && 
                            geom.mtl_seq[subsetIndex].tex_seq != null && geom.mtl_seq[subsetIndex].tex_seq.Length > 0 &&
                            geom.mtl_seq[subsetIndex].tex_seq[0] != null && geom.mtl_seq[subsetIndex].tex_seq[0].file_name != null)
                        {
                            int length = 0;
                            while (length < geom.mtl_seq[subsetIndex].tex_seq[0].file_name.Length && 
                                   geom.mtl_seq[subsetIndex].tex_seq[0].file_name[length] != '\0') 
                                length++;
                            string textureFile = new string(geom.mtl_seq[subsetIndex].tex_seq[0].file_name, 0, length);
                            
                            // EXACT same logic as PKO viewer: skip 1.BMP
                            if (textureFile.Equals("1.BMP", StringComparison.OrdinalIgnoreCase))
                            {
                                skipSubset = true;
                                System.Diagnostics.Debug.WriteLine($"SKIPPING Subset {subsetIndex}: Uses effects texture 1.BMP (same as PKO viewer)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking subset {subsetIndex} texture: {ex.Message}");
                    }
                    
                    if (!skipSubset)
                    {
                        validSubsets.Add(subsetIndex);
                        System.Diagnostics.Debug.WriteLine($"INCLUDING Subset {subsetIndex}: Valid subset for GLTF export");
                    }
                }
                
                // Safety check: if no valid subsets, create a fallback empty primitive (Unreal compatible)
                if (validSubsets.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: No valid subsets found! Creating fallback primitive.");
                    sb.AppendLine("        {");
                    sb.AppendLine("          \"attributes\": {");
                    sb.AppendLine("            \"POSITION\": 0,");
                    sb.AppendLine("            \"NORMAL\": 1");
                    sb.AppendLine("          },");
                    sb.AppendLine("          \"material\": 0");
                    sb.AppendLine("        }");
                }
                else
                {
                    // Second pass: create primitives only for valid subsets (Unreal Engine compatible)
                    for (int i = 0; i < validSubsets.Count; i++)
                    {
                        int subsetIndex = validSubsets[i];
                        sb.AppendLine("        {");
                        sb.AppendLine("          \"attributes\": {");
                        sb.AppendLine("            \"POSITION\": 0,");
                        sb.AppendLine("            \"NORMAL\": 1"); // Unreal Engine often requires normals
                        if (geom != null && (geom.mesh.header.fvf & 0x100) != 0)
                            sb.AppendLine("            ,\"TEXCOORD_0\": 2"); // Texture coordinates after normals
                        sb.AppendLine("          },");
                        sb.AppendLine($"          \"indices\": {3 + i},"); // Adjust for normals accessor
                        sb.AppendLine($"          \"material\": {i}"); // Use sequential material index for valid subsets only
                        if (i < validSubsets.Count - 1)
                            sb.AppendLine("        },");
                        else
                            sb.AppendLine("        }");
                    }
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
            
            // Create materials ONLY for valid subsets (not using 1.BMP effects texture)
            sb.AppendLine("  \"materials\": [");
            if (geom != null && geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
            {
                // Recompute valid subsets (same logic as primitives)
                List<int> validSubsets = new List<int>();
                
                for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                {
                    bool skipSubset = false;
                    
                    // Check if this subset uses 1.BMP (effects texture) - same logic as PKO viewer
                    try
                    {
                        if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && 
                            geom.mtl_seq[subsetIndex].tex_seq != null && geom.mtl_seq[subsetIndex].tex_seq.Length > 0 &&
                            geom.mtl_seq[subsetIndex].tex_seq[0] != null && geom.mtl_seq[subsetIndex].tex_seq[0].file_name != null)
                        {
                            int length = 0;
                            while (length < geom.mtl_seq[subsetIndex].tex_seq[0].file_name.Length && 
                                   geom.mtl_seq[subsetIndex].tex_seq[0].file_name[length] != '\0') 
                                length++;
                            string textureFile = new string(geom.mtl_seq[subsetIndex].tex_seq[0].file_name, 0, length);
                            
                            if (textureFile.Equals("1.BMP", StringComparison.OrdinalIgnoreCase))
                            {
                                skipSubset = true;
                            }
                        }
                    }
                    catch { }
                    
                    if (!skipSubset)
                    {
                        validSubsets.Add(subsetIndex);
                    }
                }
                
                // Safety check: if no valid subsets, create a fallback material
                if (validSubsets.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: No valid subsets for materials! Creating fallback material.");
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"name\": \"{modelName}_fallback_material\",");
                    sb.AppendLine("      \"pbrMetallicRoughness\": {");
                    
                    // PKO VIEWER DISCOVERY: PKO viewer uses GL.Color3(1f, 1f, 1f) - WHITE color, not material colors!
                    // All color comes from textures, material colors are ignored in PKO viewer rendering
                    if (geom.mtl_seq != null && geom.mtl_seq.Length > 0 && geom.mtl_seq[0] != null)
                    {
                        var material = geom.mtl_seq[0];
                        // PKO viewer only uses material.opacity for overall transparency, not diffuse colors
                        float materialAlpha = material.opacity; // Use only material opacity, ignore diffuse alpha
                        
                        // Always use white color like PKO viewer does: GL.Color3(1f, 1f, 1f)
                        sb.AppendLine($"        \"baseColorFactor\": [1.0000, 1.0000, 1.0000, {materialAlpha.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}],");
                    }
                    else
                    {
                        sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                    }
                    
                    sb.AppendLine("        \"metallicFactor\": 0.0,");
                    sb.AppendLine("        \"roughnessFactor\": 0.5");
                    sb.AppendLine("      },");
                    
                    // Add transparency for fallback material - PKO viewer uses global blending
                    if (geom.mtl_seq != null && geom.mtl_seq.Length > 0 && geom.mtl_seq[0] != null)
                    {
                        var material = geom.mtl_seq[0];
                        float materialAlpha = material.opacity; // Only use material opacity
                        
                        // PKO viewer enables global blending: GL.BlendFunc(SrcAlpha, OneMinusSrcAlpha)
                        // Transparency comes from texture alpha and material opacity
                        if (materialAlpha < 1.0f)
                        {
                            // Use BLEND mode for any transparency - PKO viewer uses global blending
                            sb.AppendLine("      \"alphaMode\": \"BLEND\",");
                        }
                    }
                    
                    sb.AppendLine("      \"doubleSided\": true");
                    sb.AppendLine("    }");
                }
                else
                {
                    for (int i = 0; i < validSubsets.Count; i++)
                    {
                        int subsetIndex = validSubsets[i];
                        sb.AppendLine("    {");
                        sb.AppendLine($"      \"name\": \"{modelName}_material_{subsetIndex}\",");
                        sb.AppendLine("      \"pbrMetallicRoughness\": {");
                        
                        // Try to get texture for this valid subset
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
                        
                        // PKO MATERIAL DISCOVERY: PKO viewer ignores material colors completely!
                        // PKO viewer uses GL.Color3(1f, 1f, 1f) white and gets color from texture
                        if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && geom.mtl_seq[subsetIndex] != null)
                        {
                            var material = geom.mtl_seq[subsetIndex];
                            
                            // PKO viewer only uses material.opacity, ignores diffuse colors entirely
                            float materialAlpha = material.opacity; // Only material opacity, no diffuse alpha
                            
                            // Always use white like PKO viewer: GL.Color3(1f, 1f, 1f)
                            sb.AppendLine($"        \"baseColorFactor\": [1.0000, 1.0000, 1.0000, {materialAlpha.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}],");
                        }
                        else
                        {
                            sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                        }
                        
                        sb.AppendLine("        \"metallicFactor\": 0.0,");
                        sb.AppendLine("        \"roughnessFactor\": 0.5");
                        sb.AppendLine("      },");
                        
                        // PKO transparency: PKO viewer uses global GL.BlendFunc(SrcAlpha, OneMinusSrcAlpha)
                        if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && geom.mtl_seq[subsetIndex] != null)
                        {
                            var material = geom.mtl_seq[subsetIndex];
                            float materialAlpha = material.opacity; // Only material opacity matters
                            
                            // PKO viewer has global blending enabled, transparency comes from texture alpha + material opacity
                            if (materialAlpha < 1.0f)
                            {
                                // Use BLEND for any transparency - matches PKO viewer's global blending
                                sb.AppendLine("      \"alphaMode\": \"BLEND\",");
                            }
                        }
                        
                        sb.AppendLine("      \"doubleSided\": true");
                        if (i < validSubsets.Count - 1)
                            sb.AppendLine("    },");
                        else
                            sb.AppendLine("    }");
                    }
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
                
                // PKO DISCOVERY: Use white color like PKO viewer, not material colors
                if (geom.mtl_seq != null && geom.mtl_seq.Length > 0 && geom.mtl_seq[0] != null)
                {
                    var material = geom.mtl_seq[0];
                    // PKO viewer ignores diffuse colors: GL.Color3(1f, 1f, 1f)
                    float materialAlpha = material.opacity; // Only material opacity
                    
                    sb.AppendLine($"        \"baseColorFactor\": [1.0000, 1.0000, 1.0000, {materialAlpha.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}],");
                }
                else
                {
                    sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                }
                
                sb.AppendLine("        \"metallicFactor\": 0.0,");
                sb.AppendLine("        \"roughnessFactor\": 0.5");
                sb.AppendLine("      },");
                
                // PKO transparency for fallback material
                if (geom.mtl_seq != null && geom.mtl_seq.Length > 0 && geom.mtl_seq[0] != null)
                {
                    var material = geom.mtl_seq[0];
                    float materialAlpha = material.opacity; // Only material opacity
                    
                    // PKO viewer uses global blending, so use BLEND for any transparency
                    if (materialAlpha < 1.0f)
                    {
                        sb.AppendLine("      \"alphaMode\": \"BLEND\",");
                    }
                }
                
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
                CreateBinaryDataWithStrategy(geom, binFilePath, uvStrategy);
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
                
                // Normal accessor (1) - Unreal Engine compatibility
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"bufferView\": " + bufferViewIndex + ",");
                sb.AppendLine("      \"componentType\": 5126,");
                sb.AppendLine("      \"count\": " + geom.mesh.vertex_seq.Length + ",");
                sb.AppendLine("      \"type\": \"VEC3\",");
                sb.AppendLine("      \"byteOffset\": 0");
                sb.AppendLine("    }");
                accessorIndex++;
                bufferViewIndex++;
                
                // Texture coordinate accessor (2)
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
                
                // Normal buffer view (1) - Unreal Engine compatibility
                sb.AppendLine("    ,{");
                sb.AppendLine("      \"buffer\": 0,");
                sb.AppendLine("      \"byteOffset\": " + byteOffset + ",");
                sb.AppendLine("      \"byteLength\": " + (geom.mesh.vertex_seq.Length * 12) + ",");
                sb.AppendLine("      \"target\": 34962");
                sb.AppendLine("    }");
                byteOffset += geom.mesh.vertex_seq.Length * 12;
                
                // Texture coordinate buffer view (2)
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
            
            string gltfContent = sb.ToString();
            
            // Debug: Log the generated GLTF content
            System.Diagnostics.Debug.WriteLine("=== GENERATED GLTF CONTENT ===");
            string[] lines = gltfContent.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Line {i + 1}: {lines[i]}");
            }
            System.Diagnostics.Debug.WriteLine("=== END GLTF CONTENT ===");
            
            File.WriteAllText(outputPath, gltfContent);
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
                
                // Write normals for Unreal Engine compatibility - generate flat normals
                for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                {
                    // For now, write simple up-pointing normals
                    // In a real implementation, you'd calculate proper normals from triangles
                    writer.Write(0.0f); // normal.x
                    writer.Write(0.0f); // normal.y  
                    writer.Write(1.0f); // normal.z (pointing up)
                }
                
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
                
                // CRITICAL FIX: Write indices ONLY for valid subsets (not using 1.BMP) - same logic as GLTF JSON
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    if (geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
                    {
                        // First pass: identify valid subsets (not using 1.BMP effects texture) - EXACT same logic as GLTF
                        List<int> validSubsets = new List<int>();
                        for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                        {
                            bool skipSubset = false;
                            
                            // Check if this subset uses 1.BMP (effects texture) - same logic as PKO viewer
                            try
                            {
                                if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && 
                                    geom.mtl_seq[subsetIndex].tex_seq != null && geom.mtl_seq[subsetIndex].tex_seq.Length > 0 &&
                                    geom.mtl_seq[subsetIndex].tex_seq[0] != null && geom.mtl_seq[subsetIndex].tex_seq[0].file_name != null)
                                {
                                    int length = 0;
                                    while (length < geom.mtl_seq[subsetIndex].tex_seq[0].file_name.Length && 
                                           geom.mtl_seq[subsetIndex].tex_seq[0].file_name[length] != '\0') 
                                        length++;
                                    string textureFile = new string(geom.mtl_seq[subsetIndex].tex_seq[0].file_name, 0, length);
                                    
                                    // EXACT same logic as PKO viewer: skip 1.BMP
                                    if (textureFile.Equals("1.BMP", StringComparison.OrdinalIgnoreCase))
                                    {
                                        skipSubset = true;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // If error reading material, include the subset
                            }
                            
                            if (!skipSubset)
                            {
                                validSubsets.Add(subsetIndex);
                            }
                        }
                        
                        // Write indices ONLY for valid subsets
                        foreach (int subsetIndex in validSubsets)
                        {
                            var subset = geom.mesh.subset_seq[subsetIndex];
                            uint startIndex = subset.start_index;
                            uint primitiveCount = subset.primitive_num;
                            
                            // Write indices for this valid subset only
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
        
        private static void CreateBinaryDataWithStrategy(lwGeomObjInfo geom, string binFilePath, UVStrategy uvStrategy)
        {
            using (var writer = new BinaryWriter(File.Open(binFilePath, FileMode.Create)))
            {
                // Write vertex positions exactly like PKO viewer (no scaling, no coordinate conversion)
                for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                {
                    var vertex = geom.mesh.vertex_seq[i];
                    
                    // Write vertices exactly as PKO viewer uses them
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                
                // Write normals for Unreal Engine compatibility (calculate flat normals)
                for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                {
                    // Use default upward normal for compatibility - Unreal will recalculate if needed
                    writer.Write(0.0f); // Normal X
                    writer.Write(0.0f); // Normal Y  
                    writer.Write(1.0f); // Normal Z (pointing up)
                }
                
                // Write texture coordinates EXACTLY like PKO viewer - GL.TexCoord2(t1.x, t1.y)
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    int texCoordCount = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length);
                    
                    for (int i = 0; i < texCoordCount; i++)
                    {
                        var texCoord = geom.mesh.texcoord0_seq[i];
                        
                        // Use EXACT same UV coordinates as PKO viewer - NO TRANSFORMATIONS!
                        // PKO viewer uses: GL.TexCoord2(t1.x, t1.y) directly
                        writer.Write(texCoord.x);  // Exact X coordinate
                        writer.Write(texCoord.y);  // Exact Y coordinate
                    }
                }
                
                // Write indices per subset with CRITICAL index validation
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"=== INDEX VALIDATION ===");
                    System.Diagnostics.Debug.WriteLine($"Total vertices: {geom.mesh.vertex_seq.Length}");
                    System.Diagnostics.Debug.WriteLine($"Total indices: {geom.mesh.index_seq.Length}");
                    
                    if (geom.mesh.subset_seq != null && geom.mesh.subset_seq.Length > 0)
                    {
                        // Write indices ONLY for valid subsets (not using 1.BMP effects texture)
                        for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                        {
                            // Check if this subset uses 1.BMP (effects texture) - skip it like PKO viewer
                            bool skipSubset = false;
                            try
                            {
                                if (geom.mtl_seq != null && subsetIndex < geom.mtl_seq.Length && 
                                    geom.mtl_seq[subsetIndex].tex_seq != null && geom.mtl_seq[subsetIndex].tex_seq.Length > 0 &&
                                    geom.mtl_seq[subsetIndex].tex_seq[0] != null && geom.mtl_seq[subsetIndex].tex_seq[0].file_name != null)
                                {
                                    int length = 0;
                                    while (length < geom.mtl_seq[subsetIndex].tex_seq[0].file_name.Length && 
                                           geom.mtl_seq[subsetIndex].tex_seq[0].file_name[length] != '\0') 
                                        length++;
                                    string textureFile = new string(geom.mtl_seq[subsetIndex].tex_seq[0].file_name, 0, length);
                                    
                                    if (textureFile.Equals("1.BMP", StringComparison.OrdinalIgnoreCase))
                                    {
                                        skipSubset = true;
                                        System.Diagnostics.Debug.WriteLine($"BINARY DATA: Skipping subset {subsetIndex} indices (1.BMP effects texture)");
                                    }
                                }
                            }
                            catch { }
                            
                            if (skipSubset) continue; // Skip this subset like PKO viewer does
                            
                            var subset = geom.mesh.subset_seq[subsetIndex];
                            uint startIndex = subset.start_index;
                            uint primitiveCount = subset.primitive_num;
                            
                            System.Diagnostics.Debug.WriteLine($"BINARY DATA: Writing subset {subsetIndex}: start_index={startIndex}, primitive_num={primitiveCount}");
                            
                            // Write indices for this subset only - same range as PKO viewer
                            for (uint i = 0; i < primitiveCount; i++)
                            {
                                uint triangleStart = startIndex + (i * 3);
                                if (triangleStart + 2 < geom.mesh.index_seq.Length)
                                {
                                    uint idx0 = geom.mesh.index_seq[triangleStart + 0];
                                    uint idx1 = geom.mesh.index_seq[triangleStart + 1];
                                    uint idx2 = geom.mesh.index_seq[triangleStart + 2];
                                    
                                    // CRITICAL: Validate indices are within vertex bounds
                                    if (idx0 < geom.mesh.vertex_seq.Length && 
                                        idx1 < geom.mesh.vertex_seq.Length && 
                                        idx2 < geom.mesh.vertex_seq.Length)
                                    {
                                        writer.Write((ushort)idx0);
                                        writer.Write((ushort)idx1);
                                        writer.Write((ushort)idx2);
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"WARNING: Invalid indices {idx0},{idx1},{idx2} for vertex count {geom.mesh.vertex_seq.Length}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Process all indices sequentially with validation
                        for (int i = 0; i < geom.mesh.index_seq.Length; i += 3)
                        {
                            if (i + 2 < geom.mesh.index_seq.Length)
                            {
                                uint idx0 = geom.mesh.index_seq[i + 0];
                                uint idx1 = geom.mesh.index_seq[i + 1];
                                uint idx2 = geom.mesh.index_seq[i + 2];
                                
                                // CRITICAL: Validate indices are within vertex bounds
                                if (idx0 < geom.mesh.vertex_seq.Length && 
                                    idx1 < geom.mesh.vertex_seq.Length && 
                                    idx2 < geom.mesh.vertex_seq.Length)
                                {
                                    writer.Write((ushort)idx0);
                                    writer.Write((ushort)idx1);
                                    writer.Write((ushort)idx2);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"WARNING: Invalid indices {idx0},{idx1},{idx2} for vertex count {geom.mesh.vertex_seq.Length}");
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private static int GetVertexSubsetByIndex(lwGeomObjInfo geom, int vertexIndex)
        {
            try
            {
                if (geom.mesh.subset_seq == null || geom.mesh.index_seq == null) return 0;
                
                // Use the SAME logic as PKO viewer: check which subset's index range contains this vertex
                for (int subsetIndex = 0; subsetIndex < geom.mesh.subset_seq.Length; subsetIndex++)
                {
                    var subset = geom.mesh.subset_seq[subsetIndex];
                    
                    // Check if this vertex appears in this subset's index range
                    uint startIndex = subset.start_index;
                    uint endIndex = subset.start_index + (subset.primitive_num * 3);
                    
                    for (uint i = startIndex; i < endIndex && i < geom.mesh.index_seq.Length; i++)
                    {
                        if (geom.mesh.index_seq[i] == vertexIndex)
                        {
                            return subsetIndex;
                        }
                    }
                }
                
                return 0; // Default to first subset
            }
            catch
            {
                return 0; // Safe fallback
            }
        }
        
        private static int FindVertexSubset(lwGeomObjInfo geom, int vertexIndex)
        {
            try
            {
                if (geom.mesh.subset_seq == null) return 0;
                
                // Use a different approach - check against subset index ranges
                if (geom.mesh.index_seq != null && geom.mesh.subset_seq.Length > 1)
                {
                    // For swords, typically subset 0 = handle, subset 1 = blade
                    // Use vertex position or index ranges to determine subset
                    
                    var subset0 = geom.mesh.subset_seq[0];
                    var subset1 = geom.mesh.subset_seq[1];
                    
                    // Check if vertex falls within subset 1 (blade) index range
                    uint blade_start = subset1.start_index;
                    uint blade_end = subset1.start_index + (subset1.primitive_num * 3);
                    
                    // Simple heuristic: if vertex index is in the second half, it's likely blade
                    if (vertexIndex >= geom.mesh.vertex_seq.Length / 2)
                    {
                        return 1; // Blade subset
                    }
                    else
                    {
                        return 0; // Handle subset
                    }
                }
                
                return 0; // Default to first subset
            }
            catch
            {
                return 0; // Safe fallback
            }
        }
        
        private static (bool hasTransform, Func<float, float, float> transformedU, Func<float, float, float> transformedV) GetSafeUVTransformation(lwGeomObjInfo geom, int subsetIndex, UVStrategy strategy)
        {
            try
            {
                // Try to access PKO animation data UV matrices
                if (geom.mesh.subset_seq != null && subsetIndex < geom.mesh.subset_seq.Length)
                {
                    // For now, return false as we need to investigate the exact structure
                    // This is the safe implementation that provides fallback behavior
                }
            }
            catch
            {
                // Safe fallback if PKO data access fails
            }
            
            return (false, null, null);
        }
        
        // Combined Character Export Helper Methods
        private static void CreateCombinedCharacterGltf(List<lwGeomObjInfo> characterParts, List<string> partNames, List<string> partPaths, string outputPath, string characterName, lwAnimDataBone boneData)
        {
            System.Diagnostics.Debug.WriteLine("=== PKO COMBINED CHARACTER EXPORT ===");
            System.Diagnostics.Debug.WriteLine($"Character: {characterName}");
            System.Diagnostics.Debug.WriteLine($"Parts: {characterParts.Count}");
            System.Diagnostics.Debug.WriteLine($"Bone Data: {(boneData != null ? $"Available ({boneData._header.bone_num} bones)" : "None")}");
            
            if (characterParts == null || characterParts.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: No character parts to export!");
                return;
            }

            var sb = new StringBuilder();
            
            // GLTF Header
            sb.AppendLine("{");
            sb.AppendLine("  \"asset\": {");
            sb.AppendLine("    \"version\": \"2.0\",");
            sb.AppendLine("    \"generator\": \"PKO Model Viewer - Combined Character Export\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"scene\": 0,");
            
            // Scenes
            sb.AppendLine("  \"scenes\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"PKO_Character_Scene\",");
            sb.AppendLine("      \"nodes\": [0]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");

            // Root character node that contains all parts
            sb.AppendLine("  \"nodes\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{characterName}_Complete\",");
            sb.AppendLine("      \"children\": [");
            
            // Add child nodes for each character part
            var childIndices = new List<string>();
            for (int i = 0; i < partNames.Count; i++)
            {
                childIndices.Add((i + 1).ToString());
            }
            sb.AppendLine($"        {string.Join(", ", childIndices)}");
            sb.AppendLine("      ]");
            sb.AppendLine("    },");
            
            // Character part nodes
            for (int i = 0; i < partNames.Count; i++)
            {
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{partNames[i]}\",");
                sb.AppendLine($"      \"mesh\": {i}");
                sb.AppendLine("    }");
                
                if (i < partNames.Count - 1)
                    sb.AppendLine("    ,");
            }
            
            sb.AppendLine("  ],");
            
            // Create meshes section for all character parts
            sb.AppendLine("  \"meshes\": [");
            
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var geom = characterParts[partIndex];
                var partName = partNames[partIndex];
                
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{partName}_Mesh\",");
                sb.AppendLine("      \"primitives\": [");
                sb.AppendLine("        {");
                sb.AppendLine("          \"attributes\": {");
                sb.AppendLine($"            \"POSITION\": {partIndex * 3}");
                
                // Add normals
                sb.AppendLine($"            ,\"NORMAL\": {partIndex * 3 + 1}");
                
                // Add texture coordinates if available
                if (geom != null && (geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    sb.AppendLine($"            ,\"TEXCOORD_0\": {partIndex * 3 + 2}");
                }
                
                sb.AppendLine("          },");
                
                // Add indices if available
                if (geom?.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    sb.AppendLine($"          \"indices\": {characterParts.Count * 3 + partIndex},");
                }
                
                sb.AppendLine($"          \"material\": {partIndex}");
                sb.AppendLine("        }");
                sb.AppendLine("      ]");
                sb.AppendLine("    }");
                
                if (partIndex < characterParts.Count - 1)
                    sb.AppendLine("    ,");
            }
            
            sb.AppendLine("  ],");
            
            // Create materials for all character parts
            sb.AppendLine("  \"materials\": [");
            
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var partName = partNames[partIndex];
                
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{partName}_Material\",");
                sb.AppendLine("      \"pbrMetallicRoughness\": {");
                sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
                sb.AppendLine("        \"metallicFactor\": 0.0,");
                sb.AppendLine("        \"roughnessFactor\": 0.5");
                sb.AppendLine("      },");
                sb.AppendLine("      \"doubleSided\": true");
                sb.AppendLine("    }");
                
                if (partIndex < characterParts.Count - 1)
                    sb.AppendLine("    ,");
            }
            
            sb.AppendLine("  ],");
            
            // Create accessors and buffers for combined character
            sb.AppendLine("  \"accessors\": [");
            
            // Create accessors for each character part
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var geom = characterParts[partIndex];
                
                if (geom != null && geom.mesh.vertex_seq != null)
                {
                    // Position accessor
                    sb.AppendLine($"    {{\"bufferView\": {partIndex * 3}, \"componentType\": 5126, \"count\": {geom.mesh.vertex_seq.Length}, \"type\": \"VEC3\"}},");
                    
                    // Normal accessor
                    sb.AppendLine($"    {{\"bufferView\": {partIndex * 3 + 1}, \"componentType\": 5126, \"count\": {geom.mesh.vertex_seq.Length}, \"type\": \"VEC3\"}},");
                    
                    // Texture coordinates if available
                    if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                    {
                        sb.AppendLine($"    {{\"bufferView\": {partIndex * 3 + 2}, \"componentType\": 5126, \"count\": {geom.mesh.vertex_seq.Length}, \"type\": \"VEC2\"}},");
                    }
                }
            }
            
            // Index accessors
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var geom = characterParts[partIndex];
                if (geom?.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    sb.AppendLine($"    {{\"bufferView\": {characterParts.Count * 3 + partIndex}, \"componentType\": 5123, \"count\": {geom.mesh.index_seq.Length}, \"type\": \"SCALAR\"}}");
                    
                    if (partIndex < characterParts.Count - 1)
                        sb.AppendLine("    ,");
                }
            }
            
            sb.AppendLine("  ],");
            
            // Buffer views (simplified)
            sb.AppendLine("  \"bufferViews\": [");
            
            int bufferOffset = 0;
            
            // Vertex data buffer views
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var geom = characterParts[partIndex];
                if (geom?.mesh.vertex_seq != null)
                {
                    // Position buffer view
                    sb.AppendLine($"    {{\"buffer\": 0, \"byteOffset\": {bufferOffset}, \"byteLength\": {geom.mesh.vertex_seq.Length * 12}, \"target\": 34962}},");
                    bufferOffset += geom.mesh.vertex_seq.Length * 12;
                    
                    // Normal buffer view
                    sb.AppendLine($"    {{\"buffer\": 0, \"byteOffset\": {bufferOffset}, \"byteLength\": {geom.mesh.vertex_seq.Length * 12}, \"target\": 34962}},");
                    bufferOffset += geom.mesh.vertex_seq.Length * 12;
                    
                    // UV buffer view if available
                    if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                    {
                        sb.AppendLine($"    {{\"buffer\": 0, \"byteOffset\": {bufferOffset}, \"byteLength\": {geom.mesh.vertex_seq.Length * 8}, \"target\": 34962}},");
                        bufferOffset += geom.mesh.vertex_seq.Length * 8;
                    }
                }
            }
            
            // Index buffer views
            for (int partIndex = 0; partIndex < characterParts.Count; partIndex++)
            {
                var geom = characterParts[partIndex];
                if (geom?.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    sb.AppendLine($"    {{\"buffer\": 0, \"byteOffset\": {bufferOffset}, \"byteLength\": {geom.mesh.index_seq.Length * 2}, \"target\": 34963}}");
                    bufferOffset += geom.mesh.index_seq.Length * 2;
                    
                    if (partIndex < characterParts.Count - 1)
                        sb.AppendLine("    ,");
                }
            }
            
            sb.AppendLine("  ],");
            
            // Buffer reference
            sb.AppendLine("  \"buffers\": [");
            sb.AppendLine("    {");
            sb.AppendLine($"      \"uri\": \"{Path.GetFileName(Path.ChangeExtension(outputPath, ".bin"))}\",");
            sb.AppendLine($"      \"byteLength\": {bufferOffset}");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");

            sb.AppendLine("}");

            // Write GLTF file
            File.WriteAllText(outputPath, sb.ToString());
            
            // Create binary data for combined character
            string binPath = Path.ChangeExtension(outputPath, ".bin");
            CreateCombinedCharacterBinaryData(characterParts, boneData, binPath);
            
            System.Diagnostics.Debug.WriteLine($"Combined Character GLTF exported: {outputPath}");
            System.Diagnostics.Debug.WriteLine($"Binary data: {binPath}");
        }
        
        private static void CreateCombinedCharacterBinaryData(List<lwGeomObjInfo> characterParts, lwAnimDataBone boneData, string binPath)
        {
            using (var writer = new BinaryWriter(File.Open(binPath, FileMode.Create)))
            {
                // Write data for each character part
                foreach (var geom in characterParts)
                {
                    if (geom?.mesh.vertex_seq != null)
                    {
                        // Write vertex positions
                        foreach (var vertex in geom.mesh.vertex_seq)
                        {
                            writer.Write(vertex.x);
                            writer.Write(vertex.y);
                            writer.Write(vertex.z);
                        }
                        
                        // Write normals (simple up-facing normals)
                        for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                        {
                            writer.Write(0.0f);
                            writer.Write(0.0f);
                            writer.Write(1.0f);
                        }
                        
                        // Write UVs if available
                        if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                        {
                            foreach (var uv in geom.mesh.texcoord0_seq)
                            {
                                writer.Write(uv.x);
                                writer.Write(uv.y);
                            }
                        }
                    }
                }
                
                // Write indices for each character part
                foreach (var geom in characterParts)
                {
                    if (geom?.mesh.index_seq != null)
                    {
                        foreach (var index in geom.mesh.index_seq)
                        {
                            writer.Write((ushort)index);
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Combined character binary data written: {new FileInfo(binPath).Length} bytes");
        }
        
        private static string CreateCombinedCharacterExportInfo(List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName, bool success, lwAnimDataBone boneData)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKO Model Viewer - Combined Character GLTF Export Report");
            sb.AppendLine("========================================================");
            sb.AppendLine($"Character Name: {characterName}");
            sb.AppendLine($"Export Status: {(success ? "SUCCESS" : "FALLBACK")}");
            sb.AppendLine($"Export Time: {DateTime.Now}");
            sb.AppendLine($"Character Parts: {characterParts.Count}");
            sb.AppendLine();
            
            // Analyze each character part
            for (int i = 0; i < characterParts.Count; i++)
            {
                var geom = characterParts[i];
                var partName = partNames[i];
                
                sb.AppendLine($"Part {i + 1}: {partName}");
                sb.AppendLine("====================");
                
                if (geom?.mesh.vertex_seq != null)
                {
                    sb.AppendLine($"  Vertices: {geom.mesh.header.vertex_num}");
                    sb.AppendLine($"  Indices: {geom.mesh.header.index_num}");
                    sb.AppendLine($"  Triangles: {geom.mesh.header.index_num / 3}");
                    sb.AppendLine($"  Materials: {geom.mesh.header.subset_num}");
                    sb.AppendLine($"  Has UVs: {((geom.mesh.header.fvf & 0x100) != 0 ? "Yes" : "No")}");
                    sb.AppendLine($"  Has Skinning: {(geom.mesh.blend_seq != null ? "Yes" : "No")}");
                }
                else
                {
                    sb.AppendLine("  ERROR: No geometry data");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine();
            sb.AppendLine("Combined Export Benefits:");
            sb.AppendLine("========================");
            sb.AppendLine("✅ Single GLTF file for entire character");
            sb.AppendLine("✅ All body parts in one model");
            sb.AppendLine("✅ Easier import into 3D software");
            sb.AppendLine("✅ Better organization and workflow");
            sb.AppendLine("✅ Reduced file clutter");
            
            return sb.ToString();
        }
        
        private static string CreateCombinedCharacterErrorReport(Exception ex, List<lwGeomObjInfo> characterParts, List<string> partNames, string characterName, lwAnimDataBone boneData)
        {
            return $"PKO Combined Character GLTF Export Error\n" +
                   $"========================================\n" +
                   $"Character: {characterName}\n" +
                   $"Parts: {characterParts?.Count ?? 0}\n" +
                   $"Error: {ex.Message}\n" +
                   $"Type: {ex.GetType().Name}\n" +
                   $"Stack: {ex.StackTrace}\n\n" +
                   $"Character Data:\n" +
                   $"Parts Count: {characterParts?.Count ?? 0}\n" +
                   $"Part Names: {(partNames != null ? string.Join(", ", partNames) : "None")}\n" +
                   $"Bone Data: {(boneData != null ? $"Available ({boneData._header.bone_num} bones)" : "None")}\n";
        }
    }
}
