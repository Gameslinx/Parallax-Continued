using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    // Holds all shaders that need to be instantiated at runtime
    public class AssetBundleLoader
    {
        public static Dictionary<string, Shader> parallaxTerrainShaders = new Dictionary<string, Shader>();
        public static void Initialize()
        {
            string filePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "Parallax/Shaders/Parallax");
            filePath = DeterminePlatform(filePath);
            LoadAssetBundles(filePath, parallaxTerrainShaders);
        }
        static string DeterminePlatform(string filePath)
        {
            if (Application.platform == RuntimePlatform.LinuxPlayer || (Application.platform == RuntimePlatform.WindowsPlayer && SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL")))
            {
                filePath = filePath + "-linux.unity3d";
            }
            else if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                filePath = filePath + "-windows.unity3d";
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                filePath = filePath + "-macosx.unity3d";
            }
            else
            {
                // Fall back to linux and cry
                filePath = filePath + "-linux.unity3d";
                ParallaxDebug.LogError("Unable to determine platform (Windows, MacOSX, Linux)");
            }
            return filePath;
        }
        // Get all shaders from asset bundle
        static void LoadAssetBundles(string filePath, Dictionary<string, Shader> dest)
        {
            var assetBundle = AssetBundle.LoadFromFile(filePath);
            if (assetBundle == null)
            {
                ParallaxDebug.Log("Failed to load bundle at path: " + filePath);
            }
            else
            {
                Shader[] shaders = assetBundle.LoadAllAssets<Shader>();
                foreach (Shader shader in shaders)
                {
                    dest.Add(shader.name, shader);
                    ParallaxDebug.Log("Loaded shader: " + shader.name);
                }
            }
        }
    }
}
