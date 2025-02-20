using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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
            ConfigNode assetBundleNode = GameDatabase.Instance.GetConfigs("ParallaxAssetBundleList")[0].config;

            string[] terrainShaderFilePaths = GetShaderPaths(assetBundleNode, "Terrain");
            string[] scatterShaderFilePaths = GetShaderPaths(assetBundleNode, "Scatter");
            string[] computeShaderFilePaths = GetShaderPaths(assetBundleNode, "Compute");
            string[] scaledShaderFilePaths = GetShaderPaths(assetBundleNode, "Scaled");
            string[] debugShaderFilePaths = GetShaderPaths(assetBundleNode, "Debug");

            LoadAssetBundles<Shader>(terrainShaderFilePaths, parallaxTerrainShaders);
            LoadAssetBundles<Shader>(scatterShaderFilePaths, parallaxScatterShaders);
            LoadAssetBundles<ComputeShader>(computeShaderFilePaths, parallaxComputeShaders);
            LoadAssetBundles<Shader>(scaledShaderFilePaths, parallaxScaledShaders);
            LoadAssetBundles<Shader>(debugShaderFilePaths, parallaxDebugShaders);
        }
        static string[] GetShaderPaths(ConfigNode assetBundleNode, string nodePrefix)
        {
            ConfigNode shaderTypeNode = assetBundleNode.GetNode(nodePrefix + "Shaders");
            string[] bundleNames = shaderTypeNode.GetValues("path");
            for (int i = 0; i < bundleNames.Length; i++)
            {
                bundleNames[i] = ConfigLoader.GameDataPath + DeterminePlatform(bundleNames[i]);
            }
            return bundleNames;
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
                ParallaxDebug.LogCritical("Unable to determine platform (Windows, MacOSX, Linux) - Falling back to Linux");
            }
            return filePath;
        }
        // Get all shaders from asset bundle
        static void LoadAssetBundles<T>(string[] filePaths, Dictionary<string, T> dest) where T : UnityEngine.Object
        {
            if (filePaths.Length == 0)
            {
                ParallaxDebug.LogCritical("Asset bundle load requested, but no file paths were supplied. Installation error?");
            }
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    ParallaxDebug.LogCritical("Asset bundle load requested, but the file doesn't exist on disk! " + filePath);
                    return;
                }
                var assetBundle = AssetBundle.LoadFromFile(filePath);
                if (assetBundle == null)
                {
                    ParallaxDebug.LogCritical("Failed to load bundle at path: " + filePath);
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
}
