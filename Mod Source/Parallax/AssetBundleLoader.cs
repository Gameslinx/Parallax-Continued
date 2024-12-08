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
        public static Dictionary<string, Shader> parallaxScatterShaders = new Dictionary<string, Shader>();
        public static Dictionary<string, ComputeShader> parallaxComputeShaders = new Dictionary<string, ComputeShader>();
        public static Dictionary<string, Shader> parallaxScaledShaders = new Dictionary<string, Shader>();
        public static Dictionary<string, Shader> parallaxDebugShaders = new Dictionary<string, Shader>();
        public static void Initialize()
        {
            string terrainShaderFilePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "ParallaxContinued/Shaders/ParallaxTerrain");
            string scatterShaderFilePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "ParallaxContinued/Shaders/ParallaxScatters");
            string computeShaderFilePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "ParallaxContinued/Shaders/ParallaxCompute");
            string scaledShaderFilePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "ParallaxContinued/Shaders/ParallaxScaled");
            string debugShaderFilePath = Path.Combine(KSPUtil.ApplicationRootPath + "GameData/" + "ParallaxContinued/Shaders/ParallaxDebug");

            terrainShaderFilePath = DeterminePlatform(terrainShaderFilePath);
            scatterShaderFilePath = DeterminePlatform(scatterShaderFilePath);
            computeShaderFilePath = DeterminePlatform(computeShaderFilePath);
            scaledShaderFilePath = DeterminePlatform(scaledShaderFilePath);
            debugShaderFilePath = DeterminePlatform(debugShaderFilePath);

            LoadAssetBundles<Shader>(terrainShaderFilePath, parallaxTerrainShaders);
            LoadAssetBundles<Shader>(scatterShaderFilePath, parallaxScatterShaders);
            LoadAssetBundles<ComputeShader>(computeShaderFilePath, parallaxComputeShaders);
            LoadAssetBundles<Shader>(scaledShaderFilePath, parallaxScaledShaders);
            LoadAssetBundles<Shader>(debugShaderFilePath, parallaxDebugShaders);
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
        static void LoadAssetBundles<T>(string filePath, Dictionary<string, T> dest) where T : UnityEngine.Object
        {
            var assetBundle = AssetBundle.LoadFromFile(filePath);
            if (assetBundle == null)
            {
                ParallaxDebug.Log("Failed to load bundle at path: " + filePath);
            }
            else
            {
                T[] shaders = assetBundle.LoadAllAssets<T>();
                foreach (T shader in shaders)
                {
                    dest.Add(shader.name, shader);
                    ParallaxDebug.Log("Loaded shader: " + shader.name);
                }
            }
        }
    }
}
