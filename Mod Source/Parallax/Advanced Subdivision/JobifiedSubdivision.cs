using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;

namespace Parallax
{
    // Created by TerrainShaderQuadData and updated each frame from RuntimeOperations
    public class JobifiedSubdivision
    {
        public static readonly List<JobifiedSubdivision> activeSubdivisions = new List<JobifiedSubdivision>();

        const float frustumCullBypassRange = 75.0f;
        const float sqrRedispatchDistance = 0.25f * 0.25f;
        const float redispatchPlaneAngleCos = 0.99985f;
        const MeshUpdateFlags meshUpdateFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        static readonly VertexAttributeDescriptor[] vertexLayout = new VertexAttributeDescriptor[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 2)
        };

        JobHandle subdivisionJobHandle;

        // Precomputed data
        NativeArray<SubdividableTriangle> meshTriangles;        // Do not dispose until Cleanup
        NativeArray<float3> vertices;                           // Do not dispose until Cleanup
        NativeArray<float3> normals;                            // Do not dispose until Cleanup
        NativeArray<float4> colors;                             // Do not dispose until Cleanup
        NativeArray<int> triangles;                             // Do not dispose until Cleanup
        NativeArray<int> triOffsets;                            // Do not dispose until Cleanup

        // Temp array for camera frustum planes
        NativeArray<ParallaxPlane> frustumPlanes;               // Disposed on cleanup
        NativeArray<float> sqrLevelThresholds;                  // Disposed on cleanup

        // Mesh generation data
        NativeList<float3> newVerts;                            // Disposed on cleanup
        NativeList<float3> newNormals;                          // Disposed on cleanup
        NativeList<float4> newColors;                           // Disposed on cleanup
        NativeList<int> newTriangles;                           // Disposed on cleanup
        NativeList<ushort> newShortTriangles;                   // Disposed on cleanup

        NativeHashMap<float3, int> storedVertTris;              // Disposed on cleanup

        readonly ParallaxPlane[] localFrustumPlanes = new ParallaxPlane[6];
        readonly float3[] lastLocalPlaneNormals = new float3[6];
        float3 lastLocalTarget;
        int lastMaxSubdivisionLevel;
        float lastSubdivisionRange;

        PQ quad;
        Mesh mesh;
        Bounds quadBounds;

        int streamForeachCount = 0;

        int vertexCapacity = 0;
        int indexCapacity = 0;
        IndexFormat indexFormat = IndexFormat.UInt32;

        public int maxSubdivisionLevel = 7;
        public float subdivisionRange = 50.0f;
        bool isActive = false;

