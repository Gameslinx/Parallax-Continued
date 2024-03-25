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
    // Added as a component to the new terrain gameobject created in TerrainShaderQuadData
    public class JobifiedSubdivision : MonoBehaviour
    {
        // Unique identifier index for interlocked counter
        int uniqueIdentifier = -1;

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
        NativeArray<SubdividableTriangle> meshTriangles;        // Do not dispose until OnDisable
        NativeArray<float3> vertices;                           // Do not dispose until OnDisable
        NativeArray<float3> normals;                            // Do not dispose until OnDisable
        NativeArray<float4> colors;                             // Do not dispose until OnDisable
        NativeArray<int> triangles;                             // Do not dispose until OnDisable

        // Subdivision data
        NativeStream tris;                                      // Dispose after triangle readback
        NativeStream.Writer trisWriter;                         // Is disposed with tris
        NativeStream.Reader trisReader;                         // Is disposed with tris

        // Temp array for camera frustum planes
        NativeArray<ParallaxPlane> frustumPlanes;               // Disposed on disable

        // Mesh generation data
        NativeArray<float3> newVerts;                           // Dispose after building mesh
        NativeArray<float3> newNormals;                         // Dispose after building mesh
        NativeArray<float4> newColors;                          // Dispose after building mesh

        NativeStream newTriangles;                              // Dispose after building mesh
        NativeStream.Writer newTrianglesWriter;                 // Disposed with newTriangles
        NativeStream.Reader newTrianglesReader;                 // Disposed with newTriangles

        NativeHashMap<float3, int> storedVertTris;              // Dispose after triangle readback

        // Triangle readback data
        NativeArray<int> outputTriIndices;                      // Dispose after building mesh

        public Mesh mesh;

        int streamForeachCount = 0;

        public int maxSubdivisionLevel = 7;
        public float subdivisionRange = 50.0f;

        public void Start()
        {
            mesh = Instantiate(GetComponent<MeshFilter>().sharedMesh);
            mesh.MarkDynamic();
            this.gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;

            GetUniqueIdentifier();
            Initialize();
        }
        void GetUniqueIdentifier()
        {
            uniqueIdentifier = InterlockedCounters.Request();
        }
        void Initialize()
        {
            vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.Persistent).Reinterpret<float3>();
            normals = new NativeArray<Vector3>(mesh.normals, Allocator.Persistent).Reinterpret<float3>();
            colors = new NativeArray<Color>(mesh.colors, Allocator.Persistent).Reinterpret<float4>();

            Color[] colorArray = mesh.colors;
            for(int i = 0; i < colors.Length; i++)
            {
                colors[i] = new float4(colorArray[i].r, colorArray[i].g, colorArray[i].b, colorArray[i].a);
            }

            triangles = new NativeArray<int>(mesh.triangles, Allocator.Persistent);

            storedVertTris = new NativeHashMap<float3, int>(3500, Allocator.Persistent);

            CreateTriangles();

            frustumPlanes = new NativeArray<ParallaxPlane>(6, Allocator.Persistent);
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

        bool isProcessingSubdivision = false;
        bool isGeneratingMesh = false;
        bool firstRun = true;
        void Update()
        {
            //if (!FlightGlobals.ready) { return; }
            // We're done, or running for the first time, so start everything off from step 1
            if (firstRun || (!isProcessingSubdivision && !isGeneratingMesh))
            {
                DispatchSubdivision();
                DispatchVertexPairRemoval();
                isProcessingSubdivision = true;
                firstRun = false;
            }
            if (isProcessingSubdivision && removeVertexPairsJobHandle.IsCompleted && subdivideJobHandle.IsCompleted)
            {
                CompleteVertexPairRemoval();
                isProcessingSubdivision = false;

                DispatchMeshGeneration();
                DispatchTriangleReadback(trisReader.ComputeItemCount() * 3);

                isGeneratingMesh = true;
            }
            if (isGeneratingMesh && triangleDataReadJobHandle.IsCompleted && constructMeshJobHandle.IsCompleted)
            {
                CompleteTriangleReadback();

                isGeneratingMesh = false;

                BuildMesh();
                FreePostMeshBuildResources();
                FreePostReadbackResources();
            }
        }
        public void DispatchSubdivision()
        {
            float3 target = RuntimeOperations.cameraPos;

            // Get the camera frustum planes
            frustumPlanes.CopyFrom(RuntimeOperations.cameraFrustumPlanes);
            tris = new NativeStream(this.streamForeachCount, Allocator.Persistent);
            trisWriter = tris.AsWriter();
            trisReader = tris.AsReader();
            subdivideJob = new SubdivideMeshJob()
            {
                meshTriangles = meshTriangles,
                originalVerts = vertices,
                originalNormals = normals,
                originalColors = colors,

                tris = trisWriter,

                maxSubdivisionLevel = this.maxSubdivisionLevel,
                sqrSubdivisionRange = this.subdivisionRange,
                cameraFrustumPlanes = frustumPlanes,
                objectToWorldMatrix = transform.localToWorldMatrix,

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
            storedVertTris.Clear();

            removeVertexPairsJob = new RemoveVertexPairsJob()
            {
                triReader = trisReader,
                vertices = storedVertTris,
                count = -1,
                foreachCount = this.streamForeachCount
            };
            removeVertexPairsJobHandle = removeVertexPairsJob.Schedule(subdivideJobHandle);
        }
        void CompleteVertexPairRemoval()
        {
            removeVertexPairsJobHandle.Complete();
        }

        // numTris = number of triangles in total we will be processing
        // we need one stream for EACH triangle to maintain order of addition in threes
        void DispatchMeshGeneration()
        {
            newVerts = new NativeArray<float3>(storedVertTris.Length, Allocator.Persistent);
            newNormals = new NativeArray<float3>(storedVertTris.Length, Allocator.Persistent);
            newColors = new NativeArray<float4>(storedVertTris.Length, Allocator.Persistent);

            newTriangles = new NativeStream(meshTriangles.Length, Allocator.Persistent);
            newTrianglesWriter = newTriangles.AsWriter();
            newTrianglesReader = newTriangles.AsReader();

            meshJob = new ConstructMeshJob()
            {
                interlockedCount = -1,
                triArray = trisReader,

                newVerts = this.newVerts,
                newNormals = this.newNormals,
                newColors = this.newColors,
                newTris = this.newTrianglesWriter,

                storedVertTris = this.storedVertTris,
                count = this.streamForeachCount
            };

            constructMeshJobHandle = meshJob.Schedule(this.streamForeachCount, 4);
        }
        void DispatchTriangleReadback(int count)
        {
            InterlockedCounters.ResetCounter(uniqueIdentifier);
            outputTriIndices = new NativeArray<int>(count, Allocator.Persistent);

            triangleDataReadJob = new ReadMeshTriangleDataJob()
            {
                newTris = this.newTrianglesReader,
                outputTris = this.outputTriIndices,
                uniqueIndex = uniqueIdentifier
            };
            triangleDataReadJobHandle = triangleDataReadJob.Schedule(this.streamForeachCount, 4, constructMeshJobHandle);
        }
        void CompleteTriangleReadback()
        {
            triangleDataReadJobHandle.Complete();
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
            this.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        void FreePostMeshBuildResources()
        {
            newVerts.Dispose();
            newNormals.Dispose();
            newColors.Dispose();
            outputTriIndices.Dispose();
            tris.Dispose();
        }
        void FreePostReadbackResources()
        {
            newTriangles.Dispose();
        }
        void ResetMesh()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles.ToArray(), 0);
            mesh.SetColors(colors);
        }
        public void Cleanup()
        {
            constructMeshJobHandle.Complete();
            subdivideJobHandle.Complete();
            triangleDataReadJobHandle.Complete();
            removeVertexPairsJobHandle.Complete();

            if (vertices.IsCreated) { vertices.Dispose(); }
            if (normals.IsCreated) { normals.Dispose(); }
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

            if (frustumPlanes.IsCreated) { frustumPlanes.Dispose(); }

            InterlockedCounters.Return(uniqueIdentifier);
        }
        void OnDisable()
        {
            Cleanup();
        }
    }
}
