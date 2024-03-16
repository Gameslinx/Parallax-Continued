using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ConstructMeshTask
{
    // Data must be readonly

    public ConcurrentDictionary<Vector3, int> newVertexIndices = new ConcurrentDictionary<Vector3, int>();
    public List<int> newTris = new List<int>(22000);
    public List<Vector3> newVerts = new List<Vector3>(22000);
    public List<Vector3> newNormals = new List<Vector3>(22000);
    public List<Color> newColors = new List<Color>(22000);
    public static void ConstructMesh(in NativeArray<SubdividableTriangle> data)
    {
    }
}
