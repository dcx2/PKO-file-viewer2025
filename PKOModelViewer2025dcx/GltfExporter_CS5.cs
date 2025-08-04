using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mindpower;

namespace PKOModelViewer
{
    /// <summary>
    /// C# 5.0 Compatible GLTF Exporter - No string interpolation, basic functionality only
    /// </summary>
    public class GltfExporter
    {
        public static void ExportToGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath = null)
        {
            try
            {
                CreateManualGltf(geom, outputPath, modelName, modelPath);
                
                // Create info file
                string infoFile = Path.ChangeExtension(outputPath, ".txt");
                File.WriteAllText(infoFile, CreateExportInfo(geom, modelName));
                
            }
            catch (Exception ex)
            {
                // Log error and fallback
                string errorFile = Path.ChangeExtension(outputPath, "_error.txt");
                File.WriteAllText(errorFile, 
                    "GLTF Export Error Details:\n" +
                    "=========================\n" +
                    "Error: " + ex.Message + "\n" +
                    "Type: " + ex.GetType().Name + "\n" +
                    "Stack trace:\n" + ex.StackTrace + "\n\n" +
                    "Model Data:\n" +
                    "Vertices: " + (geom != null && geom.mesh.vertex_seq != null ? geom.mesh.header.vertex_num.ToString() : "0") + "\n" +
                    "Indices: " + (geom != null && geom.mesh.index_seq != null ? geom.mesh.header.index_num.ToString() : "0") + "\n");
                
                // Fallback
                CreateManualGltf(geom, outputPath, modelName, modelPath);
            }
        }
        
        // Combined Character Export - Basic version
        public static void ExportCombinedCharacterToGltf(List<lwGeomObjInfo> characterParts, List<string> partNames, List<string> partPaths, string outputPath, string characterName, lwAnimDataBone boneData = null)
        {
            try
            {
                // For now, just export the first part as a fallback
                if (characterParts != null && characterParts.Count > 0)
                {
                    ExportToGltf(characterParts[0], outputPath, characterName + "_combined", null);
                }
            }
            catch (Exception ex)
            {
                string errorFile = Path.ChangeExtension(outputPath, "_error.txt");
                File.WriteAllText(errorFile, "Combined Character Export Error: " + ex.Message);
            }
        }
        
