using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallax
{
    public class FastListItem
    {
        public int id;
    }
    public class FastList<T> : IEnumerable where T : FastListItem
    {
        public int Capacity
        {
            get
            {
                return list.Capacity;
            }
        }
        public int Length
        {
            get
            {
                return list.Count;
            }
        }
        private List<T> list;

        public FastList(int capacity)
        {
            list = new List<T>(capacity);
        }
        public void Add(T item)
        {
            if (item == null) return;
            
            item.id = list.Count;
            list.Add(item);
        }
        public void Remove(int id)
        {
            // cache the count 
            int count = list.Count;
            
            // Ensure the item is within range
            if ((count - 1) < id || id < 0)
            {
                return;
            }
            // Handle edge case where there is no item to replace
            if (count == 1)
            {
                list.Clear();
                return;
            }

            // Copy the last item to the removed slot.
            T lastItem = list[count - 1];
            list[id] = lastItem;
            lastItem.id = id;
            
            // Remove the item
            list.RemoveAt(list.Count - 1);
        }
        public void Remove(T item)
        {
            // ensure the item exists
            if (item == null || item.id < 0 || item.id >= list.Count) 
            {
                return;
            }
            // prevent edge case where item id doesnt match list index
            if (list[item.id] != item)
            {
                return;
            }
            Remove(item.id);
        }

        public IEnumerator GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= list.Count) return null;
                return list[index];
            }
        }
        public void Clear()
        {
            list.Clear();
        }
    }
}
