using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Burst.CompilerServices;

namespace Parallax
{
    ////////////////
    //    JOBS    //
    ////////////////


    // Subdivide mesh into smaller triangles - edge midpoint subdivision
    [BurstCompile]
    public struct SubdivideMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SubdividableTriangle> meshTriangles;

        // Subdividable TRIS
        [WriteOnly] public NativeStream.Writer tris;

        [ReadOnly] public float3 target;
        [ReadOnly] public float sqrFrustumCullBypassRange;
        [ReadOnly] public int maxSubdivisionLevel;
        [ReadOnly] public NativeArray<float> sqrLevelThresholds;

        [ReadOnly] public NativeArray<ParallaxPlane> cameraFrustumPlanes;

        // Executes per triangle in the original mesh
        public void Execute(int index)
        {
            tris.BeginForEachIndex(index);

            // Fetch triangle
            SubdividableTriangle meshTriangle = meshTriangles[index];

            bool isVisible = math.distancesq(target, (meshTriangle.v1 + meshTriangle.v2 + meshTriangle.v3) * 0.333f) < sqrFrustumCullBypassRange;
            if (!isVisible)
            {
                isVisible = true;
                for (int i = 0; i < 6; i++)
                {
                    ParallaxPlane plane = cameraFrustumPlanes[i];
                    if (!(plane.GetSide(meshTriangle.v1) || plane.GetSide(meshTriangle.v2) || plane.GetSide(meshTriangle.v3)))
                    {
                        isVisible = false;
                        break;
                    }
                }
            }

            if (isVisible)
            {
                // With reducing distance from center, level starts at maxSubdivisionLevel and goes down
                meshTriangle.Subdivide(ref tris, 0, target, maxSubdivisionLevel, sqrLevelThresholds);
            }
            else
            {
                tris.Write(meshTriangle);
            }
            tris.EndForEachIndex();
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
        public NativeHashMap<float3, int> vertices;
        [WriteOnly] public NativeArray<int> triOffsets;
        [ReadOnly] public int foreachCount;

        public NativeList<float3> newVerts;
        public NativeList<float3> newNormals;
        public NativeList<float4> newColors;
        public NativeList<int> newTris;
        public NativeList<ushort> newShortTris;

        // This version of Burst contains an error that will throw exceptions 50% of the time
        // The code is not wrong, but we want to hide the exception from the log because it's big and not indicative of an actual failure
        // Error is always on Line 200
        public void Execute()
        {
            vertices.Clear();
            newVerts.Clear();
            newNormals.Clear();
            newColors.Clear();

            SubdividableTriangle val;
            int triCount = 0;
            for (int index = 0; index < foreachCount; index++)
            {
                int itemsInLocalStream = triReader.BeginForEachIndex(index);
                triOffsets[index] = triCount;
                triCount += itemsInLocalStream;

                for (int i = 0; i < itemsInLocalStream; i++)
                {
                    val = triReader.Read<SubdividableTriangle>();
                    TryAddVertex(val.v1, val.n1, val.c1);
                    TryAddVertex(val.v2, val.n2, val.c2);
                    TryAddVertex(val.v3, val.n3, val.c3);
                }

                triReader.EndForEachIndex();
            }

            if (newVerts.Length <= 65536)
            {
                newShortTris.ResizeUninitialized(triCount * 3);
                newTris.Clear();
            }
            else
            {
                newTris.ResizeUninitialized(triCount * 3);
                newShortTris.Clear();
            }
        }
        void TryAddVertex(in float3 vertex, in float3 normal, in float4 color)
        {
            if (vertices.TryAdd(vertex, newVerts.Length))
            {
                newVerts.Add(vertex);
                newNormals.Add(normal);
                newColors.Add(color);
            }
        }
    }
    [BurstCompile]
    public struct ConstructMeshJob : IJobParallelFor
    {
        // Stores our subdivided triangles
        [ReadOnly] public NativeStream.Reader triArray;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<int> newTris;
        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<ushort> newShortTris;

        [ReadOnly] public NativeArray<int> triOffsets;
        [ReadOnly] public NativeHashMap<float3, int> storedVertTris;

        public void Execute(int index)
        {
            int itemsInLocalStream = triArray.BeginForEachIndex(index);
            int triIndex = triOffsets[index] * 3;
            bool useShortIndices = newShortTris.Length > 0;

            for (int i = 0; i < itemsInLocalStream; i++)
            {
                SubdividableTriangle tri = triArray.Read<SubdividableTriangle>();

                int index1 = storedVertTris[tri.v1];
                int index2 = storedVertTris[tri.v2];
                int index3 = storedVertTris[tri.v3];

                if (useShortIndices)
                {
                    newShortTris[triIndex] = (ushort)index1;
                    newShortTris[triIndex + 1] = (ushort)index2;
                    newShortTris[triIndex + 2] = (ushort)index3;
                }
                else
                {
                    newTris[triIndex] = index1;
                    newTris[triIndex + 1] = index2;
                    newTris[triIndex + 2] = index3;
                }
                triIndex += 3;
            }

            triArray.EndForEachIndex();
        }
    }
}
