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
            int count = list.Count;

            // Bounds check
            if (id < 0 || id >= count)
            {
                return;
            }

            // Handle edge case where there is no item to replace
            if (count == 1)
            {
                list.Clear();
                return;
            }

            // Fetch the item from the end of the list to replace the item we're removing
            T itemToReplace = list[count - 1];

            // Set the replacement item ID to the ID we're removing to maintain ID continuity
            itemToReplace.id = id;

            // Set the (now freed) ID to the replacement
            list[id] = itemToReplace;

            // Remove the last item
            list.RemoveAt(count - 1);
        }
        public void Remove(T item)
        {
            if (item == null) return;
            int id = item.id;
            if (id < 0 || id >= list.Count) return;
            if (list[id] != item) return;
            Remove(id);
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
