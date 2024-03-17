using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using static UnityEngine.GraphicsBuffer;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine.Rendering;

public struct SubdividableTriangle
{
    public Vector3 v1, v2, v3;
    public Vector3 n1, n2, n3;
    public Color c1, c2, c3;
    public SubdividableTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, Color c1, Color c2, Color c3)
    {
        this.v1 = v1; this.v2 = v2; this.v3 = v3;
        this.n1 = n1; this.n2 = n2; this.n3 = n3;
        this.c1 = c1; this.c2 = c2; this.c3 = c3;
    }
    public void Subdivide(ref NativeStream.Writer tris, int level, Vector3 target, int maxSubdivisionLevel, float dist1, float dist2, float dist3)
    {
        if (level == maxSubdivisionLevel) { return; }

        // Get which verts are actually in range
        //float distancev1 = Vector3.Distance(v1, target);
        int subdivisionLevelv1 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist1 / 1.0f);

        //float distancev2 = Vector3.Distance(v2, target);
        int subdivisionLevelv2 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist2 / 1.0f);

        //float distancev3 = Vector3.Distance(v3, target);
        int subdivisionLevelv3 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist3 / 1.0f);

        if (AreTwoVertsOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
        {
            tris.Write(this);
            return;
        }
        else if (IsOneVertexOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
        {
            // Case 1: Line connecting v1 and midpoint between v2 and v3 - two tris
            if (subdivisionLevelv1 < subdivisionLevelv2)
            {
                Vector3 midPoint = GetVertexBetween(v2, v3);
                SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                tris.Write(v3v1midPoint);
                tris.Write(v1v2midPoint);
                return;
            }
            if (subdivisionLevelv2 < subdivisionLevelv3)
            {
                Vector3 midPoint = GetVertexBetween(v1, v3);
                SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                tris.Write(v1v2midPoint);
                tris.Write(v2v3midPoint);
                return;
            }
            if (subdivisionLevelv3 < subdivisionLevelv1)
            {
                Vector3 midPoint = GetVertexBetween(v1, v2);
                SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                tris.Write(v3v1midPoint);
                tris.Write(v2v3midPoint);
                return;
            }
        }
        else if (AllThreeOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
        {
            tris.Write(this);
            return;
        }

        // Divide triangle into 4 new triangles

        // Top tri
        Vector3 tv1 = GetVertexBetween(v1, v3);
        Vector3 tv2 = GetVertexBetween(v3, v2);
        Vector3 tv3 = v3;

        Vector3 tn1 = GetNormalBetween(n1, n3);
        Vector3 tn2 = GetNormalBetween(n3, n2);
        Vector3 tn3 = n3;

        Color tc1 = GetColorBetween(c1, c3);
        Color tc2 = GetColorBetween(c3, c2);
        Color tc3 = c3;

        float td1 = GetFloatBetween(dist1, dist3);
        float td2 = GetFloatBetween(dist3, dist2);
        float td3 = dist3;

        SubdividableTriangle t = new SubdividableTriangle(tv1, tv2, tv3, tn1, tn2, tn3, tc1, tc2, tc3);
        t.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, td1, td2, td3);

        // Lower left
        Vector3 blv1 = v1;
        Vector3 blv2 = GetVertexBetween(v1, v2);
        Vector3 blv3 = GetVertexBetween(v1, v3);

        Vector3 bln1 = n1;
        Vector3 bln2 = GetNormalBetween(n1, n2);
        Vector3 bln3 = GetNormalBetween(n1, n3);

        Color blc1 = c1;
        Color blc2 = GetColorBetween(c1, c2);
        Color blc3 = GetColorBetween(c1, c3);

        float bld1 = dist1;
        float bld2 = GetFloatBetween(dist1, dist2);
        float bld3 = GetFloatBetween(dist1, dist3);

        SubdividableTriangle bl = new SubdividableTriangle(blv1, blv2, blv3, bln1, bln2, bln3, blc1, blc2, blc3);
        bl.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, bld1, bld2, bld3);

        // Lower right
        Vector3 brv1 = GetVertexBetween(v1, v2);
        Vector3 brv2 = v2;
        Vector3 brv3 = GetVertexBetween(v3, v2);

        Vector3 brn1 = GetNormalBetween(n1, n2);
        Vector3 brn2 = n2;
        Vector3 brn3 = GetNormalBetween(n3, n2);

        Color brc1 = GetColorBetween(c1, c2);
        Color brc2 = c2;
        Color brc3 = GetColorBetween(c3, c2);

        float brd1 = GetFloatBetween(dist1, dist2);
        float brd2 = dist2;
        float brd3 = GetFloatBetween(dist3, dist2);

        SubdividableTriangle br = new SubdividableTriangle(brv1, brv2, brv3, brn1, brn2, brn3, brc1, brc2, brc3);
        br.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, brd1, brd2, brd3);

        // Center tri
        Vector3 cv1 = GetVertexBetween(v1, v2);
        Vector3 cv2 = GetVertexBetween(v2, v3);
        Vector3 cv3 = GetVertexBetween(v3, v1);

        Vector3 cn1 = GetNormalBetween(n1, n2);
        Vector3 cn2 = GetNormalBetween(n2, n3);
        Vector3 cn3 = GetNormalBetween(n3, n1);

        Color cc1 = GetColorBetween(c1, c2);
        Color cc2 = GetColorBetween(c2, c3);
        Color cc3 = GetColorBetween(c3, c1);

        float cd1 = GetFloatBetween(dist1, dist2);
        float cd2 = GetFloatBetween(dist2, dist3);
        float cd3 = GetFloatBetween(dist3, dist1);

        SubdividableTriangle c = new SubdividableTriangle(cv1, cv2, cv3, cn1, cn2, cn3, cc1, cc2, cc3);
        c.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, cd1, cd2, cd3);

        if (level + 1 == subdivisionLevelv1 && level + 1 == subdivisionLevelv2 && level + 1 == subdivisionLevelv3)
        {
            tris.Write(t);
            tris.Write(bl);
            tris.Write(br);
            tris.Write(c);
        }
    }
    bool AreTwoVertsOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
    {
        if (level1 == level2 && level3 > level2 && thisLevel + 0 == level2)
            return true;
        if (level2 == level3 && level1 > level3 && thisLevel + 0 == level3)
            return true;
        if (level3 == level1 && level2 > level1 && thisLevel + 0 == level1)
            return true;

        return false;
    }
    bool IsOneVertexOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
    {
        if (level1 == level2 && level3 < level2 && thisLevel + 0 == level3)
            return true;
        if (level2 == level3 && level1 < level3 && thisLevel + 0 == level1)
            return true;
        if (level3 == level1 && level2 < level1 && thisLevel + 0 == level2)
            return true;

        return false;
    }
    bool AllThreeOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
    {
        if (thisLevel == level1 && thisLevel == level2 && thisLevel == level3)
        {
            return true;
        }
        return false;
    }
    public Vector3 GetVertexBetween(in Vector3 v1, in Vector3 v2)
    {
        return (v1 + v2) * 0.5f;
    }
    public Vector3 GetNormalBetween(in Vector3 v1, in Vector3 v2)
    {
        return (v1 + v2) * 0.5f;
    }
    public Color GetColorBetween(in Color v1, in Color v2)
    {
        return (v1 + v2) * 0.5f;
    }
    public float GetFloatBetween(in float v1, in float v2)
    {
        return (v1 + v2) * 0.5f;
    }
}

