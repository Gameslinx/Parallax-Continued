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
        public void Remove(int id)
        {
            // Handle edge case where there is no item to replace
            if (list.Count == 1)
            {
                list.RemoveAt(0);
                return;
            }

            // Fetch the item from the end of the list to replace the item we're removing
            T itemToReplace = list[list.Count - 1];

            // Set the replacement item ID to the ID we're removing to maintain ID continuity
            itemToReplace.id = id;

            // Set the last list element to what we're about to remove
            list[list.Count - 1] = list[id];

            // Set the (now freed) ID to the replacement
            list[id] = itemToReplace;

            // Remove the item
            list.RemoveAt(list.Count - 1);
        }
        public void Remove(T item)
        {
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
                return list[index];
            }
        }
        public void Clear()
        {
            list.Clear();
        }
    }
}
