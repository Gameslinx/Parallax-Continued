using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct SubdivideMeshJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<SubdividableTriangle> meshTriangles;
    [ReadOnly] public NativeArray<float3> originalVerts;
    [ReadOnly] public NativeArray<float3> originalNormals;
    [ReadOnly] public NativeArray<float4> originalColors;

    // subdividableTRIS
    [WriteOnly] public NativeStream.Writer tris;

    [ReadOnly] public float3 target;
    [ReadOnly] public float sqrSubdivisionRange;
    [ReadOnly] public int maxSubdivisionLevel;

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
    }
    float SqrDistance(float3 d1, float3 d2)
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

// Interlocked add does not work as expected inside a job, it must be referenced from outside

//////////////////////////////
//                          //
//  EXTREMELY IMPORTANT!!!  //
//                          //
//////////////////////////////

// These MUST be reset when a job is NOT executing and accessing it!
// Used to output an arbitrary amount of data stored in a NativeStream to an array for setting mesh data, which only takes in a native array
public static class InterlockedCounters
{
    public static int numParallelSubdivisionComponents = -1;
    public static int[] triangleReadbackCounters = new int[64];
    static InterlockedCounters()
    {
        ResetAllInterlockedCounters();
    }
    static void ResetAllInterlockedCounters()
    {
        for (int i = 0; i < triangleReadbackCounters.Length; i++)
        {
            triangleReadbackCounters[i] = -3;
        }
    }
}

// We need to remove vertex pairs and also assign indices to the vertices
// We can't do this in parallel. Womp womp.
// This is by far the worst performing part of the parallel subdivision :(
// Any help greatly appreciated
[BurstCompile]
public struct RemoveVertexPairsJob : IJob
{
    [ReadOnly] public NativeStream.Reader triReader;
    [WriteOnly] public NativeHashMap<float3, int> vertices;
    [ReadOnly] public int foreachCount;
    public int count;
    public void Execute()
    {
        SubdividableTriangle val;
        // Read the value of the count right now
        for (int index = 0; index < foreachCount; index++)
        {
            int itemsInLocalStream = triReader.BeginForEachIndex(index);

            for (int i = 0; i < itemsInLocalStream; i++)
            {
                val = triReader.Read<SubdividableTriangle>();
                if (vertices.TryAdd(val.v1 * 0.001f, count + 1))
                {
                    count++;
                }
                if (vertices.TryAdd(val.v2 * 0.001f, count + 1))
                {
                    count++;
                }
                if (vertices.TryAdd(val.v3 * 0.001f, count + 1))
                {
                    count++;
                }
            }

            triReader.EndForEachIndex();
        }
    }
}
[BurstCompile]
public struct ConstructMeshJob : IJobParallelFor
{
    // Stores our subdivided triangles
    [ReadOnly] public NativeStream.Reader triArray;

    // See comment in code body for reason
    [NativeDisableContainerSafetyRestriction]
    [WriteOnly] public NativeArray<float3> newVerts;
    [NativeDisableContainerSafetyRestriction]
    [WriteOnly] public NativeArray<float3> newNormals;
    [NativeDisableContainerSafetyRestriction]
    [WriteOnly] public NativeArray<float4> newColors;

    [WriteOnly] public NativeStream.Writer newTris;

    [ReadOnly] public NativeHashMap<float3, int> storedVertTris;

    // Stores the triangle index, shared across threads and incremented using Interlocked
    public int interlockedCount;
    public int count;
    public void Execute(int index)
    {
        int itemsInLocalStream = triArray.BeginForEachIndex(index);
        newTris.BeginForEachIndex(index);

        for (int i = 0; i < itemsInLocalStream; i++)
        {
            SubdividableTriangle tri = triArray.Read<SubdividableTriangle>();

            int index1 = storedVertTris[tri.v1 * 0.001f];
            int index2 = storedVertTris[tri.v2 * 0.001f];
            int index3 = storedVertTris[tri.v3 * 0.001f];

            // Yes, this is technically a race condition since other threads will also be writing here
            // But because they'll be writing the same vertex, this is fine. But we need to disable safety checks for it
            newVerts[index1] = tri.v1;
            newVerts[index2] = tri.v2;
            newVerts[index3] = tri.v3;

            newNormals[index1] = tri.n1;
            newNormals[index2] = tri.n2;
            newNormals[index3] = tri.n3;

            newColors[index1] = tri.c1;
            newColors[index2] = tri.c2;
            newColors[index3] = tri.c3;

            newTris.Write(index1);
            newTris.Write(index2);
            newTris.Write(index3);
        }

        newTris.EndForEachIndex();
        triArray.EndForEachIndex();
    }
}

// We can't burst compile this code as it accesses an external non-readonly static field, which is a managed type
// Either we use an IJobParallelFor or we burst compile as an IJob without the counter
public struct ReadMeshTriangleDataJob : IJobParallelFor
{
    [ReadOnly] public NativeStream.Reader newTris;
    [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<int> outputTris;
    [ReadOnly] public int uniqueIndex;
    public void Execute(int index)
    {
        int itemsInLocalStream = newTris.BeginForEachIndex(index);

        // We must read back triangles in threes
        for (int i = 0; i < itemsInLocalStream; i += 3)
        {
            // Compute output array index
            int zone = Interlocked.Add(ref InterlockedCounters.triangleReadbackCounters[uniqueIndex], 3);
            outputTris[zone] = newTris.Read<int>();
            outputTris[zone + 1] = newTris.Read<int>();
            outputTris[zone + 2] = newTris.Read<int>();
        }

        newTris.EndForEachIndex();
    }
}