public class ParallelSubdivision : MonoBehaviour
{
    // Jobs
    SubdivideMeshJob subdivideJob;
    ConstructMeshJob meshJob;
    ReadMeshTriangleDataJob triangleDataReadJob;
    RemoveVertexPairsJob removeVertexPairsJob;

    // Job handles
    JobHandle subdivideJobHandle;
    JobHandle constructMeshJobHandle;
    JobHandle triangleDataReadJobHandle;
    JobHandle removeVertexPairsJobHandle;

    // Precomputed data
    NativeArray<SubdividableTriangle> meshTriangles;    // Do not dispose until OnDisable
    NativeArray<Vector3> vertices;                      // Do not dispose until OnDisable
    NativeArray<Vector3> normals;                       // Do not dispose until OnDisable
    NativeArray<Color> colors;                          // Do not dispose until OnDisable
    NativeArray<int> triangles;                         // Do not dispose until OnDisable

    // Subdivision data
    NativeStream tris;                                  // Dispose after triangle readback
    NativeStream.Writer trisWriter;                     // Is disposed with tris
    NativeStream.Reader trisReader;                     // Is disposed with tris

    // Mesh generation data
    NativeArray<Vector3> newVerts;                      // Dispose after building mesh
    NativeArray<Vector3> newNormals;                    // Dispose after building mesh
    NativeArray<Color> newColors;                       // Dispose after building mesh

    NativeStream newTriangles;                          // Dispose after building mesh
    NativeStream.Writer newTrianglesWriter;             // Disposed with newTriangles
    NativeStream.Reader newTrianglesReader;             // Disposed with newTriangles

    NativeHashMap<Vector3, int> storedVertTris;         // Dispose after triangle readback

    // Triangle readback data
    NativeArray<int> outputTriIndices;                  // Dispose after building mesh

    Mesh mesh;

    int maxSubdivisionLevel = 6;
    float subdivisionRange = 10.0f;

    bool previousIsDone = true;