        public JobifiedSubdivision(PQ quad, Mesh mesh, int maxSubdivisionLevel, float subdivisionRange)
        {
            this.quad = quad;
            this.mesh = mesh;
            this.maxSubdivisionLevel = maxSubdivisionLevel;
            this.subdivisionRange = subdivisionRange;

            mesh.MarkDynamic();

            Initialize();
            ResetMesh();

            activeSubdivisions.Add(this);
        }
        public static void UpdateAll()
        {
            for (int i = 0; i < activeSubdivisions.Count; i++)
            {
                activeSubdivisions[i].Update();
            }
            JobHandle.ScheduleBatchedJobs();
        }
        void Initialize()
        {
            InitializeSourceData();

            storedVertTris = new NativeHashMap<float3, int>(3500, Allocator.Persistent);
            frustumPlanes = new NativeArray<ParallaxPlane>(6, Allocator.Persistent);

            newVerts = new NativeList<float3>(Allocator.Persistent);
            newNormals = new NativeList<float3>(Allocator.Persistent);
            newColors = new NativeList<float4>(Allocator.Persistent);
            newTriangles = new NativeList<int>(Allocator.Persistent);
            newShortTriangles = new NativeList<ushort>(Allocator.Persistent);

            isActive = true;
        }
        void InitializeSourceData()
        {
            Mesh quadMesh = quad.mesh;
            quadBounds = quadMesh.bounds;

            vertices = new NativeArray<Vector3>(quadMesh.vertices, Allocator.Persistent).Reinterpret<float3>();
            normals = new NativeArray<Vector3>(quadMesh.normals, Allocator.Persistent).Reinterpret<float3>();

            Color[] colorArray = quadMesh.colors;
            colors = new NativeArray<Color>(colorArray, Allocator.Persistent).Reinterpret<float4>();
            for(int i = 0; i < colors.Length; i++)
            {
                colors[i] = new float4(colorArray[i].r, colorArray[i].g, colorArray[i].b, colorArray[i].a);
            }

            triangles = new NativeArray<int>(quadMesh.triangles, Allocator.Persistent);

            CreateTriangles();

            triOffsets = new NativeArray<int>(streamForeachCount, Allocator.Persistent);
        }
        void DisposeSourceData()
        {
            if (vertices.IsCreated) { vertices.Dispose(); }
            if (normals.IsCreated) { normals.Dispose(); }
            if (colors.IsCreated) { colors.Dispose(); }
            if (triangles.IsCreated) { triangles.Dispose(); }
            if (meshTriangles.IsCreated) { meshTriangles.Dispose(); }
            if (triOffsets.IsCreated) { triOffsets.Dispose(); }
        }
        public void RefreshSourceData()
        {
            CompleteAllJobs();
            DisposeSourceData();
            InitializeSourceData();

            isSubdividing = false;
            forceDispatch = true;
        }
        void CreateTriangles()
        {
            // Num triangles in the mesh
            meshTriangles = new NativeArray<SubdividableTriangle>(triangles.Length / 3, Allocator.Persistent);
            streamForeachCount = triangles.Length / 3;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int index1 = triangles[i + 0];
                int index2 = triangles[i + 1];
                int index3 = triangles[i + 2];

                float3 v1 = vertices[index1];
                float3 v2 = vertices[index2];
                float3 v3 = vertices[index3];

                float3 n1 = normals[index1];
                float3 n2 = normals[index2];
                float3 n3 = normals[index3];

                float4 c1 = colors[index1];
                float4 c2 = colors[index2];
                float4 c3 = colors[index3];

                SubdividableTriangle tri = new SubdividableTriangle(v1, v2, v3, n1, n2, n3, c1, c2, c3);
                meshTriangles[i / 3] = tri;
            }
        }

        bool isSubdividing = false;
        bool forceDispatch = true;
        void Update()
        {
            if (isSubdividing)
            {
                if (!subdivisionJobHandle.IsCompleted)
                {
                    return;
                }

                subdivisionJobHandle.Complete();
                isSubdividing = false;

                BuildMesh();
            }

            float4x4 localToWorld = quad.transform.localToWorldMatrix;
            float scale = math.length(localToWorld.c0.xyz);
            float3 localTarget = math.transform(quad.transform.worldToLocalMatrix, RuntimeOperations.cameraPos);
            UpdateLocalFrustumPlanes(localToWorld);

            if (RequiresDispatch(localTarget, scale))
            {
                DispatchSubdivision(localTarget, scale);
            }
        }
        void UpdateLocalFrustumPlanes(in float4x4 localToWorld)
        {
            float4x4 transposedLocalToWorld = math.transpose(localToWorld);
            for (int i = 0; i < 6; i++)
            {
                ParallaxPlane plane = RuntimeOperations.cameraFrustumPlanes[i];
                float4 localPlane = math.mul(transposedLocalToWorld, new float4(plane.normal, plane.distance));
                localFrustumPlanes[i] = new ParallaxPlane(localPlane.xyz, localPlane.w);
            }
        }
        bool RequiresDispatch(in float3 localTarget, float scale)
        {
            if (forceDispatch || maxSubdivisionLevel != lastMaxSubdivisionLevel || subdivisionRange != lastSubdivisionRange)
            {
                return true;
            }
            if (math.distancesq(localTarget, lastLocalTarget) * scale * scale > sqrRedispatchDistance)
            {
                return true;
            }
            for (int i = 0; i < 6; i++)
            {
                if (math.dot(math.normalizesafe(localFrustumPlanes[i].normal), lastLocalPlaneNormals[i]) < redispatchPlaneAngleCos)
                {
                    return true;
                }
            }
            return false;
        }
        void UpdateLevelThresholds(float scale)
        {
            int thresholdCount = math.max(maxSubdivisionLevel, 1);
            if (sqrLevelThresholds.Length != thresholdCount)
            {
                if (sqrLevelThresholds.IsCreated) { sqrLevelThresholds.Dispose(); }
                sqrLevelThresholds = new NativeArray<float>(thresholdCount, Allocator.Persistent);
            }

            for (int i = 0; i < maxSubdivisionLevel; i++)
            {
                float exponent = math.pow(1.0f - (float)(i + 1) / maxSubdivisionLevel, 0.625f);
                float threshold = (math.pow(subdivisionRange, exponent) - 0.708f) / scale;
                sqrLevelThresholds[i] = threshold > 0 ? threshold * threshold : -1.0f;
            }
        }
        void DispatchSubdivision(in float3 localTarget, float scale)
        {
            forceDispatch = false;
            lastLocalTarget = localTarget;
            lastMaxSubdivisionLevel = maxSubdivisionLevel;
            lastSubdivisionRange = subdivisionRange;
            for (int i = 0; i < 6; i++)
            {
                lastLocalPlaneNormals[i] = math.normalizesafe(localFrustumPlanes[i].normal);
            }

            frustumPlanes.CopyFrom(localFrustumPlanes);
            UpdateLevelThresholds(scale);

            float localFrustumCullBypassRange = frustumCullBypassRange / scale;

            NativeStream tris = new NativeStream(streamForeachCount, Allocator.Persistent);

            SubdivideMeshJob subdivideJob = new SubdivideMeshJob()
            {
                meshTriangles = meshTriangles,
                tris = tris.AsWriter(),

                target = localTarget,
                sqrFrustumCullBypassRange = localFrustumCullBypassRange * localFrustumCullBypassRange,
                maxSubdivisionLevel = maxSubdivisionLevel,
                sqrLevelThresholds = sqrLevelThresholds,
                cameraFrustumPlanes = frustumPlanes
            };

            RemoveVertexPairsJob removeVertexPairsJob = new RemoveVertexPairsJob()
            {
                triReader = tris.AsReader(),
                vertices = storedVertTris,
                triOffsets = triOffsets,
                foreachCount = streamForeachCount,

                newVerts = newVerts,
                newNormals = newNormals,
                newColors = newColors,
                newTris = newTriangles,
                newShortTris = newShortTriangles
            };

            ConstructMeshJob meshJob = new ConstructMeshJob()
            {
                triArray = tris.AsReader(),

                newTris = newTriangles.AsDeferredJobArray(),
                newShortTris = newShortTriangles.AsDeferredJobArray(),

                triOffsets = triOffsets,
                storedVertTris = storedVertTris
            };

            JobHandle subdivideJobHandle = subdivideJob.Schedule(streamForeachCount, 4);
            JobHandle removeVertexPairsJobHandle = removeVertexPairsJob.Schedule(subdivideJobHandle);
            JobHandle meshJobHandle = meshJob.Schedule(streamForeachCount, 4, removeVertexPairsJobHandle);
            subdivisionJobHandle = tris.Dispose(meshJobHandle);

            isSubdividing = true;
        }
        void BuildMesh()
        {
            NativeArray<float3> verts = newVerts.AsArray();
            SetVertexData(verts, newNormals.AsArray(), newColors.AsArray());
            if (newShortTriangles.Length > 0)
            {
                SetIndexData(newShortTriangles.AsArray(), IndexFormat.UInt16, verts.Length);
            }
            else
            {
                SetIndexData(newTriangles.AsArray(), IndexFormat.UInt32, verts.Length);
            }

            mesh.bounds = quadBounds;
        }
        void ResetMesh()
        {
            mesh.Clear();
            vertexCapacity = 0;
            indexCapacity = 0;

            SetVertexData(vertices, normals, colors);
            SetIndexData(triangles, IndexFormat.UInt32, vertices.Length);

            mesh.bounds = quadBounds;
        }
        void SetVertexData(in NativeArray<float3> verts, in NativeArray<float3> vertNormals, in NativeArray<float4> vertColors)
        {
            if (verts.Length > vertexCapacity)
            {
                vertexCapacity = math.ceilpow2(verts.Length);
                mesh.SetVertexBufferParams(vertexCapacity, vertexLayout);
            }
            mesh.SetVertexBufferData(verts, 0, 0, verts.Length, 0, meshUpdateFlags);
            mesh.SetVertexBufferData(vertNormals, 0, 0, vertNormals.Length, 1, meshUpdateFlags);
            mesh.SetVertexBufferData(vertColors, 0, 0, vertColors.Length, 2, meshUpdateFlags);
        }
        void SetIndexData<T>(in NativeArray<T> indices, IndexFormat format, int vertexCount) where T : struct
        {
            if (indices.Length > indexCapacity || format != indexFormat)
            {
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), meshUpdateFlags);
                indexCapacity = math.max(indexCapacity, math.ceilpow2(indices.Length));
                indexFormat = format;
                mesh.SetIndexBufferParams(indexCapacity, format);
            }
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, meshUpdateFlags);

            SubMeshDescriptor subMesh = new SubMeshDescriptor(0, indices.Length)
            {
                firstVertex = 0,
                vertexCount = vertexCount,
                bounds = quadBounds
            };
            mesh.SetSubMesh(0, subMesh, meshUpdateFlags);
        }
        void CompleteAllJobs()
        {
            subdivisionJobHandle.Complete();
        }
        public void Cleanup()
        {
            if (!isActive)
            {
                return;
            }

            CompleteAllJobs();
            DisposeSourceData();

            if (storedVertTris.IsCreated) { storedVertTris.Dispose(); }
            if (frustumPlanes.IsCreated) { frustumPlanes.Dispose(); }
            if (sqrLevelThresholds.IsCreated) { sqrLevelThresholds.Dispose(); }

            if (newVerts.IsCreated) { newVerts.Dispose(); }
            if (newNormals.IsCreated) { newNormals.Dispose(); }
            if (newColors.IsCreated) { newColors.Dispose(); }
            if (newTriangles.IsCreated) { newTriangles.Dispose(); }
            if (newShortTriangles.IsCreated) { newShortTriangles.Dispose(); }

            isActive = false;

            activeSubdivisions.Remove(this);
        }
    }
}
