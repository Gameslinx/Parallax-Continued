using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Rendering;

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
            // Log the active graphics API so bug reports show which one was in use
            ParallaxDebug.Log("Graphics API: " + SystemInfo.graphicsDeviceType + " (" + SystemInfo.graphicsDeviceVersion + ")");

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
            GraphicsDeviceType api = SystemInfo.graphicsDeviceType;
            bool isGL = api == GraphicsDeviceType.OpenGLCore
                     || api == GraphicsDeviceType.OpenGLES2
                     || api == GraphicsDeviceType.OpenGLES3;

            switch (Application.platform)
            {
                case RuntimePlatform.LinuxPlayer:
                    return filePath + "-linux.unity3d";

                case RuntimePlatform.WindowsPlayer:
                    // DX11, DX12 and Vulkan variants all live in the windows bundle,
                    // Unity selects the matching one automatically. OpenGL on Windows
                    // is redirected to the linux bundle.
                    return filePath + (isGL ? "-linux.unity3d" : "-windows.unity3d");

                case RuntimePlatform.OSXPlayer:
                    return filePath + "-macosx.unity3d";

                default:
                    // Fall back to linux and cry
                    ParallaxDebug.LogCritical("Unable to determine platform (Windows, MacOSX, Linux) - Falling back to Linux");
                    return filePath + "-linux.unity3d";
            }
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
                        // Flag assets with no usable variant for the current graphics API
                        Shader s = shader as Shader;
                        if (s != null && !s.isSupported)
                        {
                            ParallaxDebug.LogCritical("Shader has no usable variant on this API: " + s.name);
                        }
                        else if (shader is ComputeShader && !SystemInfo.supportsComputeShaders)
                        {
                            ParallaxDebug.LogCritical("Compute shaders unsupported on this device: " + shader.name);
                        }

                        dest.Add(shader.name, shader);
                        ParallaxDebug.Log("Loaded shader: " + shader.name);
                    }
                }
            }
        }
    }
}
