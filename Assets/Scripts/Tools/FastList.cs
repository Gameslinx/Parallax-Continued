using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public class ListItem
{
    public int id;
}
public class CacheLocalList<T> where T : ListItem
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

    public CacheLocalList(int capacity)
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
    public T this[int index]
    {
        get
        {
            return list[index];
        }
    }
}

public class FastList : MonoBehaviour
{
    static CacheLocalList<ListItem> list;
    static int size = 10;
    // Start is called before the first frame update
    void Start()
    {
        list = new CacheLocalList<ListItem>(size);
        for (int i = 0; i < size; i++)
        {
            ListItem item = new ListItem();
            list.Add(item);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public Rect rect = new Rect(100, 100, 400, 700);
    void OnGUI()
    {
        //rect = GUILayout.Window(GetInstanceID(), rect, DrawWindow, "List GUI");
    }
    public static void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Displaying list IDs in order of list index:");
        for (int i = 0; i < list.Length; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("ID: " + i);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Value: " + list[i].id);
            GUILayout.EndHorizontal();
        }

        for (int i = 0; i < list.Length; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Remove item at " + i);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove " + i))
            {
                list.Remove(i);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUI.DragWindow();
    }
}
