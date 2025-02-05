using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static GameEvents;
using static KSP.UI.Screens.MessageSystem;

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
        public static void LogCritical(string message) 
        {
            if (!ConfigLoader.parallaxGlobalSettings.debugGlobalSettings.suppressCriticalMessages)
            {
                PopupDialog dialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Critical Parallax Error", "Critical Parallax Error", message, "Okay", true, HighLogic.UISkin);
            }
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
            if (textureName.Contains("Bump") || textureName.Contains("Displacement") || textureName.Contains("Influence") || textureName.Contains("Wind") || textureName.Contains("Height"))
            { return true; }
            return false;
        }
        public static bool IsCube(string textureName)
        {
            if (textureName.Contains("Reflection") || textureName.Contains("Cube") || textureName.Contains("Refraction"))
            {
                return true;
            }
            return false;
        }
    }

    public static class MatrixUtils
    {
        public static Matrix4x4 GetTranslationMatrix(Vector3 pos)
        {
            return Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
        }

        public static Matrix4x4 GetRotationMatrix(Vector3 anglesDeg)
        {
            Vector3 anglesRad = new Vector3(0, Mathf.Deg2Rad * anglesDeg.y, 0);

            float cosX = Mathf.Cos(anglesRad.x);
            float sinX = Mathf.Sin(anglesRad.x);
            float cosY = Mathf.Cos(anglesRad.y);
            float sinY = Mathf.Sin(anglesRad.y);
            float cosZ = Mathf.Cos(anglesRad.z);
            float sinZ = Mathf.Sin(anglesRad.z);

            Matrix4x4 rotationMatrix = new Matrix4x4();

            rotationMatrix[0, 0] = cosY * cosZ;
            rotationMatrix[0, 1] = -cosX * sinZ + sinX * sinY * cosZ;
            rotationMatrix[0, 2] = sinX * sinZ + cosX * sinY * cosZ;

            rotationMatrix[1, 0] = cosY * sinZ;
            rotationMatrix[1, 1] = cosX * cosZ + sinX * sinY * sinZ;
            rotationMatrix[1, 2] = -sinX * cosZ + cosX * sinY * sinZ;

            rotationMatrix[2, 0] = -sinY;
            rotationMatrix[2, 1] = sinX * cosY;
            rotationMatrix[2, 2] = cosX * cosY;

            rotationMatrix[3, 3] = 1f;

            return rotationMatrix;
        }

        public static Matrix4x4 TransformToPlanetNormal(Vector3 a, Vector3 b)
        {
            Quaternion rotationQuaternion = Quaternion.FromToRotation(a, b);
            return Matrix4x4.Rotate(rotationQuaternion);
        }

        public static void GetTRSMatrix(Vector3 position, Vector3 rotationAngles, Vector3 scale, Vector3 terrainNormal, Vector3 localNormal, ref Matrix4x4 mat)
        {
            mat = GetTranslationMatrix(position) * TransformToPlanetNormal(localNormal, terrainNormal) * GetRotationMatrix(rotationAngles) * Matrix4x4.Scale(scale);
        }
    }
}