    public void Start()
    {
        mesh = Instantiate(GetComponent<MeshFilter>().sharedMesh);
        mesh.MarkDynamic();
        this.gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        Initialize();

        //float time = Time.realtimeSinceStartup;
        //DispatchSubdivision();
        //Debug.Log("Elapsed time 1 (ms): " + ((Time.realtimeSinceStartup - time) * 1000.0f));
        //CompleteSubdivision();
        //Debug.Log("Elapsed time 2 (ms): " + ((Time.realtimeSinceStartup - time) * 1000.0f));
        //DispatchVertexPairRemoval();
        //CompleteVertexPairRemoval();
        //
        //DispatchMeshGeneration();
        //DispatchTriangleReadback(trisReader.ComputeItemCount() * 3);
        //
        //CompleteTriangleReadback();
        //
        //
        //BuildMesh();
    }
    void Initialize()
    {
        vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.Persistent);
        normals = new NativeArray<Vector3>(mesh.normals, Allocator.Persistent);
        colors = new NativeArray<Color>(mesh.colors, Allocator.Persistent);
        triangles = new NativeArray<int>(mesh.triangles, Allocator.Persistent);
        CreateTriangles();
    }
    Vector3 GetMousePosInWorld()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100))
        {
            return hit.point;
        }
        else
        {
            return Vector3.zero;
        }
    }
    void CreateTriangles()
    {
        // Num triangles in the mesh
        meshTriangles = new NativeArray<SubdividableTriangle>(triangles.Length / 3, Allocator.Persistent);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int index1 = triangles[i + 0];
            int index2 = triangles[i + 1];
            int index3 = triangles[i + 2];

            Vector3 v1 = vertices[index1];
            Vector3 v2 = vertices[index2];
            Vector3 v3 = vertices[index3];

            Vector3 n1 = normals[index1];
            Vector3 n2 = normals[index2];
            Vector3 n3 = normals[index3];

            Color c1 = Color.black;// colors[index1];
            Color c2 = Color.black;// colors[index2];
            Color c3 = Color.black;// colors[index3];

            SubdividableTriangle tri = new SubdividableTriangle(v1, v2, v3, n1, n2, n3, c1, c2, c3);
            meshTriangles[i / 3] = tri;
        }
    }
    float lastTime = 0;
    

    bool isProcessingSubdivision = false;
    bool isProcessingPairRemoval = false;
    bool isGeneratingMesh = false;
    bool firstRun = true;
    int runs = 0;
    void Update()
    {
        //if (runs > 0) { return; }
        // IMPORTANT
        
        
        // We're done, or running for the first time, so start everything off from step 1
        if (firstRun || (!isProcessingSubdivision && !isProcessingPairRemoval && !isGeneratingMesh))
        {
            DispatchSubdivision();
            isProcessingSubdivision = true;
            firstRun = false;
        }
        if (isProcessingSubdivision && subdivideJobHandle.IsCompleted)
        {
            CompleteSubdivision();
            isProcessingSubdivision = false;

            DispatchVertexPairRemoval();
            isProcessingPairRemoval = true;
        }
        if (isProcessingPairRemoval && removeVertexPairsJobHandle.IsCompleted)
        {
            CompleteVertexPairRemoval();
            isProcessingPairRemoval = false;

            DispatchMeshGeneration();
            DispatchTriangleReadback(trisReader.ComputeItemCount() * 3);
            isGeneratingMesh = true;
        }
        if (isGeneratingMesh && triangleDataReadJobHandle.IsCompleted && constructMeshJobHandle.IsCompleted)
        {
            CompleteTriangleReadback();
            FreePostReadbackResources();
            isGeneratingMesh = false;

            BuildMesh();
            FreePostMeshBuildResources();
        }
        runs++;
        //float time = Time.realtimeSinceStartup;
        //DispatchSubdivision();
        //Debug.Log("Elapsed time 1 (ms): " + ((Time.realtimeSinceStartup - time) * 1000.0f));
        //CompleteSubdivision();
        //Debug.Log("Elapsed time 2 (ms): " + ((Time.realtimeSinceStartup - time) * 1000.0f));
        //DispatchVertexPairRemoval();
        //CompleteVertexPairRemoval();
        //
        //DispatchMeshGeneration();
        //DispatchTriangleReadback(trisReader.ComputeItemCount() * 3);
        //
        //CompleteTriangleReadback();
        //
        //
        //BuildMesh();
    }
    public void DispatchSubdivision()
    {
        Vector3 target = transform.InverseTransformPoint(GetMousePosInWorld());
        int localSubdivisionLevel = maxSubdivisionLevel;
        tris = new NativeStream(meshTriangles.Length, Allocator.Persistent);
        trisWriter = tris.AsWriter();
        trisReader = tris.AsReader();

        subdivideJob = new SubdivideMeshJob()
        {
            meshTriangles = meshTriangles,
            originalVerts = vertices,
            originalNormals = normals,
            originalColors = colors,

            tris = trisWriter,

            maxSubdivisionLevel = localSubdivisionLevel,
            sqrSubdivisionRange = 10,
            target = target
        };

        subdivideJobHandle = subdivideJob.Schedule(meshTriangles.Length, 4);
    }
    public void CompleteSubdivision()
    {
        subdivideJobHandle.Complete();
    }

    void DispatchVertexPairRemoval()
    {
        storedVertTris = new NativeHashMap<Vector3, int>(20000, Allocator.Persistent);

        removeVertexPairsJob = new RemoveVertexPairsJob()
        {
            triReader = trisReader,
            vertices = storedVertTris,
            count = -1,
            foreachCount = trisReader.ForEachCount
        };
        removeVertexPairsJobHandle = removeVertexPairsJob.Schedule();
    }
    void CompleteVertexPairRemoval()
    {
        removeVertexPairsJobHandle.Complete();
    }

    // numTris = number of triangles in total we will be processing
    // we need one stream for EACH triangle to maintain order of addition in threes
    void DispatchMeshGeneration()
    {
        newVerts = new NativeArray<Vector3>(storedVertTris.Length, Allocator.Persistent);
        newNormals = new NativeArray<Vector3>(storedVertTris.Length, Allocator.Persistent);
        newColors = new NativeArray<Color>(storedVertTris.Length, Allocator.Persistent);

        newTriangles = new NativeStream(trisReader.ForEachCount, Allocator.Persistent);

        newTrianglesWriter = newTriangles.AsWriter();

        meshJob = new ConstructMeshJob()
        {
            interlockedCount = -1,
            triArray = trisReader,

            newVerts = this.newVerts,
            newNormals = this.newNormals,
            newColors = this.newColors,
            newTris = this.newTrianglesWriter,

            storedVertTris = this.storedVertTris,
            count = this.trisReader.ForEachCount
        };

        constructMeshJobHandle = meshJob.Schedule(trisReader.ForEachCount, 4);
    }
    
    void CompleteMeshGeneration()
    {
        constructMeshJobHandle.Complete();
    }
    void DispatchTriangleReadback(int count)
    {
        InterlockedCounters.triangleReadbackCounter = -3;
        outputTriIndices = new NativeArray<int>(count, Allocator.TempJob);
        newTrianglesReader = newTriangles.AsReader();
        triangleDataReadJob = new ReadMeshTriangleDataJob()
        {
            newTris = this.newTrianglesReader,
            outputTris = this.outputTriIndices
        };
        triangleDataReadJobHandle = triangleDataReadJob.Schedule(trisReader.ForEachCount, 4, constructMeshJobHandle);
    }
    void CompleteTriangleReadback()
    {
        triangleDataReadJobHandle.Complete();
    }
    void FreePostReadbackResources()
    {
        tris.Dispose();
        storedVertTris.Dispose();
        newTriangles.Dispose();
    }
    void BuildMesh()
    {
        mesh.Clear();

        mesh.SetVertexBufferParams(newVerts.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32));
        mesh.SetVertexBufferData(newVerts, 0, 0, newVerts.Length);

        mesh.SetIndexBufferParams(outputTriIndices.Length, IndexFormat.UInt32);
        mesh.SetIndexBufferData(outputTriIndices, 0, 0, outputTriIndices.Length);

        mesh.SetSubMesh(0, new SubMeshDescriptor(0, outputTriIndices.Length));

        mesh.SetNormals(newNormals);
        mesh.SetColors(newColors);
    }
    void FreePostMeshBuildResources()
    {
        newVerts.Dispose();
        newNormals.Dispose();
        newColors.Dispose();
        outputTriIndices.Dispose();
    }
    void ResetMesh()
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetColors(colors);
    }
    void Cleanup()
    {
        constructMeshJobHandle.Complete();
        subdivideJobHandle.Complete();
        triangleDataReadJobHandle.Complete();
        removeVertexPairsJobHandle.Complete();

        if (vertices.IsCreated) { vertices.Dispose(); }
        if (normals.IsCreated) {  normals.Dispose(); }
        if (colors.IsCreated) { colors.Dispose(); }
        if (triangles.IsCreated) { triangles.Dispose(); }

        if (newVerts.IsCreated) { newVerts.Dispose(); }
        if (newNormals.IsCreated) { newNormals.Dispose(); }
        if (newColors.IsCreated) { newColors.Dispose(); }
        if (outputTriIndices.IsCreated) { outputTriIndices.Dispose(); }

        if (newTriangles.IsCreated) { newTriangles.Dispose(); }
        if (meshTriangles.IsCreated) { meshTriangles.Dispose(); }

        if (storedVertTris.IsCreated) { storedVertTris.Dispose(); }
        if (tris.IsCreated) { tris.Dispose(); }
    }
    void OnDisable()
    {
        Cleanup();
    }
}
