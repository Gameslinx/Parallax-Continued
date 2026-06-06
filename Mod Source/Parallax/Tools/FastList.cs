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
            item.id = list.Count;
            list.Add(item);
        }
        public bool Remove(int id)
        {
            int count = list.Count;

            // Stale or out of range id - nothing to remove
            if (id < 0 || id >= count)
            {
                return false;
            }

            // Handle edge case where there is no item to replace
            if (count == 1)
            {
                list.Clear();
                return true;
            }

            // Move the last item into the freed slot to keep id == index
            T lastItem = list[count - 1];
            lastItem.id = id;
            list[id] = lastItem;

            list.RemoveAt(count - 1);
            return true;
        }
        public bool Remove(T item)
        {
            if (item == null || item.id < 0 || item.id >= list.Count)
            {
                return false;
            }

            // After a swap-back the slot may hold a different item - don't remove the wrong one
            if (list[item.id] != item)
            {
                return false;
            }

            return Remove(item.id);
        }

        public IEnumerator GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public T this[int index]
        {
            get
            {
                return list[index];
            }
        }
        public void Clear()
        {
            list.Clear();
        }
    }
}
