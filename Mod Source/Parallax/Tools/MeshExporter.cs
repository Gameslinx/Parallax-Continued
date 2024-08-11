using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Tools
{
    /// <summary>
    /// Exports the .mu meshes from KSP to obj format
    /// </summary>
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class MeshExporter : MonoBehaviour
    {
        // Not used in release versions
        void Update()
        {
            return;

            bool flag = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha9);
            if (flag)
            {
                ParallaxDebug.Log("Exporting all models to GameData/Parallax/Exports/Models... ");
                // Now parse ALL scatter configs
                string basePath = KSPUtil.ApplicationRootPath + "GameData/Parallax/Exports/Models/";
                foreach (ParallaxScatterBody body in ConfigLoader.parallaxScatterBodies.Values)
                {
                    foreach (Scatter scatter in body.scatters.Values)
                    {
                        string lod0Path = scatter.modelPath;
                        string lod1Path = scatter.distributionParams.lod1.modelPathOverride;
                        string lod2Path = scatter.distributionParams.lod2.modelPathOverride;
        
                        ParallaxDebug.Log(" - " + lod0Path);
                        ParallaxDebug.Log(" - " + lod1Path);
                        ParallaxDebug.Log(" - " + lod2Path);
        
                        GameObject lod0 = GameDatabase.Instance.GetModel(lod0Path);
                        GameObject lod1 = GameDatabase.Instance.GetModel(lod1Path);
                        GameObject lod2 = GameDatabase.Instance.GetModel(lod2Path);
        
                        ExportMeshToOBJ(lod0, basePath + GetFileName(lod0Path) + ".obj");
                        ExportMeshToOBJ(lod1, basePath + GetFileName(lod1Path) + ".obj");
                        ExportMeshToOBJ(lod2, basePath + GetFileName(lod2Path) + ".obj");
                    }
                }
            }
        }

        public static string GetFileName(string path)
        {
            int lastIndex = path.LastIndexOf('/');
            if (lastIndex == -1)
            {
                // Handle cases where the path does not contain any backslashes
                return path;
            }
            return path.Substring(lastIndex + 1);
        }
        // From https://forum.unity.com/threads/export-unity-mesh-to-obj-or-fbx-format.222690/
        public static void ExportMeshToOBJ(GameObject gameObject, string filePath)
        {
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("No MeshFilter found on the given GameObject.");
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("No mesh found on the MeshFilter.");
                return;
            }

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write vertices
                foreach (Vector3 vertex in mesh.vertices)
                {
                    writer.WriteLine($"v {vertex.x} {vertex.y} {vertex.z}");
                }

                // Write UVs
                foreach (Vector2 uv in mesh.uv)
                {
                    writer.WriteLine($"vt {uv.x} {uv.y}");
                }

                // Write normals
                foreach (Vector3 normal in mesh.normals)
                {
                    writer.WriteLine($"vn {normal.x} {normal.y} {normal.z}");
                }

                // Write tangents
                foreach (Vector4 tangent in mesh.tangents)
                {
                    writer.WriteLine($"vtan {tangent.x} {tangent.y} {tangent.z} {tangent.w}");
                }

                // Write faces
                for (int i = 0; i < mesh.triangles.Length; i += 3)
                {
                    int index0 = mesh.triangles[i] + 1;
                    int index1 = mesh.triangles[i + 1] + 1;
                    int index2 = mesh.triangles[i + 2] + 1;
                    writer.WriteLine($"f {index0}/{index0}/{index0} {index1}/{index1}/{index1} {index2}/{index2}/{index2}");
                }
            }
        }
    }
}
