using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parallax.Debugging
{
    public class ParallaxSystemInfo
    {
        public static string gpuName;
        public static string rendererName;
        public static bool supportsComputeShaders;
        public static bool supportsAsyncReadback;
        public static int availableVRAM;
        public static int availableRAM;
        public static void ReadInfo()
        {
            gpuName = SystemInfo.graphicsDeviceName;
            rendererName = SystemInfo.graphicsDeviceType.ToString();
            supportsComputeShaders = SystemInfo.supportsComputeShaders;
            supportsAsyncReadback = SystemInfo.supportsAsyncGPUReadback;
            availableVRAM = SystemInfo.graphicsMemorySize;
            availableRAM = SystemInfo.systemMemorySize;
        }
        public static void LogInfo()
        {
            ParallaxDebug.Log("System Information: ");
            ParallaxDebug.Log("GPU Name: " + gpuName);
            ParallaxDebug.Log("Renderer Name: " + rendererName);
            ParallaxDebug.Log("Supports Compute Shaders: " + supportsComputeShaders);
            ParallaxDebug.Log("Supports Async Readback: " + supportsAsyncReadback);
            ParallaxDebug.Log("Available VRAM: " + availableVRAM);
            ParallaxDebug.Log("Available RAM: " + availableRAM);

            if (!supportsComputeShaders || !supportsAsyncReadback)
            {
                ParallaxDebug.LogError("This system is not capable of running Parallax.");
                if (!supportsComputeShaders)
                {
                    ParallaxDebug.LogError(" - Reason: This system does not support compute shaders");
                }
                else
                {
                    ParallaxDebug.LogError(" - Reason: This system does not support async GPU readback");
                }
                // This could be better, but it's already made clear that Apple devices aren't supported due to their lack of OpenGL support.
                if (SystemInfo.operatingSystem.ToLower().Contains("mac") || SystemInfo.operatingSystem.ToLower().Contains("osx"))
                {
                    ParallaxDebug.LogError(" - Reason: This is likely due to Apple devices having too low of an OpenGL version");
                }
            }
        }
    }
}
