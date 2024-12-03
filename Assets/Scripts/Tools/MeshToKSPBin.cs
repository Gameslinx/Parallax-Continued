using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEditor;

[CustomEditor(typeof(MeshToKSPBin))]
class MeshToKSPBinButton : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Generate Bin"))
        {
            MeshToKSPBin script = (MeshToKSPBin)target;
            script.Generate();
        }
    }
}

public class MeshToKSPBin : MonoBehaviour
{
    public Mesh kopMesh;
    public void Generate()
    {
        String binPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Kerbal Space Program\\GameData\\ParallaxContinued\\Models\\ScaledMesh.bin";

        //obj = new OBJLoader().Load(OBJmodelPath);
        //Mesh kopMesh = obj.GetComponentInChildren<MeshFilter>().mesh;

        if (kopMesh == null)
        {
            Debug.Log("Please specify a mesh!");
        }

        SerializeMesh(kopMesh, binPath);
    }

    public static void RecalculateTangents(Mesh theMesh)
    {
        Int32 vertexCount = theMesh.vertexCount;
        Vector3[] vertices = theMesh.vertices;
        Vector3[] normals = theMesh.normals;
        Vector2[] uv = theMesh.uv;
        Int32[] triangles = theMesh.triangles;
        Int32 triangleCount = triangles.Length / 3;

        Vector4[] tangents = new Vector4[vertexCount];
        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];

        Int32 tri = 0;

        for (Int32 i = 0; i < triangleCount; i++)
        {
            Int32 i1 = triangles[tri];
            Int32 i2 = triangles[tri + 1];
            Int32 i3 = triangles[tri + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = uv[i1];
            Vector2 w2 = uv[i2];
            Vector2 w3 = uv[i3];

            Single x1 = v2.x - v1.x;
            Single x2 = v3.x - v1.x;
            Single y1 = v2.y - v1.y;
            Single y2 = v3.y - v1.y;
            Single z1 = v2.z - v1.z;
            Single z2 = v3.z - v1.z;

            Single s1 = w2.x - w1.x;
            Single s2 = w3.x - w1.x;
            Single t1 = w2.y - w1.y;
            Single t2 = w3.y - w1.y;

            Single r = 1.0f / (s1 * t2 - s2 * t1);
            Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sdir;
            tan1[i2] += sdir;
            tan1[i3] += sdir;

            tan2[i1] += tdir;
            tan2[i2] += tdir;
            tan2[i3] += tdir;

            tri += 3;
        }
        for (Int32 i = 0; i < vertexCount; i++)
        {
            Vector3 n = normals[i];
            Vector3 t = tan1[i];

            Vector3.OrthoNormalize(ref n, ref t);

            tangents[i].x = t.x;
            tangents[i].y = t.y;
            tangents[i].z = t.z;

            // Calculate handedness
            tangents[i].w = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f ? -1.0f : 1.0f;
        }
        theMesh.tangents = tangents;
    }
    public static void SerializeMesh(Mesh mesh, String path)
        {
            Debug.Log(mesh.vertexCount);
            // Open an output file stream
            FileStream outputStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            RecalculateTangents(mesh);
            using (BinaryWriter writer = new BinaryWriter(outputStream))
            {

                // Indicate that this is version two of the .bin format
                writer.Write(-2);

                // Write the vertex count of the mesh
                writer.Write(mesh.vertices.Length);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                writer.Write(mesh.uv.Length);
                foreach (Vector2 uv in mesh.uv)
                {
                    writer.Write(uv.x);
                    writer.Write(uv.y);
                }
                writer.Write(mesh.triangles.Length);
                foreach (Int32 triangle in mesh.triangles)
                {
                    writer.Write(triangle);
                }
                writer.Write(mesh.uv2.Length);
                foreach (Vector2 uv2 in mesh.uv2)
                {
                    writer.Write(uv2.x);
                    writer.Write(uv2.y);
                }
                writer.Write(mesh.normals.Length);
                foreach (Vector3 normal in mesh.normals)
                {
                    writer.Write(normal.x);
                    writer.Write(normal.y);
                    writer.Write(normal.z);
                }
                writer.Write(mesh.tangents.Length);
                foreach (Vector4 tangent in mesh.tangents)
                {
                    writer.Write(tangent.x);
                    writer.Write(tangent.y);
                    writer.Write(tangent.z);
                    writer.Write(tangent.w);
                }

                // Finish writing
                writer.Close();
                outputStream.Close();
            }
        }

}
