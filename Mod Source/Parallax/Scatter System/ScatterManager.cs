using Kopernicus.Components;
using Parallax.Harmony_Patches;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

namespace Parallax
{
    // Scatter Manager - Manages scatter renderers and enabling/disabling renderers
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class ScatterManager : MonoBehaviour
    {
        public static ScatterManager Instance;

        // Stores all renderer components
        public List<ScatterRenderer> scatterRenderers = new List<ScatterRenderer>();
        public Dictionary<string, ScatterRenderer> fastScatterRenderers = new Dictionary<string, ScatterRenderer>();

        // Stored only active renderer components that need rendering
        public List<ScatterRenderer> activeScatterRenderers = new List<ScatterRenderer>();

        public static Texture2D biomeIndexMap;

        int lastEvaluatedFrame = -1;

        void Awake()
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(this);
            Camera.onPreCull += OnCameraPreCull;
            PQSStartPatch.onPQSStart += DominantBodyLoaded;
            PQSStartPatch.onPQSUnload += DominantBodyUnloaded;
            PQSStartPatch.onPQSRestart += DominantBodyRestarted;
        }
        void Start()
        {
            foreach (KeyValuePair<string, ParallaxScatterBody> body in ConfigLoader.parallaxScatterBodies)
            {
                // Initialize a new per-planet renderer manager
                GameObject perPlanetRenderer = new GameObject();
                GameObject.DontDestroyOnLoad(perPlanetRenderer);
                perPlanetRenderer.SetActive(false);

                // Now add a renderer for each scatter on this body and parent it to the per-planet GameObject
                // This includes shared scatters and adds them appropriately!
                foreach (KeyValuePair<string, Scatter> scatter in body.Value.scatters)
                {
                    ScatterRenderer renderer = perPlanetRenderer.AddComponent<ScatterRenderer>();
                    renderer.planetName = body.Key;
                    renderer.scatter = scatter.Value;
                    scatterRenderers.Add(renderer);
                    fastScatterRenderers.Add(scatter.Key, renderer);
                    scatter.Value.renderer = renderer;
                }

            }
        }
        void DominantBodyLoaded(string bodyName) => DominantBodyLoaded(bodyName, isRestart: false);
        void DominantBodyLoaded(string bodyName, bool isRestart)
        {
            ParallaxDebug.Log("[Scatter Manager] body loading " + bodyName);

            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                // Make sure to do this before calling CompileToTexture because otherwise it blocks
                // on the graphics thread being idle. KopernicusCBAttributeMapSO.CompileToTexture
                // creates a new texture and that can block the render thread for quite a bit when
                // large textures are used.
                foreach (Scatter scatter in ConfigLoader.parallaxScatterBodies[bodyName].fastScatters)
                {
                    scatter.InitShader();
                }

                if (!isRestart)
                {
                    UpdateBiomeIndexMap(FlightGlobals.GetBodyByName(bodyName).BiomeMap);
                }

                foreach (ScatterRenderer renderer in scatterRenderers)
                {
                    // Renderer body is the new one - enable it
                    if (renderer.planetName == bodyName)
                    {
                        //renderer.gameObject.SetActive(true);
                        renderer.Enable();
                        activeScatterRenderers.Add(renderer);
                    }
                }
            }
        }

        void DominantBodyUnloaded(string bodyName) => DominantBodyUnloaded(bodyName, isRestart: false);
        void DominantBodyUnloaded(string bodyName, bool isRestart)
        {
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                foreach (ScatterRenderer renderer in activeScatterRenderers)
                {
                    renderer.Disable();
                }
                activeScatterRenderers.Clear();

                foreach (Scatter scatter in ConfigLoader.parallaxScatterBodies[bodyName].fastScatters)
                {
                    scatter.UnloadShader();
                }

                ParallaxScatterBody body = ConfigLoader.parallaxScatterBodies[bodyName];

                if (!isRestart)
                {
                    body.UnloadTextures();
                }
            }
        }
        void UpdateBiomeIndexMap(CBAttributeMapSO biomeMap)
        {
            int width = biomeMap.Width;
            int height = biomeMap.Height;

            if (biomeIndexMap == null || biomeIndexMap.width != width || biomeIndexMap.height != height)
            {
                if (biomeIndexMap != null)
                    UnityEngine.Object.Destroy(biomeIndexMap);

                biomeIndexMap = new Texture2D(width, height, TextureFormat.R8, false, true);
                biomeIndexMap.filterMode = FilterMode.Point;
                biomeIndexMap.wrapMode = TextureWrapMode.Clamp;
            }

            biomeIndexMap.LoadRawTextureData(GetBiomeIndices(biomeMap));
            biomeIndexMap.Apply(false, false);
        }
        static byte[] GetBiomeIndices(CBAttributeMapSO biomeMap)
        {
            int width = biomeMap.Width;
            int height = biomeMap.Height;

            if (biomeMap is KopernicusCBAttributeMapSO && biomeMap._data.Length == width * height)
            {
                return biomeMap._data;
            }

            ParallaxDebug.Log("[Scatter Manager] Biome map " + biomeMap.MapName + " is not a Kopernicus biome map, matching biome colours per pixel");

            CBAttributeMapSO.MapAttribute[] attributes = biomeMap.Attributes;
            byte[] indices = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = biomeMap.GetPixelColor(x, y);
                    int nearestIndex = 0;
                    float nearestDistance = float.MaxValue;
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        Color biomeColor = attributes[i].mapColor;
                        float distance = new Vector3(pixel.r - biomeColor.r, pixel.g - biomeColor.g, pixel.b - biomeColor.b).sqrMagnitude;
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestIndex = i;
                        }
                    }
                    indices[x + y * width] = (byte)nearestIndex;
                }
            }
            return indices;
        }
        void DominantBodyRestarted(string bodyName)
        {
            DominantBodyUnloaded(bodyName, isRestart: true);
            DominantBodyLoaded(bodyName, isRestart: true);
        }
        void OnCameraPreCull(Camera cam)
        {
            if (lastEvaluatedFrame == Time.frameCount)
                return;

            FlightCamera flightCamera = FlightCamera.fetch;
            if (flightCamera == null || Array.IndexOf(flightCamera.cameras, cam) < 0)
                return;

            lastEvaluatedFrame = Time.frameCount;
            RuntimeOperations.UpdateCameraData();

            // Clear the buffers
            foreach (ScatterRenderer renderer in activeScatterRenderers)
            {
                renderer.PreRender();
            }

            // Update quad-camera distances
            // Then dispatch the evaluate compute shaders

            foreach (var data in ScatterComponent.scatterQuadData)
            {
                ScatterSystemQuadData quadData = data.Value;
                if (quadData.quad.isVisible && (quadData.quad.meshRenderer.isVisible || quadData.ignoreRendererVisibility))
                {
                    quadData.UpdateQuadCameraDistance(ref RuntimeOperations.vectorCameraPos);
                    quadData.EvaluateQuad();
                }
            }

            // Schedule drawing instanced data to screen

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
            {
                foreach (ScatterRenderer renderer in activeScatterRenderers)
                {
                    renderer.Render();
                }
            }
            else
            {
                foreach (ScatterRenderer renderer in activeScatterRenderers)
                {
                    renderer.RenderInCameras(FlightCamera.fetch.cameras);
                }
            }
        }
        public ScatterRenderer GetSharedScatterRenderer(SharedScatter scatter)
        {
            return fastScatterRenderers[scatter.parent.scatterName];
        }
        /// <summary>
        /// Called when the game is closed. Releases all buffers.
        /// </summary>
        void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            PQSStartPatch.onPQSStart -= DominantBodyLoaded;
            PQSStartPatch.onPQSUnload -= DominantBodyUnloaded;
            PQSStartPatch.onPQSRestart -= DominantBodyRestarted;

            int quadsWithColorBuffer = 0;
            ParallaxDebug.Log("Scatter Manager is being destroyed, cleaning up scatter system");
            foreach (ScatterSystemQuadData quadData in ScatterComponent.scatterQuadData.Values)
            {
                foreach (ScatterData scatterData in quadData.quadScatters)
                {
                    if (scatterData.scatter.distributionParams.coloredByTerrain)
                    {
                        quadsWithColorBuffer++;
                    }
                }
                quadData.Cleanup();
            }

            foreach (ScatterRenderer renderer in scatterRenderers)
            {
                renderer.ReleaseBuffers();
            }

            if (biomeIndexMap != null)
                UnityEngine.Object.Destroy(biomeIndexMap);

            ParallaxDebug.Log("Quads with color buffer: " +  quadsWithColorBuffer);
        }
    }
}