        private static void CreateManualGltf(lwGeomObjInfo geom, string outputPath, string modelName, string modelPath)
        {
            var sb = new StringBuilder();
            
            // Validate geometry
            if (geom == null || geom.mesh.vertex_seq == null || geom.mesh.vertex_seq.Length == 0)
            {
                // Create minimal valid GLTF
                sb.AppendLine("{");
                sb.AppendLine("  \"asset\": {");
                sb.AppendLine("    \"version\": \"2.0\",");
                sb.AppendLine("    \"generator\": \"PKO Model Viewer - No Geometry\"");
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
            
            // Create basic GLTF structure
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
            sb.AppendLine("      \"name\": \"" + modelName + "\",");
            sb.AppendLine("      \"mesh\": 0");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"meshes\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"" + modelName + "_mesh\",");
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
            sb.AppendLine("  \"materials\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"" + modelName + "_material\",");
            sb.AppendLine("      \"pbrMetallicRoughness\": {");
            sb.AppendLine("        \"baseColorFactor\": [1.0, 1.0, 1.0, 1.0],");
            sb.AppendLine("        \"metallicFactor\": 0.0,");
            sb.AppendLine("        \"roughnessFactor\": 0.5");
            sb.AppendLine("      },");
            sb.AppendLine("      \"doubleSided\": true");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            
            // Add accessors and buffer views if we have data
            if (geom.mesh.vertex_seq != null && geom.mesh.vertex_seq.Length > 0)
            {
                // Create binary file
                string binFilePath = Path.ChangeExtension(outputPath, ".bin");
                CreateBinaryData(geom, binFilePath);
                string binFileName = Path.GetFileName(binFilePath);
                
                sb.AppendLine("  \"accessors\": [");
                
                // Position accessor (0)
                sb.AppendLine("    {");
                sb.AppendLine("      \"bufferView\": 0,");
                sb.AppendLine("      \"componentType\": 5126,");
                sb.AppendLine("      \"count\": " + geom.mesh.vertex_seq.Length + ",");
                sb.AppendLine("      \"type\": \"VEC3\"");
                sb.AppendLine("    }");
                
                int accessorIndex = 1;
                int bufferViewIndex = 1;
                
                // Texture coordinate accessor (1)
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"bufferView\": " + bufferViewIndex + ",");
                    sb.AppendLine("      \"componentType\": 5126,");
                    sb.AppendLine("      \"count\": " + Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) + ",");
                    sb.AppendLine("      \"type\": \"VEC2\"");
                    sb.AppendLine("    }");
                    accessorIndex++;
                    bufferViewIndex++;
                }
                
                // Index accessor
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"bufferView\": " + bufferViewIndex + ",");
                    sb.AppendLine("      \"componentType\": 5123,");
                    sb.AppendLine("      \"count\": " + geom.mesh.index_seq.Length + ",");
                    sb.AppendLine("      \"type\": \"SCALAR\"");
                    sb.AppendLine("    }");
                }
                
                sb.AppendLine("  ],");
                
                // Buffer views
                sb.AppendLine("  \"bufferViews\": [");
                
                int currentOffset = 0;
                
                // Position buffer view (0)
                int positionSize = geom.mesh.vertex_seq.Length * 12; // 3 floats * 4 bytes
                sb.AppendLine("    {");
                sb.AppendLine("      \"buffer\": 0,");
                sb.AppendLine("      \"byteOffset\": " + currentOffset + ",");
                sb.AppendLine("      \"byteLength\": " + positionSize);
                sb.AppendLine("    }");
                currentOffset += positionSize;
                
                // Texture coordinate buffer view (1)
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    int uvSize = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length) * 8; // 2 floats * 4 bytes
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"buffer\": 0,");
                    sb.AppendLine("      \"byteOffset\": " + currentOffset + ",");
                    sb.AppendLine("      \"byteLength\": " + uvSize);
                    sb.AppendLine("    }");
                    currentOffset += uvSize;
                }
                
                // Index buffer view
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    int indexSize = geom.mesh.index_seq.Length * 2; // ushort indices
                    sb.AppendLine("    ,{");
                    sb.AppendLine("      \"buffer\": 0,");
                    sb.AppendLine("      \"byteOffset\": " + currentOffset + ",");
                    sb.AppendLine("      \"byteLength\": " + indexSize);
                    sb.AppendLine("    }");
                }
                
                sb.AppendLine("  ],");
                
                // Buffer
                sb.AppendLine("  \"buffers\": [");
                sb.AppendLine("    {");
                sb.AppendLine("      \"uri\": \"" + binFileName + "\",");
                sb.AppendLine("      \"byteLength\": " + new FileInfo(binFilePath).Length);
                sb.AppendLine("    }");
                sb.AppendLine("  ]");
            }
            
            sb.AppendLine("}");
            
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private static void CreateBinaryData(lwGeomObjInfo geom, string binFilePath)
        {
            using (var stream = new FileStream(binFilePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // Write positions
                for (int i = 0; i < geom.mesh.vertex_seq.Length; i++)
                {
                    var vertex = geom.mesh.vertex_seq[i];
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                
                // Write texture coordinates
                if ((geom.mesh.header.fvf & 0x100) != 0 && geom.mesh.texcoord0_seq != null)
                {
                    int uvCount = Math.Min(geom.mesh.texcoord0_seq.Length, geom.mesh.vertex_seq.Length);
                    for (int i = 0; i < uvCount; i++)
                    {
                        var uv = geom.mesh.texcoord0_seq[i];
                        writer.Write(uv.x);
                        writer.Write(uv.y);
                    }
                }
                
                // Write indices
                if (geom.mesh.index_seq != null && geom.mesh.index_seq.Length > 0)
                {
                    for (int i = 0; i < geom.mesh.index_seq.Length; i++)
                    {
                        writer.Write((ushort)geom.mesh.index_seq[i]);
                    }
                }
            }
        }
        
        private static string CreateExportInfo(lwGeomObjInfo geom, string modelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKO Model Viewer - GLTF Export Report");
            sb.AppendLine("=====================================");
            sb.AppendLine("Model Name: " + modelName);
            sb.AppendLine("Export Time: " + DateTime.Now);
            sb.AppendLine();
            
            if (geom != null && geom.mesh.vertex_seq != null)
            {
                sb.AppendLine("Model Statistics:");
                sb.AppendLine("  Vertices: " + geom.mesh.header.vertex_num);
                sb.AppendLine("  Indices: " + geom.mesh.header.index_num);
                sb.AppendLine("  Triangles: " + (geom.mesh.header.index_num / 3));
                sb.AppendLine("  Materials: " + geom.mesh.header.subset_num);
                sb.AppendLine("  Vertex Format (FVF): 0x" + geom.mesh.header.fvf.ToString("X"));
                sb.AppendLine();
                
                sb.AppendLine("Vertex Format Features:");
                sb.AppendLine("  Has Positions: Yes");
                sb.AppendLine("  Has Normals: " + ((geom.mesh.header.fvf & 0x10) != 0 ? "Yes" : "No"));
                sb.AppendLine("  Has Texture Coordinates: " + ((geom.mesh.header.fvf & 0x100) != 0 ? "Yes" : "No"));
                sb.AppendLine("  Has Diffuse Color: " + ((geom.mesh.header.fvf & 0x40) != 0 ? "Yes" : "No"));
            }
            
            sb.AppendLine();
            sb.AppendLine("Usage Instructions:");
            sb.AppendLine("==================");
            sb.AppendLine("1. Import the .gltf file into Blender using File > Import > glTF 2.0");
            sb.AppendLine("2. The model should appear with correct orientation and scale");
            sb.AppendLine("3. Materials will be basic PBR materials - customize as needed");
            sb.AppendLine("4. UV coordinates are preserved for texture mapping");
            
            return sb.ToString();
        }
    }
}
