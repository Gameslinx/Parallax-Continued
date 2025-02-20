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
            }
            if (SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL") && Application.platform == RuntimePlatform.OSXPlayer)
            {
                ParallaxDebug.LogCritical("Parallax is not supported on MacOSX systems running OpenGL. Please install CrossOver and under 'Advanced Settings', set 'Graphics' to 'D3DMetal' with synchronization 'MSync'");
            }
        }
    }
}
