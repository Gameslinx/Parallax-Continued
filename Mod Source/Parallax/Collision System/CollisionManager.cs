using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Kopernicus.ConfigParser.ParserOptions;

namespace Parallax
{
    public class ScatterColliderData : FastListItem
    {
        // The quad this data belongs to
        public ScatterSystemQuadData scatterSystemQuad;

        // Data from the GPU
        public readonly NativeArray<PositionData> quadLocalData;

        // Job
        public NativeArray<float> lastDistances;
        public JobHandle initDistancesHandle;

        // Pointer to Scatter in collideableScatters
        public int collideableScattersIndex;
        public int dataCount;
        public ScatterColliderData(ScatterSystemQuadData scatterSystemQuad, NativeArray<PositionData> quadLocalData, int collideableScattersIndex)
        {
            this.scatterSystemQuad = scatterSystemQuad;
            this.quadLocalData = quadLocalData;
            this.collideableScattersIndex = collideableScattersIndex;
            this.dataCount = quadLocalData.Length;
            InitializeDistances();
        }
        // Sets all distances to max value
        public void InitializeDistances()
        {
            lastDistances = new NativeArray<float>(dataCount, Allocator.Persistent);
            InitalizeArrayJob initJob = new InitalizeArrayJob
            {
                array = lastDistances,
                initializeTo = float.MaxValue
            };
            initDistancesHandle = initJob.Schedule(dataCount, dataCount / 8);
        }
    }
    /// <summary>
    /// Manages data for all scatters and creates the colliders for them
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class CollisionManager : MonoBehaviour
    {
        public static CollisionManager Instance;
        public static ParallaxScatterBody currentScatterBody;
        public static int numCollideableScatters = 0;

        // O(1) insertion and deletion makes managing large amounts of data much easier
        // Iterated through often to determine which colliders need spawning
        public static FastList<ScatterColliderData> collisionData = new FastList<ScatterColliderData>(10000);

        // Incoming and outgoing data queued to be added/removed
        static List<ScatterColliderData> incomingData = new List<ScatterColliderData>(1000);
        static List<ScatterColliderData> outgoingData = new List<ScatterColliderData>(1000);

        //static FastList<ScatterTransformData> activeObjects = new FastList<ScatterTransformData>(300);

        // Collideable scatter index plus the dictionary belonging to it
        // Allows for colliders between same seeds

        static int numVesselsLoaded = 0;
        static int numQuads = 0;

        // Vessel requirements
        static NativeList<float3> vesselPositions;
        static NativeList<float> sqrVesselBounds;

        // Quad requirements
        static NativeList<float3> quadPositions;
        static NativeList<float> sqrQuadBounds;
        static NativeList<int> quadIDs;

        // All scatters on this planet that satisfy the global config's collision level, and are collideable
        public static Scatter[] collideableScatters;
        //public static NativeList<PositionDataQuadID>[] collidersToAdd;
        //public static NativeList<PositionDataQuadID>[] collidersToRemove;

        // Array index is collideable scatter
        // One stream per quad in the quadIDs array output from the quadDistance job
        // We need to append from multiple quad sources to one list, and a NativeList is not concurrency-safe
        // Sadly, NativeList.AsParallelWriter does not exist in this version of unity
        // THESE ARE NOT PERSISTENT, THEY ARE CREATED WHEN NEEDED
        public static NativeStream[] collidersToAdd;
        public static NativeStream[] collidersToRemove;

        public static Dictionary<PositionDataQuadID, GameObject>[] activeObjects;

        static bool initialized = false;

        void Awake()
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(this);
            PQSStartPatch.onPQSStart += DominantBodyLoaded;
            PQSStartPatch.onPQSUnload += DominantBodyUnloaded;
            PQSStartPatch.onPQSRestart += SameBodyLoaded;

            // Allow 100 vessels max before we ignore any extra
            vesselPositions = new NativeList<float3>(100, Allocator.Persistent);
            sqrVesselBounds = new NativeList<float>(100, Allocator.Persistent);

            quadPositions = new NativeList<float3>(1000, Allocator.Persistent);
            sqrQuadBounds = new NativeList<float>(1000, Allocator.Persistent);
            quadIDs = new NativeList<int>(250, Allocator.Persistent);
        }
        public void Load(string name)
        {
            Debug.Log("Dominant body loaded start (collision manager");
            currentScatterBody = ConfigLoader.parallaxScatterBodies[name];
            collideableScatters = currentScatterBody.collideableScatters;

            numCollideableScatters = collideableScatters.Length;

            // Init streams
            collidersToAdd = new NativeStream[numCollideableScatters];
            collidersToRemove = new NativeStream[numCollideableScatters];

            activeObjects = new Dictionary<PositionDataQuadID, GameObject>[numCollideableScatters];

            // Initialize job native arrays
            // Streams created at runtime
            for (int i = 0; i < numCollideableScatters; i++)
            {
                activeObjects[i] = new Dictionary<PositionDataQuadID, GameObject>();
            }
            initialized = true;
            Debug.Log("Dominant body loaded end (collision manager");
        }
        // We need to cleanup even if the same body requested a load
        void SameBodyLoaded(string name)
        {
            //Debug.Log("Same body loading: " + name);
            DominantBodyUnloaded(name);
            DominantBodyLoaded(name);
        }
        void DominantBodyLoaded(string name)
        {
            Load(name);
        }
        void DominantBodyUnloaded(string name)
        {
            Cleanup();
        }
        /// <summary>
        /// Provide new data to the collision manager. Added after the next job completion
        /// </summary>
        /// <param name="dataIn"></param>
        /// <param name="scatterIndex"></param>
        public static void QueueIncomingData(ScatterColliderData data)
        {
            incomingData.Add(data);
        }
        /// <summary>
        /// Queue data to be removed from the collision manager. Removed after the next job completion
        /// </summary>
        /// <param name="data"></param>
        public static void QueueOutgoingData(ScatterColliderData data)
        {
            outgoingData.Add(data);
        }
        // Add data for processing
        static void AddQueuedData()
        {
            foreach (ScatterColliderData data in incomingData)
            {
                // Complete the distance initialisation here
                data.initDistancesHandle.Complete();
                collisionData.Add(data);

                // Add job info
                sqrQuadBounds.Add(data.scatterSystemQuad.sqrQuadWidth);
            }
            
            incomingData.Clear();
        }
        // Remove data from processing
        static void RemoveQueuedData()
        {
            foreach (ScatterColliderData data in outgoingData)
            {
                // Mirror the removal from fastlist in our own data here
                // Remove job info
                sqrQuadBounds.RemoveAtSwapBack(data.id);
                collisionData.Remove(data);

                data.lastDistances.Dispose();
                data.quadLocalData.Dispose();
            }
            outgoingData.Clear();
        }
        // Fetch craft bounds and positions
        // Do this early to prevent calculation for every scatter
        static void UpdateCraftData()
        {
            vesselPositions.Clear();
            sqrVesselBounds.Clear();

            numVesselsLoaded = FlightGlobals.VesselsLoaded.Count;
            foreach (Vessel vessel in FlightGlobals.VesselsLoaded)
            {
                vesselPositions.Add(vessel.transform.position);
                float maxBound = Mathf.Max(vessel.vesselSize.x, vessel.vesselSize.y, vessel.vesselSize.z);
                sqrVesselBounds.Add(maxBound * maxBound);
            }
        }
        // Update the quad positions
        static void UpdateQuadData()
        {
            quadIDs.Clear();
            quadPositions.Clear();
            foreach (ScatterColliderData data in collisionData)
            {
                quadPositions.Add((Vector3)data.scatterSystemQuad.quad.PrecisePosition);
            }
            numQuads = collisionData.Length;
        }

