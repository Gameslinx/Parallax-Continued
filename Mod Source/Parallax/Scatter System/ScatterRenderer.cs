using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public delegate void EvaluateScatters();
        public event EvaluateScatters onEvaluateScatters;
        public void Enable()
        {
            Debug.Log("[Renderer] OnEnable");
            Prerequisites();
            Initialize();
            FirstTimeArgs();
        }
        // Assign materials and meshes
        void Prerequisites()
        {
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
                Debug.Log("Enabling shader keyword: " + keyword);
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
            foreach (KeyValuePair<string, string> texturePair in properties.shaderTextures)
            {
                Texture texture;
                if (!ConfigLoader.parallaxScatterBodies[planetName].loadedTextures.ContainsKey(texturePair.Value))
                {
                    bool linear = TextureUtils.IsLinear(texturePair.Key);
                    bool isCube = TextureUtils.IsCube(texturePair.Key);
                    if (!isCube)
                    {
                        texture = TextureLoader.LoadTexture(texturePair.Value, linear);
                    }
                    else
                    {
                        texture = TextureLoader.LoadCubeTexture(texturePair.Value, linear);
                    }
                    
                    ConfigLoader.parallaxScatterBodies[planetName].loadedTextures.Add(texturePair.Value, texture);
                }
                else
                {
                    texture = ConfigLoader.parallaxScatterBodies[planetName].loadedTextures[texturePair.Value];
                }
                material.SetTexture(texturePair.Key, texture);
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
            Debug.Log("Scatter: " + scatter.scatterName);
            // Create output buffers - Evaluate() function on quads will fill these
            if (!scatter.isShared)
            {
                int lod0Count = EstimatePerLODMaxCount(scatter.optimizationParams.maxRenderableObjects, scatter.distributionParams.range, scatter.distributionParams.lod1.range * scatter.distributionParams.range);
                int lod1Count = EstimatePerLODMaxCount(scatter.optimizationParams.maxRenderableObjects, scatter.distributionParams.range, scatter.distributionParams.lod2.range * scatter.distributionParams.range);
                int lod2Count = scatter.optimizationParams.maxRenderableObjects;

                Debug.Log(" - LOD0 Count: " + lod0Count);
                Debug.Log(" - LOD1 Count: " + lod1Count);
                Debug.Log(" - LOD2 Count: " + lod2Count);

                outputLOD0 = new ComputeBuffer(lod0Count, TransformData.Size(), ComputeBufferType.Append);
                outputLOD1 = new ComputeBuffer(lod1Count, TransformData.Size(), ComputeBufferType.Append);
                outputLOD2 = new ComputeBuffer(lod2Count, TransformData.Size(), ComputeBufferType.Append);
            }
            else
            {
                // Get buffers from its renderer
                ScatterRenderer renderer = ScatterManager.Instance.GetSharedScatterRenderer(scatter as SharedScatter);
                outputLOD0 = renderer.outputLOD0;
                outputLOD1 = renderer.outputLOD1;
                outputLOD2 = renderer.outputLOD2;
            }

            // Set the instance data on the material
            instancedMaterialLOD0.SetBuffer("_InstanceData", outputLOD0);
            instancedMaterialLOD1.SetBuffer("_InstanceData", outputLOD1);
            instancedMaterialLOD2.SetBuffer("_InstanceData", outputLOD2);

            // Must initialize the count to 0
            outputLOD0.SetCounterValue(0);
            outputLOD1.SetCounterValue(0);
            outputLOD2.SetCounterValue(0);

            rendererBounds = new Bounds(Vector3.zero, Vector3.one * 25000.0f);
        }
        /// <summary>
        /// Estimates the number of objects as a portion of maxObjects that will be visible at one time.
        /// Computed using simple differences in areas
        /// </summary>
        /// <param name="actualMaxCount"></param>
        /// <param name="range"></param>
        /// <param name="maxRange"></param>
        /// <returns></returns>
        int EstimatePerLODMaxCount(int actualMaxCount, float maxRange, float lodRange)
        {
            // This function needs to be generous - Find the minimum count that can be visible given the maximum area

            float maxArea = Mathf.PI * maxRange * maxRange;

            // We can be dealing with pretty low numbers that can make things unreliable
            // Triple the radius, which should account for distribution noise differences
            float thisArea = Mathf.PI * (lodRange * 3) * (lodRange * 3);
            float fraction = thisArea / maxArea;

            float estimation = Mathf.CeilToInt(actualMaxCount * fraction);

            
            return (int)Mathf.Min(actualMaxCount, estimation);
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
        public void Render()
        {
            // Hugely important we set the count to 0 or the buffer will keep filling up
            // For shared scatters, this is already done in the parent scatter renderer so we can skip it here
            if (!scatter.isShared)
            {
                outputLOD0.SetCounterValue(0);
                outputLOD1.SetCounterValue(0);
                outputLOD2.SetCounterValue(0);

                // Fill the buffer with our instanced data
                if (onEvaluateScatters != null)
                {
                    onEvaluateScatters();
                }
            }

            // Copy the count from the output buffer to the indirect args for instancing
            ComputeBuffer.CopyCount(outputLOD0, indirectArgsLOD0, 4);
            ComputeBuffer.CopyCount(outputLOD1, indirectArgsLOD1, 4);
            ComputeBuffer.CopyCount(outputLOD2, indirectArgsLOD2, 4);

            // Render instanced data
            Graphics.DrawMeshInstancedIndirect(meshLOD0, 0, instancedMaterialLOD0, rendererBounds, indirectArgsLOD0, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, Camera.main);
            Graphics.DrawMeshInstancedIndirect(meshLOD1, 0, instancedMaterialLOD1, rendererBounds, indirectArgsLOD1, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, Camera.main);
            Graphics.DrawMeshInstancedIndirect(meshLOD2, 0, instancedMaterialLOD2, rendererBounds, indirectArgsLOD2, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, Camera.main);
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

            ParallaxDebug.Log(" - Count (LOD 0): " + countLOD0[0]);
            ParallaxDebug.Log(" - Count (LOD 1): " + countLOD1[0]);
            ParallaxDebug.Log(" - Count (LOD 2): " + countLOD2[0]);
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
