using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        // The current biome map
        public static Texture2D currentBiomeMap;

        void Awake()
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(this);
            PQSStartPatch.onPQSStart += DominantBodyLoaded;
            PQSStartPatch.onPQSUnload += DominantBodyUnloaded;
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
                    Debug.Log("Init new renderer: " + scatter.Value.scatterName);
                }

                Debug.Log("Init new manager body: " + body.Key);
            }
        }
        void DominantBodyLoaded(string bodyName)
        {
            Debug.Log("[Scatter Manager] body loading " + bodyName);
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                currentBiomeMap = FlightGlobals.GetBodyByName(bodyName).BiomeMap.CompileToTexture();
            }
            foreach (ScatterRenderer renderer in scatterRenderers)
            {
                // Renderer body is the new one - enable it
                if (renderer.planetName == bodyName)
                {
                    //renderer.gameObject.SetActive(true);
                    renderer.Enable();
                    Debug.Log("Renderer set active: " + renderer.scatter.scatterName);
                    activeScatterRenderers.Add(renderer);
                }
            }
        }
        void DominantBodyUnloaded(string bodyName)
        {
            Debug.Log("[Scatter Manager] body unloading " + bodyName);
            foreach (ScatterRenderer renderer in activeScatterRenderers)
            {
                // Renderer body is not the new one - disable it
                //if (renderer.planetName != bodyName)
                //{
                //renderer.gameObject.SetActive(false);
                Debug.Log("Renderer set inactive: " + renderer.scatter.scatterName);
                renderer.Disable();
                //}
            }
            activeScatterRenderers.Clear();

            // Could be more elegant but it's effectively a "is this first run?" check
            if (bodyName != "ParallaxFirstRunDoNotCallAPlanetThis")
            {
                ParallaxScatterBody body = ConfigLoader.parallaxScatterBodies[bodyName];
                body.UnloadTextures();
                UnityEngine.Object.Destroy(currentBiomeMap);
            }
        }
        // After any world origin shifts
        void LateUpdate()
        {
            foreach (ScatterRenderer renderer in activeScatterRenderers)
            {
                renderer.Render();
            }
        }
        public ScatterRenderer GetSharedScatterRenderer(SharedScatter scatter)
        {
            Debug.Log("Parent = " + scatter.parent.scatterName);
            return fastScatterRenderers[scatter.parent.scatterName];
        }
        void OnDestroy()
        {
            PQSStartPatch.onPQSStart -= DominantBodyLoaded;
            PQSStartPatch.onPQSUnload -= DominantBodyUnloaded;
        }
    }
}