        static bool inQuadJob = false;
        static bool inColliderJob = false;
        static List<JobHandle> colliderJobHandles = new List<JobHandle>();
        static JobHandle findQuadsHandle = new JobHandle();
        static int numColliderJobsCompleted = 0;

        bool allComplete = false;
        void Update()
        {
            if (!initialized)
            {
                return;
            }

            // No jobs running
            // We restart from the beginning here by processing incoming/outgoing data and updating the craft and quad positions
            if (!inQuadJob && !inColliderJob && !allComplete) 
            {
                RemoveQueuedData();
                AddQueuedData();
                UpdateCraftData();
                UpdateQuadData();

                // INCREDIBLY important - If we try to kick off jobs without any loaded vessels or quads, things go to shit
                if (numVesselsLoaded == 0 || numQuads == 0)
                {
                    return;
                }

                // Find which quads are nearest the craft and worth checking the objects on
                DispatchQuadJob();

                // Lock until the quad job is complete
                inQuadJob = true;
            }

            // Quad job completed - setup collider job
            if (inQuadJob && findQuadsHandle.IsCompleted)
            {
                inQuadJob = false;
                SetupColliderJob(quadIDs.Length);
                
                // Dispatch a job on a new thread for each quad
                // Stream represents the quad we're processing. Sometimes we will be processing data from multiple quads where a NativeStream is required
                // Nativelist does not support parallel writing (which will happen from multiple quads)
                int stream = 0;
                foreach (int i in quadIDs)
                {
                    ScatterColliderData data = collisionData[i];
                    Scatter scatter = collideableScatters[data.collideableScattersIndex];
                
                    DispatchColliderJob(scatter, data, stream);
                    stream++;
                }

                // Lock until the collider job is complete
                inColliderJob = true;
            }
            
            // Check for all collider job handles completing
            if (colliderJobHandles.All(x => x.IsCompleted) && inColliderJob)
            {
                // When all collider handles are done, get the data back
                // We cannot shorten this loop from 2O(n) to O(n) because sometimes the final completion(s) will run over to the next frame, and the data will be stagnant
                foreach (JobHandle handle in colliderJobHandles)
                {
                    handle.Complete();
                }
            
                allComplete = true;
            }

            // Finalize - Process the colliders that need to be enabled/disabled and dispose the streams
            if (allComplete)
            {
                DisableInvalidColliders();
                EnableValidColliders();

                CompleteColliderJob();
                inColliderJob = false;

                allComplete = false;
            }
        }

