using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Dictionary_TryAdd
{
    public static bool TryAdd(this Dictionary<Vector3, int> dictionary, Vector3 key, int value)
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
            return true;
        }
        
        return false;
    }
}
