using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static GameEvents;

namespace Parallax
{
    public class Variance
    {
        public string Prop { get; set; }
        public object valA { get; set; }
        public object valB { get; set; }
    }
    // Debug logging with "[Parallax]"
    public static class ParallaxDebug
    {
        public static void Log(string message)
        {
            Debug.Log("[Parallax] " + message);
        }
        public static void LogError(string message) 
        {
            Debug.LogError("[Parallax] " + message);
        }
        public static void LogParseError(string name, string planetName, string type, string value)
        {
            LogError("Error parsing " + name + " on planet: " + planetName + " - Tried parsing as a " + type + " but no matching conversion was found. Value = " + value);
        }
    }
    // Config loader try-parse vars
    public class ConfigUtils
    {
        public static void TryParse(string planetName, string name, string value, Type type, out object result)
        {
            // Must be assigned before function ends
            result = null;
            if (type ==  typeof(string))
            {
                result = value;
            }
            else if (type == typeof(float))
            {
                try
                {
                    result = float.Parse(value);
                }
                catch 
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                }
            }
            else if (type == typeof(Vector3))
            {
                try
                {
                    string[] components = value.Trim().Replace(" ", string.Empty).Split(',');
                    result = new Vector3(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                }
            }
            else if (type == typeof(Color))
            {
                string[] components = value.Trim().Replace(" ", string.Empty).Split(',');
                result = new Color(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
            }
            else
            {
                ParallaxDebug.LogError("Trying to parse " + name + " on planet: " + planetName + " as type " + type.Name + " but converting to this type is unsupported");
            }
        }
    }
}