        // Init the streams for the collider job
        static void SetupColliderJob(int numStreams)
        {
            for (int i = 0; i < numCollideableScatters; i++)
            {
                collidersToAdd[i] = new NativeStream(numStreams, Allocator.Persistent);
                collidersToRemove[i] = new NativeStream(numStreams, Allocator.Persistent);
            }
        }
        // Dispose the streams after collider job completion
        static void CompleteColliderJob()
        {
            colliderJobHandles.Clear();

            for (int i = 0; i < numCollideableScatters; i++)
            {
                if (collidersToAdd[i].IsCreated)
                {
                    collidersToAdd[i].Dispose();
                }
                if (collidersToRemove[i].IsCreated)
                {
                    collidersToRemove[i].Dispose();
                }
                
            }
        }
        // Calculate which jobs need to be processed - simple distance check which takes into account vessel and quad bounds
        static void DispatchQuadJob()
        {
            DetermineQuadsForEvaluationJob findQuadsJob = new DetermineQuadsForEvaluationJob
            {
                vesselPositions = CollisionManager.vesselPositions,
                vesselBounds = CollisionManager.sqrVesselBounds,
                vesselCount = CollisionManager.numVesselsLoaded,

                quadPositions = CollisionManager.quadPositions,
                sqrQuadBounds = CollisionManager.sqrQuadBounds,

                quadIndices = CollisionManager.quadIDs,
                count = quadPositions.Length
            };
            findQuadsHandle = findQuadsJob.Schedule();
        }
        // Calculate which colliders are in range using the vessel bounds and scatter mesh bounds
        // One dispatched per quad in range (from quad job) per scatter
        static void DispatchColliderJob(Scatter scatter, ScatterColliderData data, int stream)
        {
            ProcessColliderJob colliderJob = new ProcessColliderJob
            {
                positions = data.quadLocalData,
                lastDistances = data.lastDistances,

                vesselPositions = CollisionManager.vesselPositions,
                vesselBounds = CollisionManager.sqrVesselBounds,
                vesselCount = CollisionManager.numVesselsLoaded,

                quadPosition = data.scatterSystemQuad.quad.gameObject.transform.position,
                sqrQuadBound = data.scatterSystemQuad.sqrQuadWidth,
                localToWorldMatrix = data.scatterSystemQuad.quad.meshRenderer.localToWorldMatrix,
                quadID = data.id,

                scatterSqrMeshBound = scatter.sqrMeshBound,
                collideableScatterIndex = data.collideableScattersIndex,

                collidersToAdd = collidersToAdd[data.collideableScattersIndex].AsWriter(),
                collidersToRemove = collidersToRemove[data.collideableScattersIndex].AsWriter(),
                count = data.quadLocalData.Length,
                stream = stream
            };
            JobHandle colliderHandle = colliderJob.Schedule();
            colliderJobHandles.Add(colliderHandle);
        }
        // Colliders in range that must be enabled
        static void EnableValidColliders()
        {
            for (int i = 0; i < collidersToAdd.Length; i++)
            {
                // The scatter this reader belongs to
                NativeStream.Reader collidersToAddReader = collidersToAdd[i].AsReader();

                // Number of quads this reader contains streams for
                int streamCount = collidersToAddReader.ForEachCount;

                // For each quad in the nativestream
                for (int b = 0; b < streamCount; b++)
                {
                    // Begin reading from the stream
                    int itemsInLocalStream = collidersToAddReader.BeginForEachIndex(b);
                    for (int c = 0; c < itemsInLocalStream; c++)
                    {
                        PositionDataQuadID transformData = collidersToAddReader.Read<PositionDataQuadID>();
                        GameObject go = CreateGameObject(collideableScatters[i], transformData);
                        activeObjects[i].Add(transformData, go);
                    }
                    collidersToAddReader.EndForEachIndex();
                }
            }
        }

