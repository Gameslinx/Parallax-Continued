using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GenerateScaledMesh))]
class GenerateScaledMeshButton : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Generate Bin"))
        {
            GenerateScaledMesh script = (GenerateScaledMesh)target;
            script.Start();
        }
    }
}

public class GenerateScaledMesh : MonoBehaviour
{
    public string planetName;
    public Texture2D heightmap;
    public double bodyRadius;
    public float minAltitude;
    public float maxAltitude;
    public bool ocean;
    // Start is called before the first frame update
    public void Start()
    {
        Mesh exportedMesh = Instantiate(GetComponent<MeshFilter>().sharedMesh);

        Vector3[] verts = exportedMesh.vertices;
        Vector2[] uvs = exportedMesh.uv;
        Vector3[] normals = exportedMesh.normals;
        
        // Calculate scaling factors world -> local
        // We know this because the mesh is always a parallax scaled mesh
        double meshRadius = 1000.0f;
        double planetRadius = bodyRadius;
        double worldToScaledFactor = meshRadius / planetRadius;
        
        // World space real min/max altitude
        // We'll pad it a bit so the atmosphere is always slightly above
        float minRadialAlt = (float)((minAltitude) * worldToScaledFactor);
        float maxRadialAlt = (float)((maxAltitude) * worldToScaledFactor);
        
        float heightValue;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector2 uv = uvs[i];
            heightValue = heightmap.GetPixelBilinear(uv.x, uv.y, 0).r;
            float altitude = Mathf.Lerp(minRadialAlt, maxRadialAlt, heightValue);
            if (ocean && altitude < 0)
            {
                altitude = 0;
            }

            verts[i] = verts[i] + normals[i] * altitude;
        }

        exportedMesh.vertices = verts;

        MeshExporter.SaveMeshAsOBJ(exportedMesh, "C:\\Users\\tuvee\\Documents\\PlanetBinFiles\\" + planetName + ".obj");
        DestroyImmediate(exportedMesh);
    }
}
