using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

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
            InitalizeArray initJob = new InitalizeArray
            {
                array = lastDistances,
                initializeTo = float.MaxValue
            };
            initDistancesHandle = initJob.Schedule(dataCount, dataCount / 8);
        }
    }
    public class ScatterTransformData : FastListItem
    {
        public ScatterSystemQuadData scatterSystemQuad;
        public PositionData position;
        public int collideableScattersIndex;
        public GameObject colliderObject;
        public ScatterTransformData(ScatterSystemQuadData scatterSystemQuad, PositionData transform, int collideableScattersIndex)
        {
            this.scatterSystemQuad = scatterSystemQuad;
            this.position = transform;
            this.collideableScattersIndex = collideableScattersIndex;
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

        // O(1) insertion and deletion makes managing large amounts of data much easier
        // Iterated through often to determine which colliders need spawning
        public static FastList<ScatterColliderData> collisionData = new FastList<ScatterColliderData>(10000);

        // Incoming and outgoing data queued to be added/removed
        static List<ScatterColliderData> incomingData = new List<ScatterColliderData>(1000);
        static List<ScatterColliderData> outgoingData = new List<ScatterColliderData>(1000);

        static List<ScatterTransformData> collidersToAdd = new List<ScatterTransformData>();
        static List<ScatterTransformData> collidersToRemove = new List<ScatterTransformData>();

        static FastList<ScatterTransformData> activeObjects = new FastList<ScatterTransformData>(300);

        // Collideable scatter index plus the dictionary belonging to it
        // Allows for colliders between same seeds
        static Dictionary<PositionData, ScatterTransformData>[] activeObjectsDict;

        //static List<Vector3> vesselPositions = new List<Vector3>();
        //static List<float> sqrVesselMaxSizes = new List<float>();
        static int numVesselsLoaded = 0;

        static NativeList<Vector3> vesselPositions;
        static NativeList<float> sqrVesselBounds;


        // All scatters on this planet that satisfy the global config's collision level, and are collideable
        public static Scatter[] collideableScatters;

        void Awake()
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(this);
            PQSStartPatch.onPQSStart += DominantBodyLoaded;
            PQSStartPatch.onPQSUnload += DominantBodyUnloaded;
        }
        void DominantBodyLoaded(string name)
        {
            currentScatterBody = ConfigLoader.parallaxScatterBodies[name];
            collideableScatters = currentScatterBody.collideableScatters;
            activeObjectsDict = new Dictionary<PositionData, ScatterTransformData>[collideableScatters.Length];
            for (int i = 0; i < activeObjectsDict.Length; i++)
            {
                activeObjectsDict[i] = new Dictionary<PositionData, ScatterTransformData>();
            }
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
            }
            incomingData.Clear();
        }
        // Remove data from processing
        static void RemoveQueuedData()
        {
            foreach (ScatterColliderData data in outgoingData)
            {
                collisionData.Remove(data);
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
        
        void Update()
        {
            /*
             * Requirements
             * Quad requirements:
             * 1. Quad positions - every frame
             * 2. Quad width - once
             * 3. Quad local to world matrix - every frame
             * 
             * Craft requirements:
             * 1. Craft positions
             * 2. Craft bounds
             * 
             * Scatter requirements:
             * 1. Scatter bounds
             */
        }

        // Single threaded implementation
        void OldUpdate()
        {
            // Process incoming and outgoing data
            RemoveQueuedData();
            AddQueuedData();
            UpdateCraftData();

            foreach (ScatterColliderData data in collisionData)
            {
                ScatterSystemQuadData scatterSystemQuad = data.scatterSystemQuad;
                PQ quad = scatterSystemQuad.quad;
                Scatter scatter = collideableScatters[data.collideableScattersIndex];

                float quadSqrDistance = SqrQuadDistanceToNearestCraft(quad.gameObject.transform.position);
                float quadSqrWidth = ScatterComponent.scatterQuadData[quad].sqrQuadWidth;
                if (quadSqrDistance - quadSqrWidth > 0)
                {
                    continue;
                }

                // Potentially:
                // Work out the last world position by using how far the craft moved to offset the world position backwards by that amount
                // Then we know if it's just come in range, or just gone out of range, based on if one distance check is true and the other is false
                // Support world origin changes by doing the same for the world origin and offsetting the previous craft position by the difference

                for (int i = 0; i < data.dataCount; i++)
                {
                    PositionData position = data.quadLocalData[i];
                    // All data stored for this scatter
                    Vector3 localPos = position.localPos;
                    Vector3 localScale = position.localScale;

                    // Evaluate distance to nearest craft
                    Vector3 worldPos = quad.meshRenderer.localToWorldMatrix.MultiplyPoint3x4(localPos);
                    float meshSize = Mathf.Max(localScale.x, localScale.y, localScale.z) * scatter.sqrMeshBound;
                    float sqrDistance = SqrDistanceToNearestCraft(worldPos, meshSize);

                    // Just come into range, add the collider
                    if (sqrDistance < 0 && data.lastDistances[i] >= 0)
                    {
                        ScatterTransformData transformData = new ScatterTransformData(scatterSystemQuad, position, data.collideableScattersIndex);
                        collidersToAdd.Add(transformData);

                        // Note - this is added before collidersToAdd is processed and added
                        activeObjectsDict[data.collideableScattersIndex].Add(position, transformData);
                        Debug.Log("Queued collider data");
                    }
                    // Just gone out of range, remove the collider
                    if (sqrDistance >= 0 && data.lastDistances[i] < 0)
                    {
                        // This collider needs removing if it's active
                        if (activeObjectsDict[data.collideableScattersIndex].TryGetValue(position, out ScatterTransformData transformData))
                        {
                            collidersToRemove.Add(transformData);
                            activeObjectsDict[data.collideableScattersIndex].Remove(position);
                        }
                    }
                    data.lastDistances[i] = sqrDistance;
                }
            }

            DisableInvalidColliders();
            EnableValidColliders();
        }
        static void EnableValidColliders()
        {
            // foreach in colliders to add
            // add colliders
            foreach (ScatterTransformData transformData in collidersToAdd)
            {
                Debug.Log("Collider added");
                activeObjects.Add(transformData);
                CreateGameObject(transformData);
            }
            collidersToAdd.Clear();
        }

        static void DisableInvalidColliders()
        {
            foreach (ScatterTransformData transformData in collidersToRemove)
            {
                Debug.Log("Collider removed");
                activeObjects.Remove(transformData);

                // Remove collider
                transformData.colliderObject.SetActive(false);
                ConfigLoader.colliderPool.Add(transformData.colliderObject);
            }
            collidersToRemove.Clear();
        }
        static void CreateGameObject(ScatterTransformData transform)
        {
            GameObject go = ConfigLoader.colliderPool.Fetch();
            Scatter scatter = collideableScatters[transform.collideableScattersIndex];

            go.GetComponent<MeshCollider>().sharedMesh = scatter.renderer.meshLOD1;
            go.GetComponent<MeshFilter>().sharedMesh = scatter.renderer.meshLOD1;

            go.transform.SetParent(transform.scatterSystemQuad.quad.gameObject.transform, false);

            go.transform.localPosition = transform.position.localPos;
            go.transform.localScale = transform.position.localScale;

            // Ideally I would like this to set localRotation but I spent days trying to get it to work in local space, and couldn't figure it out
            // If align to terrain normal, we need to obtain the terrain normal at this scatter
            Vector3 targetNormal;
            if (scatter.distributionParams.alignToTerrainNormal == 1)
            {
                // Terrain normal
                targetNormal = transform.scatterSystemQuad.GetTerrainNormal(transform.position.index);
            }
            else
            {
                // Planet normal
                targetNormal = Vector3.Normalize(transform.scatterSystemQuad.quad.gameObject.transform.position - transform.scatterSystemQuad.quad.sphereRoot.transform.position);
            }
            
            Quaternion rotAWorld = Quaternion.FromToRotation(Vector3.up, targetNormal);
            Quaternion rotBWorld = Quaternion.AngleAxis(transform.position.rotation, Vector3.up);
            go.transform.rotation = rotAWorld * rotBWorld;

            transform.colliderObject = go;

            go.SetActive(true);
        }
        static void DecomposeTRS(in Matrix4x4 m, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            // Extract new local position
            position = m.GetColumn(3);

            // Extract new local rotation
            rotation = Quaternion.LookRotation(
                m.GetColumn(2),
                m.GetColumn(1)
            );

            // Extract new local scale
            scale = new Vector3(
                m.GetColumn(0).magnitude,
                m.GetColumn(1).magnitude,
                m.GetColumn(2).magnitude
            );
        }
        /// <summary>
        /// Calculates the square distance to the nearest craft, taking into account vessel bounds and scatter bounds. Returns 0 if in range
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public float SqrDistanceToNearestCraft(in Vector3 worldPos, in float sqrMeshSize)
        {
            float closest = float.MaxValue;
            float distance;
            for (int i = 0; i < numVesselsLoaded; i++)
            {
                // Generous distance calculation that assumes worst case mesh and craft alignment
                distance = (vesselPositions[i] - worldPos).sqrMagnitude - sqrVesselMaxSizes[i] - sqrMeshSize;
                if (distance < closest)
                {
                    closest = distance;
                }
            }
            return closest;
        }
        public float SqrQuadDistanceToNearestCraft(in Vector3 worldPos)
        {
            float closest = float.MaxValue;
            float distance;
            for (int i = 0; i < numVesselsLoaded; i++)
            {
                // Generous distance calculation that assumes worst case mesh and craft alignment
                distance = (vesselPositions[i] - worldPos).sqrMagnitude - sqrVesselMaxSizes[i];
                if (distance < closest)
                {
                    closest = distance;
                }
            }
            return closest;
        }
        // Release resources and destroy colliders
        public void Cleanup()
        {
            foreach (ScatterTransformData collider in activeObjects)
            {
                collider.colliderObject.SetActive(false);
                ConfigLoader.colliderPool.Add(collider.colliderObject);
            }
            activeObjects.Clear();

            if (activeObjectsDict != null)
            {
                foreach (var dictionary in activeObjectsDict)
                {
                    dictionary.Clear();
                }
            }

            incomingData.Clear();
            outgoingData.Clear();
            
            collidersToAdd.Clear();
            collidersToRemove.Clear();
        }
    }
}