        // Colliders going out of range that need to be disabled
        static void DisableInvalidColliders()
        {
            for (int i = 0; i < collidersToRemove.Length; i++)
            {
                // The scatter this reader belongs to
                NativeStream.Reader collidersToRemoveReader = collidersToRemove[i].AsReader();

                // Number of quads this reader contains streams for
                int streamCount = collidersToRemoveReader.ForEachCount;
                for (int b = 0; b < streamCount; b++)
                {
                    // Get the number of items in this stream
                    int itemsInLocalStream = collidersToRemoveReader.BeginForEachIndex(b);
                   
                    // Read data in this stream
                    for (int c = 0; c < itemsInLocalStream; c++)
                    {
                        PositionDataQuadID transformData = collidersToRemoveReader.Read<PositionDataQuadID>();
                        GameObject go = activeObjects[i][transformData];

                        // Remove collider
                        go.SetActive(false);
                        activeObjects[i].Remove(transformData);
                        ConfigLoader.colliderPool.Add(go);
                    }
                    collidersToRemoveReader.EndForEachIndex();
                }
            }
        }
        // Use TRS matrix from gpu data to parent the scatter to the quad to match the visual scatter
        static GameObject CreateGameObject(Scatter scatter, PositionDataQuadID transform)
        {
            ScatterSystemQuadData scatterSystemQuad = collisionData[transform.quadID].scatterSystemQuad;
            GameObject go = ConfigLoader.colliderPool.Fetch();

            go.GetComponent<MeshCollider>().sharedMesh = scatter.renderer.meshLOD1;

            go.transform.SetParent(scatterSystemQuad.quad.gameObject.transform, false);

            go.transform.localPosition = transform.localPos;
            go.transform.localScale = transform.localScale;

            // Ideally I would like this to set localRotation but I spent days trying to get it to work in local space, and couldn't figure it out
            // If align to terrain normal, we need to obtain the terrain normal at this scatter
            Vector3 targetNormal;
            if (scatter.distributionParams.alignToTerrainNormal == 1)
            {
                // Terrain normal
                targetNormal = scatterSystemQuad.GetTerrainNormal(transform.index);
            }
            else
            {
                // Planet normal
                targetNormal = Vector3.Normalize(scatterSystemQuad.quad.gameObject.transform.position - scatterSystemQuad.quad.sphereRoot.transform.position);
            }
            
            Quaternion rotAWorld = Quaternion.FromToRotation(Vector3.up, targetNormal);
            Quaternion rotBWorld = Quaternion.AngleAxis(transform.rotation, Vector3.up);
            go.transform.rotation = rotAWorld * rotBWorld;

            go.tag = scatterSystemQuad.quad.tag;

            go.SetActive(true);
            return go;
        }
        // Release resources and destroy colliders
        public void Cleanup()
        {
            if (inQuadJob)
            {
                findQuadsHandle.Complete();
            }
            if (inColliderJob)
            {
                foreach (JobHandle handle in colliderJobHandles)
                {
                    handle.Complete();
                }
                CompleteColliderJob();
            }

            // Complete the init distances job in the very rare case it has not completed yet
            foreach (ScatterColliderData data in incomingData)
            {
                data.initDistancesHandle.Complete();
                if (data.lastDistances.IsCreated)
                {
                    data.lastDistances.Dispose();
                }
            }

            for (int i = 0; i < numCollideableScatters; i++)
            {
                foreach (GameObject go in activeObjects[i].Values)
                {
                    go.SetActive(false);
                    ConfigLoader.colliderPool.Add(go);
                }
                activeObjects[i].Clear();
            }

            numCollideableScatters = 0;
            initialized = false;
            inQuadJob = false;
            inColliderJob = false;
            allComplete = false;
        }
        // Called on game exit
        void OnDestroy()
        {
            Debug.Log("OnDestroy begun");
            Cleanup();

            // Dispose native resources
            vesselPositions.Dispose();
            sqrVesselBounds.Dispose();

            quadPositions.Dispose();
            sqrQuadBounds.Dispose();
            quadIDs.Dispose();
            Debug.Log("OnDestroy completed");
        }
    }
}
