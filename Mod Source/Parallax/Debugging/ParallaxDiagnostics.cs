using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Debugging
{
    public class ParallaxDiagnostics
    {
        public static void LogComputeShaderResourceUsage()
        {
            string body = FlightGlobals.currentMainBody.name;

            // Shared quad buffers, and any other required buffers
            float requiredUsage = 0;

            // Buffers local to scatters
            float scatterUsage = 0;

            // ScatterRenderer LOD0, LOD1, LOD2
            float rendererUsage = 0;

            // Get required and scatter usages
            foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
            {
                ScatterSystemQuadData data = quadData.Value;
                requiredUsage += GetBufferUsage(data.sourceDirsFromCenterBuffer);
                requiredUsage += GetBufferUsage(data.sourceUVsBuffer);
                requiredUsage += GetBufferUsage(data.sourceNormalsBuffer);
                requiredUsage += GetBufferUsage(data.sourceTrianglesBuffer);
                requiredUsage += GetBufferUsage(data.sourceVertsBuffer);

                foreach (ScatterData scatterData in data.quadScatters)
                {
                    // Need to use reflection to get the private buffers
                    List<ComputeBuffer> buffers = GetPrivateComputeBuffers(scatterData);
                    Debug.Log("Has " + buffers.Count + " buffers");
                    foreach (ComputeBuffer buffer in buffers)
                    {
                        scatterUsage += GetBufferUsage(buffer);
                    }
                }
            }

            // Get renderer usages
            List<ScatterRenderer> renderers = ScatterManager.Instance.activeScatterRenderers;
            foreach (ScatterRenderer renderer in renderers)
            {
                rendererUsage += GetBufferUsage(renderer.outputLOD0);
                rendererUsage += GetBufferUsage(renderer.outputLOD1);
                rendererUsage += GetBufferUsage(renderer.outputLOD2);
            }

            // Log usages

            float totalUsage = requiredUsage + rendererUsage + scatterUsage;

            ParallaxDebug.Log("Parallax Scatter VRAM Usages (Excludes Textures)");
            ParallaxDebug.Log("Required Usage: " + ToMB(requiredUsage));
            ParallaxDebug.Log("Scatter Usage: " + ToMB(scatterUsage));
            ParallaxDebug.Log("Renderer Usage: " + ToMB(rendererUsage));
            ParallaxDebug.Log("Total Usage: " + ToMB(totalUsage));
        }
        /// <summary>
        /// Get ComputeBuffer VRAM usage in bytes
        /// </summary>
        /// <param name="computeBuffer"></param>
        /// <returns></returns>
        public static float GetBufferUsage(ComputeBuffer computeBuffer)
        {
            if (computeBuffer == null || !computeBuffer.IsValid())
            { 
                return 0; 
            }
            return computeBuffer.count * computeBuffer.stride;
        }
        private static List<ComputeBuffer> GetPrivateComputeBuffers(ScatterData instance)
        {
            Type type = typeof(ScatterData);
            List<ComputeBuffer> buffers = new List<ComputeBuffer>();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                // Check if the property type is ComputeBuffer
                if (field.FieldType == typeof(ComputeBuffer))
                {
                    // Get the value of the ComputeBuffer property
                    ComputeBuffer buffer = field.GetValue(instance) as ComputeBuffer;

                    buffers.Add(buffer);
                }
            }
            return buffers;
        }
        private static float ToMB(float value)
        {
            return value / (1024.0f * 1024.0f);
        }
    }
}
