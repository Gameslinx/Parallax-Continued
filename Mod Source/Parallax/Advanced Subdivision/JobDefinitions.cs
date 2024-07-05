using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using System.Diagnostics;

namespace Parallax
{
    /////////////////////
    //    UTILITIES    //
    /////////////////////

    // Interlocked add does not work as expected inside a job since an instance of the job struct is made for each thread, it must be referenced from outside

    //////////////////////////////
    //                          //
    //  EXTREMELY IMPORTANT!!!  //
    //                          //
    //////////////////////////////

    // These MUST be reset when a job is NOT executing and accessing it!
    // Used to output an arbitrary amount of data stored in a NativeStream to an array for setting mesh data, which only takes in a native array

    public static class InterlockedCounters
    {
        // Create a queue of unique identifiers (0 to 15)
        // And use the identifier to access a unique counter for a specific quad
        // To prevent collisions between multiple quads performing the subdivide jobs (specifically the readback job)
        public static int[] triangleReadbackCounters = new int[16384];
        public static Queue<int> uniqueQuadIdentifiers = new Queue<int>();
        static InterlockedCounters()
        {
            ResetAllInterlockedCounters();
        }
        static void ResetAllInterlockedCounters()
        {
            uniqueQuadIdentifiers.Clear();
            for (int i = 0; i < 16384; i++)
            {
                // MUST start the counter at -3, because triangles are read back in threes
                // and add 3 to the counter at the start of each iteration, bringing it to 0 initially
                triangleReadbackCounters[i] = -3;
                uniqueQuadIdentifiers.Enqueue(i);
            }
        }
        public static void ResetCounter(int uniqueIdentifier)
        {
            triangleReadbackCounters[uniqueIdentifier] = -3;
        }
        public static void Return(int counter)
        {
            UnityEngine.Debug.Log("Returning identifier: " + counter);
            triangleReadbackCounters[counter] = -3;
            uniqueQuadIdentifiers.Enqueue(counter);
        }
        public static int Request()
        {
            // We can't really guard against this from happening, so we'll return a unique identifier of 0 and let the jobs clash
            // While KSP won't crash, the meshes will completely scramble
            // Edit: Crashes KSP
            // This error should typically never happen

            if (uniqueQuadIdentifiers.Count - 1 == 0)
            {
                ParallaxDebug.LogError("CATASTROPHIC EXCEPTION: The unique quad identifier queue is empty - subdivision jobs cannot continue. If you see this in your log file, too many quads are trying to subdivide! (Max 16)");
                return 0;
            }
            return uniqueQuadIdentifiers.Dequeue();
        }
    }

    ////////////////
    //    JOBS    //
    ////////////////


    // Subdivide mesh into smaller triangles - edge midpoint subdivision
    [BurstCompile]
    public struct SubdivideMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SubdividableTriangle> meshTriangles;
        [ReadOnly] public NativeArray<float3> originalVerts;
        [ReadOnly] public NativeArray<float3> originalNormals;
        [ReadOnly] public NativeArray<float4> originalColors;

        // Subdividable TRIS
        [WriteOnly] public NativeStream.Writer tris;

        [ReadOnly] public float3 target;
        [ReadOnly] public float sqrSubdivisionRange;
        [ReadOnly] public int maxSubdivisionLevel;

        [ReadOnly] public NativeArray<ParallaxPlane> cameraFrustumPlanes;
        [ReadOnly] public float4x4 objectToWorldMatrix;

        // Executes per triangle in the original mesh
        public void Execute(int index)
        {
            tris.BeginForEachIndex(index);

            int numInside = 0;

            // Fetch triangle
            SubdividableTriangle meshTriangle = meshTriangles[index];

            // Fetch world space points for frustum culling
            float4 worldSpaceV1 = math.mul(objectToWorldMatrix, new float4(meshTriangle.v1, 1));
            float4 worldSpaceV2 = math.mul(objectToWorldMatrix, new float4(meshTriangle.v2, 1));
            float4 worldSpaceV3 = math.mul(objectToWorldMatrix, new float4(meshTriangle.v3, 1));
            float centerDist = SqrDistance(target, (worldSpaceV1.xyz + worldSpaceV2.xyz + worldSpaceV3.xyz) * 0.333f);

            // Make sure all points are within the frustum
            for (int i = 0; i < 6; i++)
            {
                ParallaxPlane plane = cameraFrustumPlanes[i];
                // 75 is arbitrary - merely, if the triangle center dist is less than 75m 
                if (centerDist < 75 || plane.GetSide(worldSpaceV1.xyz) || plane.GetSide(worldSpaceV2.xyz) || plane.GetSide(worldSpaceV3.xyz))
                {
                    numInside++;
                }
            }

            if (numInside == 6)
            {
                // With reducing distance from center, level starts at maxSubdivisionLevel and goes down
                meshTriangle.Subdivide(ref tris, 0, target, maxSubdivisionLevel, sqrSubdivisionRange, objectToWorldMatrix);
            }
            else
            {
                tris.Write(meshTriangle);
            }
            tris.EndForEachIndex();

        }
        float SqrDistance(in float3 d1, in float3 d2)
        {
            float dx = d2.x - d1.x;
            float dy = d2.y - d1.y;
            float dz = d2.z - d1.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
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

        // This version of Burst contains an error that will throw exceptions 50% of the time
        // The code is not wrong, but we want to hide the exception from the log because it's big and not indicative of an actual failure
        // Error is always on Line 200
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
                    if (vertices.TryAdd(val.v1 * 0.001f, count))
                    {
                        count++;
                    }
                    if (vertices.TryAdd(val.v2 * 0.001f, count))
                    {
                        count++;
                    }
                    if (vertices.TryAdd(val.v3 * 0.001f, count))
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
        [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<int> outputTris;
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
}
