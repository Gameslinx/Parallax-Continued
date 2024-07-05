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
    // Would be nicer if it was neater but it does the job
    // I'm not a huge fan of writing extensive config loaders
    public static class ConfigUtils
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
                    result = 0;
                }
            }
            else if (type == typeof(int))
            {
                try
                {
                    result = int.Parse(value);
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = 0;
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
                    result = Vector3.one;
                }
            }
            else if (type == typeof(Color))
            {
                try
                {
                    string[] components = value.Trim().Replace(" ", string.Empty).Split(',');
                    result = new Color(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = Color.magenta;
                }
            }
            else if (type == typeof(bool))
            {
                try
                {
                    result = bool.Parse(value);
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = false;
                }
            }
            else
            {
                ParallaxDebug.LogError("Trying to parse " + name + " on planet: " + planetName + " as type " + type.Name + " but converting to this type is unsupported");
            }
        }
        // Alternative version that returns an object instead of passing as out
        public static object TryParse(string planetName, string name, string value, Type type)
        {
            // Must be assigned before function ends
            object result = null;
            if (type == typeof(string))
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
                    result = 0.0f;
                }
            }
            else if (type == typeof(int))
            {
                try
                {
                    result = int.Parse(value);
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = 0;
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
                    result = Vector3.one;
                }
            }
            else if (type == typeof(Color))
            {
                try
                {
                    string[] components = value.Trim().Replace(" ", string.Empty).Split(',');
                    result = new Color(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = Color.magenta;
                }
            }
            else if (type == typeof(bool))
            {
                try
                {
                    result = bool.Parse(value);
                }
                catch
                {
                    ParallaxDebug.LogParseError(name, planetName, type.Name, value);
                    result = false;
                }
            }    
            else
            {
                ParallaxDebug.LogError("Trying to parse " + name + " on planet: " + planetName + " as type " + type.Name + " but converting to this type is unsupported");
            }
            return result;
        }
        public static string TryGetConfigValue(ConfigNode node, string name, bool logIfNull = true)
        {
            string result = node.GetValue(name);
            if (result == null && logIfNull)
            {
                ParallaxDebug.LogError("Error parsing config - Unable to get property '" + name + "'. Fix this!");
            }
            return result;
        }
    }
    public static class TextureUtils
    {
        public static bool IsLinear(string textureName)
        {
            if (textureName.Contains("Bump") || textureName.Contains("Displacement") || textureName.Contains("Influence") || textureName.Contains("Wind")) { return true; }
            return false;
        }
    }
}
