using System.IO;
using System.Text;
using UnityEngine;

public static class MeshExporter
{
    /// <summary>
    /// Saves the given mesh to a .obj file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="filePath">The file path where the .obj file will be saved.</param>
    public static void SaveMeshAsOBJ(Mesh mesh, string filePath)
    {
        if (mesh == null)
        {
            Debug.LogError("Mesh is null. Cannot export.");
            return;
        }

        StringBuilder sb = new StringBuilder();

        // Write vertices
        foreach (Vector3 vertex in mesh.vertices)
        {
            sb.AppendLine($"v {vertex.x} {vertex.y} {vertex.z}");
        }

        // Write UVs
        foreach (Vector2 uv in mesh.uv)
        {
            sb.AppendLine($"vt {uv.x} {uv.y}");
        }

        // Write normals
        foreach (Vector3 normal in mesh.normals)
        {
            sb.AppendLine($"vn {normal.x} {normal.y} {normal.z}");
        }

        // Write tangents
        foreach (Vector4 tangent in mesh.tangents)
        {
            sb.AppendLine($"# tangent {tangent.x} {tangent.y} {tangent.z} {tangent.w}");
        }

        // Write faces with shared/smooth normals
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i] + 1;
            int v2 = triangles[i + 1] + 1;
            int v3 = triangles[i + 2] + 1;

            string uv1 = uvs.Length > 0 ? v1.ToString() : "";
            string uv2 = uvs.Length > 0 ? v2.ToString() : "";
            string uv3 = uvs.Length > 0 ? v3.ToString() : "";

            string n1 = normals.Length > 0 ? v1.ToString() : "";
            string n2 = normals.Length > 0 ? v2.ToString() : "";
            string n3 = normals.Length > 0 ? v3.ToString() : "";

            sb.AppendLine($"f {v1}/{uv1}/{n1} {v2}/{uv2}/{n2} {v3}/{uv3}/{n3}");
        }

        try
        {
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Mesh successfully exported to {filePath}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"Failed to save mesh to file: {ex.Message}");
        }
    }
}
