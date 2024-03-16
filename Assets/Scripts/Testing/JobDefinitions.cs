using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct SubdivideMeshJob : IJobParallelFor
{
    public NativeArray<SubdividableTriangle> meshTriangles;
    public NativeArray<Vector3> originalVerts;
    public NativeArray<Vector3> originalNormals;
    public NativeArray<Color> originalColors;

    // subdividableTRIS
    public NativeStream.Writer tris;

    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeHashMap<Vector3, int> funny;
    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeHashMap<Vector3, int>.ParallelWriter funnyWriter;

    public Vector3 target;
    public float sqrSubdivisionRange;
    public int maxSubdivisionLevel;

    // Executes per triangle in the original mesh
    public void Execute(int index)
    {
        tris.BeginForEachIndex(index);

        SubdividableTriangle meshTriangle = meshTriangles[index];

        // Calculate 
        float dist1 = Clamp01(SqrDistance(meshTriangle.v1, target) / sqrSubdivisionRange);
        float dist2 = Clamp01(SqrDistance(meshTriangle.v2, target) / sqrSubdivisionRange);
        float dist3 = Clamp01(SqrDistance(meshTriangle.v3, target) / sqrSubdivisionRange);

        // With reducing distance from center, level starts at maxSubdivisionLevel and goes down
        meshTriangle.Subdivide(ref tris, 0, target, maxSubdivisionLevel, dist1, dist2, dist3);
        tris.EndForEachIndex();

        if (funnyWriter.TryAdd(Vector3.one * index, 4)) { } else { int index2 = funny[Vector3.one * index]; };
    }
    float SqrDistance(Vector3 d1, Vector3 d2)
    {
        float dx = d2.x - d1.x;
        float dy = d2.y - d1.y;
        float dz = d2.z - d1.z;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    float Clamp01(float value)
    {
        if (value > 1)
        {
            value = 1;
            return value;
        }
        else if (value < 0)
        {
            value = 0;
            return value;
        }
        else
        {
            return value;
        }
    }
}
// Read tris back from a NativeStream, and we don't care what order the data is in
[BurstCompile]
public struct ReadTrisJob : IJobParallelFor
{
    [ReadOnly] public NativeStream.Reader inputStream;
    [NativeDisableParallelForRestriction]
    public NativeArray<SubdividableTriangle> resultArray;
    public int arrayIndex;
    public void Execute(int index)
    {
        int index2 = inputStream.BeginForEachIndex(index);
        for (int i = 0; i < index2; i++)
        {
            //int resultIntex = Interlocked.Increment(ref arrayIndex);
            resultArray[Interlocked.Increment(ref arrayIndex)] = inputStream.Read<SubdividableTriangle>();
        }
        
        // Fill the array with SubdividableTriangles - much faster than doing .ToArray() on main
        inputStream.EndForEachIndex();
    }
}

public struct ConstructMeshJob : IJobParallelFor
{
    // Stores our subdivided triangles
    NativeStream.Reader triArray;

    NativeStream.Writer newVerts;
    NativeStream.Writer newNormals;
    NativeStream.Writer newColors;

    NativeStream.Writer newTris;

    // The ~danger zone~
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeHashMap<Vector3, int> storedVertTris;
    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeHashMap<Vector3, int>.ParallelWriter storedVertTrisWriter;

    // Stores the triangle index, shared across threads and incremented using Interlocked
    int interlockedCount;

    public void Execute(int index)
    {
        int itemsInLocalStream = triArray.BeginForEachIndex(index);

        int triIndex1;
        int triIndex2;
        int triIndex3;

        for (int i = 0; i <= itemsInLocalStream; i++)
        {
            SubdividableTriangle tri = triArray.Read<SubdividableTriangle>();

            if (storedVertTrisWriter.TryAdd(tri.v1, interlockedCount + 1)) { Interlocked.Increment(ref interlockedCount); triIndex1 = interlockedCount; newVerts.Write(tri.v1); newNormals.Write(tri.n1); newColors.Write(tri.c1); } else { triIndex1 = storedVertTris[tri.v1]; }
            if (storedVertTrisWriter.TryAdd(tri.v2, interlockedCount + 1)) { Interlocked.Increment(ref interlockedCount); triIndex2 = interlockedCount; newVerts.Write(tri.v2); newNormals.Write(tri.n2); newColors.Write(tri.c2); } else { triIndex2 = storedVertTris[tri.v2]; }
            if (storedVertTrisWriter.TryAdd(tri.v3, interlockedCount + 1)) { Interlocked.Increment(ref interlockedCount); triIndex3 = interlockedCount; newVerts.Write(tri.v3); newNormals.Write(tri.n3); newColors.Write(tri.c3); } else { triIndex3 = storedVertTris[tri.v3]; }

            newTris.Write(triIndex1);
            newTris.Write(triIndex2);
            newTris.Write(triIndex3);
        }
    }
}