using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Parallax
{
    public class ScatterRenderer : MonoBehaviour
    {
        public string planetName;
        public Scatter scatter;

        public Material instancedMaterialLOD0;
        public Material instancedMaterialLOD1;
        public Material instancedMaterialLOD2;

        public Mesh meshLOD0;
        public Mesh meshLOD1;
        public Mesh meshLOD2;

        public ComputeBuffer outputLOD0;
        public ComputeBuffer outputLOD1;
        public ComputeBuffer outputLOD2;

        ComputeBuffer indirectArgsLOD0;
        ComputeBuffer indirectArgsLOD1;
        ComputeBuffer indirectArgsLOD2;

        Bounds rendererBounds;

        public void Enable()
        {
            Prerequisites();
            Initialize();
            FirstTimeArgs();
        }
        // Assign materials and meshes
        void Prerequisites()
        {
            PreloadTextures();

            meshLOD0 = Instantiate(GameDatabase.Instance.GetModel(scatter.modelPath).GetComponent<MeshFilter>().mesh);
            meshLOD1 = Instantiate(GameDatabase.Instance.GetModel(scatter.distributionParams.lod1.modelPathOverride).GetComponent<MeshFilter>().mesh);
            meshLOD2 = Instantiate(GameDatabase.Instance.GetModel(scatter.distributionParams.lod2.modelPathOverride).GetComponent<MeshFilter>().mesh);

            instancedMaterialLOD0 = new Material(AssetBundleLoader.parallaxScatterShaders[scatter.materialParams.shader]);
            instancedMaterialLOD1 = new Material(AssetBundleLoader.parallaxScatterShaders[scatter.distributionParams.lod1.materialOverride.shader]);
            instancedMaterialLOD2 = new Material(AssetBundleLoader.parallaxScatterShaders[scatter.distributionParams.lod2.materialOverride.shader]);

            SetLOD0MaterialParams();
            SetLOD1MaterialParams();
            SetLOD2MaterialParams();
        }

        void PreloadTextures()
        {
            PreloadMaterialTextures(in scatter.materialParams);
            PreloadMaterialTextures(in scatter.distributionParams.lod1.materialOverride);
            PreloadMaterialTextures(in scatter.distributionParams.lod2.materialOverride);
        }

        void PreloadMaterialTextures(in MaterialParams materialParams)
        {
            // Start loading textures asynchronously. We will still block on
            // them when setting the material parameters, but this way they
            // can run async in the background while we're blocked.

            var properties = materialParams.shaderProperties;
            var loadedTextures = ConfigLoader.parallaxScatterBodies[planetName].loadedTextures;

            foreach (var (name, path) in properties.shaderTextures)
            {
                if (loadedTextures.ContainsKey(name))
                    continue;

                bool linear = TextureUtils.IsLinear(name);
                bool isCube = TextureUtils.IsCube(name);
                var options = new TextureLoadOptions
                {
                    linear = linear,
                    unreadable = true
                };

                TextureLoadManager.TextureHandle<Texture> handle;
                if (!isCube)
                    handle = TextureLoadManager.LoadTexture(path, options);
                else
                    handle = TextureLoadManager.LoadCubemap(path, options);

                loadedTextures.Add(name, handle);
            }
        }

        /// <summary>
        /// Sets the actual material parameters for a given set of params and scatter material
        /// </summary>
        /// <param name="materialParams"></param>
        /// <param name="material"></param>
        public void SetMaterialParams(in MaterialParams materialParams, Material material)
        {
            ShaderProperties properties = materialParams.shaderProperties;
            // Set textures - OnEnable is called when the renderer is re-enabled on planet change, so we can load textures here
            // They are unloaded by the scatter manager

            bool isCutout = false;
            // Keywords
            foreach (string keyword in materialParams.shaderKeywords)
            {
                material.EnableKeyword(keyword);
                if (keyword == "ALPHA_CUTOFF")
                {
                    isCutout = true;
                }
            }

            // Set scriptable render params
            if (isCutout)
            {
                // Is alpha cutout
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetOverrideTag("IgnoreProjector", "True");
                material.SetOverrideTag("Queue", "AlphaTest");

                material.SetInt("_SrcMode", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstMode", (int)UnityEngine.Rendering.BlendMode.Zero);
            }
            else
            {
                // Is opaque (we don't support transparency)
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetOverrideTag("Queue", "Geometry");

                material.SetInt("_SrcMode", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstMode", (int)UnityEngine.Rendering.BlendMode.Zero);
            }

            // Textures
            var loadedTextures = ConfigLoader.parallaxScatterBodies[planetName].loadedTextures;
            foreach (var name in properties.shaderTextures.Keys)
            {
                // Texture load was started in PreloadTextures
                material.SetTexture(name, loadedTextures[name].Texture);
            }

            // Floats
            foreach (KeyValuePair<string, float> floatPair in properties.shaderFloats)
            {
                material.SetFloat(floatPair.Key, floatPair.Value);
            }

            // Vectors
            foreach (KeyValuePair<string, Vector3> vectorPair in properties.shaderVectors)
            {
                material.SetVector(vectorPair.Key, vectorPair.Value);
            }

            // Colors
            foreach (KeyValuePair<string, Color> colorPair in properties.shaderColors)
            {
                material.SetColor(colorPair.Key, colorPair.Value);
            }

            // Ints
            foreach (KeyValuePair<string, int> intPair in properties.shaderInts)
            {
                material.SetInt(intPair.Key, intPair.Value);
            }
        }
        /// <summary>
        /// Explicitly set LOD0 material params
        /// </summary>
        public void SetLOD0MaterialParams()
        {
            SetMaterialParams(scatter.materialParams, instancedMaterialLOD0);
        }
        /// <summary>
        /// Explicitly set LOD1 material params
        /// </summary>
        public void SetLOD1MaterialParams()
        {
            SetMaterialParams(scatter.distributionParams.lod1.materialOverride, instancedMaterialLOD1);
        }
        /// <summary>
        /// Explicitly set LOD2 material params
        /// </summary>
        public void SetLOD2MaterialParams()
        {
            SetMaterialParams(scatter.distributionParams.lod2.materialOverride, instancedMaterialLOD2);
        }

        void Initialize()
        {
            if (!scatter.isShared)
            {
                // LOD change distances remain the same regardless of range multiplier
                // Only LOD2 is affected
                float originalRange = scatter.distributionParams.originalRange;
                float newRange = scatter.distributionParams.range;

                float originalArea = Mathf.PI * originalRange * originalRange;
                float newArea = Mathf.PI * newRange * newRange;

                float areaFactor = newArea / originalArea;

                float densityFactor = ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier;

                // Apply area factor to LOD2 and apply density factor to all LODs
                int newLOD0Count = Mathf.CeilToInt(scatter.optimizationParams.maxRenderableObjectsLOD0 * densityFactor);
                int newLOD1Count = Mathf.CeilToInt(scatter.optimizationParams.maxRenderableObjectsLOD1 * densityFactor);
                int newLOD2Count = Mathf.CeilToInt(scatter.optimizationParams.maxRenderableObjectsLOD2 * areaFactor * densityFactor);

                outputLOD0 = new ComputeBuffer(newLOD0Count, TransformData.Size(), ComputeBufferType.Append);
                outputLOD1 = new ComputeBuffer(newLOD1Count, TransformData.Size(), ComputeBufferType.Append);
                outputLOD2 = new ComputeBuffer(newLOD2Count, TransformData.Size(), ComputeBufferType.Append);
            }
            else
            {
                ScatterRenderer renderer = ScatterManager.Instance.GetSharedScatterRenderer(scatter as SharedScatter);
                outputLOD0 = renderer.outputLOD0;
                outputLOD1 = renderer.outputLOD1;
                outputLOD2 = renderer.outputLOD2;
            }

            instancedMaterialLOD0.SetBuffer("_InstanceData", outputLOD0);
            instancedMaterialLOD1.SetBuffer("_InstanceData", outputLOD1);
            instancedMaterialLOD2.SetBuffer("_InstanceData", outputLOD2);

            outputLOD0.SetCounterValue(0);
            outputLOD1.SetCounterValue(0);
            outputLOD2.SetCounterValue(0);

            rendererBounds = new Bounds(Vector3.zero, Vector3.one * 50000.0f);
        }

        int EstimatePerLODMaxCount(int actualMaxCount, float maxRange, float lodRange)
        {
            float maxArea = Mathf.PI * maxRange * maxRange;

            // Adjust safety margin to prevent undershooting
            float adjustedLodRange = lodRange;
            float thisArea = Mathf.PI * adjustedLodRange * adjustedLodRange;

            float fraction = Mathf.Clamp01(thisArea / maxArea);
            float estimation = Mathf.CeilToInt(actualMaxCount * fraction);

            // Undershoot safety
            return (int)Mathf.Max(estimation, Mathf.CeilToInt(actualMaxCount * 0.1f));
        }
        void FirstTimeArgs()
        {
            uint[] argumentsLod0 = new uint[5] { 0, 0, 0, 0, 0 };
            argumentsLod0[0] = (uint)meshLOD0.GetIndexCount(0);
            argumentsLod0[1] = 0; // Number of meshes to instance, we will set this in Render() through CopyCount
            argumentsLod0[2] = (uint)meshLOD0.GetIndexStart(0);
            argumentsLod0[3] = (uint)meshLOD0.GetBaseVertex(0);

            uint[] argumentsLod1 = new uint[5] { 0, 0, 0, 0, 0 };
            argumentsLod1[0] = (uint)meshLOD1.GetIndexCount(0);
            argumentsLod1[1] = 0; // Number of meshes to instance, we will set this in Render() through CopyCount
            argumentsLod1[2] = (uint)meshLOD1.GetIndexStart(0);
            argumentsLod1[3] = (uint)meshLOD1.GetBaseVertex(0);

            uint[] argumentsLod2 = new uint[5] { 0, 0, 0, 0, 0 };
            argumentsLod2[0] = (uint)meshLOD2.GetIndexCount(0);
            argumentsLod2[1] = 0; // Number of meshes to instance, we will set this in Render() through CopyCount
            argumentsLod2[2] = (uint)meshLOD2.GetIndexStart(0);
            argumentsLod2[3] = (uint)meshLOD2.GetBaseVertex(0);

            indirectArgsLOD0 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            indirectArgsLOD0.SetData(argumentsLod0);

            indirectArgsLOD1 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            indirectArgsLOD1.SetData(argumentsLod1);

            indirectArgsLOD2 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            indirectArgsLOD2.SetData(argumentsLod2);
        }
        // Called on Update from ScatterManager.cs
        public void PreRender()
        {
            // Hugely important we set the count to 0 or the buffer will keep filling up
            // For shared scatters, this is already done in the parent scatter renderer so we can skip it here

            if (!scatter.isShared)
            {
                outputLOD0.SetCounterValue(0);
                outputLOD1.SetCounterValue(0);
                outputLOD2.SetCounterValue(0);
            }
        }
        public void Render()
        {
            // Copy the count from the output buffer to the indirect args for instancing
            ComputeBuffer.CopyCount(outputLOD0, indirectArgsLOD0, 4);
            ComputeBuffer.CopyCount(outputLOD1, indirectArgsLOD1, 4);
            ComputeBuffer.CopyCount(outputLOD2, indirectArgsLOD2, 4);

            // Render instanced data
            Graphics.DrawMeshInstancedIndirect(meshLOD0, 0, instancedMaterialLOD0, rendererBounds, indirectArgsLOD0, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, Camera.main);
            Graphics.DrawMeshInstancedIndirect(meshLOD1, 0, instancedMaterialLOD1, rendererBounds, indirectArgsLOD1, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, Camera.main);
            Graphics.DrawMeshInstancedIndirect(meshLOD2, 0, instancedMaterialLOD2, rendererBounds, indirectArgsLOD2, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, Camera.main);
        }
        // Called on Update from ScatterManager.cs, if we're not in DX11
        public void RenderInCameras(params Camera[] cameras)
        {
            // Copy the count from the output buffer to the indirect args for instancing
            ComputeBuffer.CopyCount(outputLOD0, indirectArgsLOD0, 4);
            ComputeBuffer.CopyCount(outputLOD1, indirectArgsLOD1, 4);
            ComputeBuffer.CopyCount(outputLOD2, indirectArgsLOD2, 4);

            foreach (Camera cam in cameras)
            {
                // Render instanced data
                Graphics.DrawMeshInstancedIndirect(meshLOD0, 0, instancedMaterialLOD0, rendererBounds, indirectArgsLOD0, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, cam);
                Graphics.DrawMeshInstancedIndirect(meshLOD1, 0, instancedMaterialLOD1, rendererBounds, indirectArgsLOD1, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, cam);
                Graphics.DrawMeshInstancedIndirect(meshLOD2, 0, instancedMaterialLOD2, rendererBounds, indirectArgsLOD2, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 15, cam);
            }
        }

        /// <summary>
        /// Log performance stats. Outputs the number of triangles in total being rendered by this renderer
        /// </summary>
        /// <returns></returns>
        public int LogStats()
        {
            ComputeBuffer countBuffer0 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            ComputeBuffer countBuffer1 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            ComputeBuffer countBuffer2 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);

            int[] countLOD0 = { 0, 0, 0 };
            int[] countLOD1 = { 0, 0, 0 };
            int[] countLOD2 = { 0, 0, 0 };

            ComputeBuffer.CopyCount(outputLOD0, countBuffer0, 0);
            ComputeBuffer.CopyCount(outputLOD1, countBuffer1, 0);
            ComputeBuffer.CopyCount(outputLOD2, countBuffer2, 0);

            countBuffer0.GetData(countLOD0);
            countBuffer1.GetData(countLOD1);
            countBuffer2.GetData(countLOD2);

            ParallaxDebug.Log("///////////////////");
            ParallaxDebug.Log("");

            ParallaxDebug.Log("Scatter: " + scatter.scatterName);

            ParallaxDebug.Log(" - Count (LOD 0): " + countLOD0[0] + " / " + outputLOD0.count);
            ParallaxDebug.Log(" - Count (LOD 1): " + countLOD1[0] + " / " + outputLOD1.count);
            ParallaxDebug.Log(" - Count (LOD 2): " + countLOD2[0] + " / " + outputLOD2.count);
            ParallaxDebug.Log("");

            int trisLOD0 = ((meshLOD0.triangles.Length / 3) * countLOD0[0]);
            int trisLOD1 = ((meshLOD1.triangles.Length / 3) * countLOD1[0]);
            int trisLOD2 = ((meshLOD2.triangles.Length / 3) * countLOD2[0]);

            int numTotalTris = trisLOD0 + trisLOD1 + trisLOD2;

            ParallaxDebug.Log(" - Triangles (LOD 0): " + trisLOD0);
            ParallaxDebug.Log(" - Triangles (LOD 1): " + trisLOD1);
            ParallaxDebug.Log(" - Triangles (LOD 2): " + trisLOD2);

            ParallaxDebug.Log("");
            ParallaxDebug.Log(" - Triangles (TOTAL): " + numTotalTris);

            ParallaxDebug.Log("");
            ParallaxDebug.Log("///////////////////");

            countBuffer0.Dispose();
            countBuffer1.Dispose();
            countBuffer2.Dispose();

            return numTotalTris;
        }
        void Cleanup()
        {
            ReleaseBuffers();

            UnityEngine.Object.Destroy(meshLOD0);
            UnityEngine.Object.Destroy(meshLOD1);
            UnityEngine.Object.Destroy(meshLOD2);

            UnityEngine.Object.Destroy(instancedMaterialLOD0);
            UnityEngine.Object.Destroy(instancedMaterialLOD1);
            UnityEngine.Object.Destroy(instancedMaterialLOD2);
        }
        public void ReleaseBuffers()
        {
            outputLOD0?.Dispose();
            outputLOD1?.Dispose();
            outputLOD2?.Dispose();
            indirectArgsLOD0?.Dispose();
            indirectArgsLOD1?.Dispose();
            indirectArgsLOD2?.Dispose();
        }
        public void Disable()
        {
            Cleanup();
        }
    }

}
