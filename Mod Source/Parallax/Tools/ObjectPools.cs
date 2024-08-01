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
            // Would be a bit silly to need to instantiate if we're out of capacity later and the template object was destroyed innit
            // Bit like yoinking a turret out of the scanner in Chapter 5 (The Escape) from the hit game Portal 2 by Valve
            GameObject.DontDestroyOnLoad(type);
            for (int i = 0; i < capacity; i++)
            {
                T obj = InitSingle();
                pool.Enqueue(obj);
            }
        }
        protected virtual T InitSingle()
        {
            T obj = UnityEngine.Object.Instantiate(type);
            if (typeof(T) == typeof(GameObject))
            {
                GameObject.DontDestroyOnLoad(obj);
            }
            return obj;
        }
        public virtual T Fetch()
        {
            if (pool.Count == 0)
            {
                return InitSingle();
            }
            return pool.Dequeue();
        }
        public virtual void Add(T obj)
        {
            pool.Enqueue(obj);
        }
    }
}
