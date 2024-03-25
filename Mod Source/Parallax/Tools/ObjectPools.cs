using Smooth.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    // Base object pool that can provide whatever object specified
    // Used for ComputeShaders, GameObjects, car keys...
    public class ObjectPool<T> where T : UnityEngine.Object
    {
        public readonly int capacity;
        private T type;
        private Queue<T> pool = new Queue<T>();
        public ObjectPool(T type, int capacity)
        {
            this.type = type;
            this.capacity = capacity;
            InitPool();
        }
        private void InitPool()
        {
            for (int i = 0; i < capacity; i++)
            {
                T obj = InitSingle();
                pool.Enqueue(obj);
            }
        }
        protected virtual T InitSingle()
        {
            T obj = UnityEngine.Object.Instantiate(type);
            return obj;
        }
        public virtual T Fetch()
        {
            if (pool.Count == 0)
            {
                ParallaxDebug.LogError("Warning: Object pool with type " + typeof(T).Name + " is empty! Consider initialising with a larger number of items, or fix what's using so many");
                return InitSingle();
            }
            return pool.Dequeue();
        }
        public virtual void Add(T obj)
        {
            pool.Enqueue(obj);
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ParallaxObjectPools : MonoBehaviour
    {
        public static ObjectPool<GameObject> terrainQuadPoolInstance;
        void Start()
        {
            InitTerrainQuadPool();
        }
        void InitTerrainQuadPool()
        {
            GameObject baseObject = new GameObject("ParallaxAvdSubdivisionQuad");
            baseObject.AddComponent<MeshFilter>();
            baseObject.AddComponent<MeshRenderer>();
            baseObject.SetActive(false);

            terrainQuadPoolInstance = new ObjectPool<GameObject>(baseObject, 8);
        }
    }
}
