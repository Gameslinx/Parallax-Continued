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
                foreach (KeyValuePair<string, Scatter> scatter in body.Value.scatters)
                {
                    ScatterRenderer renderer = perPlanetRenderer.AddComponent<ScatterRenderer>();
                    renderer.planetName = body.Key;
                    renderer.scatter = scatter.Value;
                    scatterRenderers.Add(renderer);
                    fastScatterRenderers.Add(scatter.Key, renderer);
                }

                Debug.Log("Init new manager body: " + body.Key);
            }
        }
        void DominantBodyLoaded(string bodyName)
        {
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                currentBiomeMap = FlightGlobals.GetBodyByName(bodyName).BiomeMap.CompileToTexture();
            }
            foreach (ScatterRenderer renderer in scatterRenderers)
            {
                // Renderer body is the new one - enable it
                if (renderer.planetName == bodyName)
                {
                    renderer.gameObject.SetActive(true);
                    activeScatterRenderers.Add(renderer);
                }
            }
        }
        void DominantBodyUnloaded(string bodyName)
        {
            foreach (ScatterRenderer renderer in activeScatterRenderers)
            {
                // Renderer body is not the new one - disable it
                if (renderer.planetName != bodyName)
                {
                    renderer.gameObject.SetActive(false);
                }
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
        // Create single channel 8 bpp biome map - stores indices of each biome 
        Texture2D CreateBiomeIndexMap(CBAttributeMapSO biomeMap)
        {
            Type type = biomeMap.GetType();
            FieldInfo fieldInfo = type.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            byte[] data = (byte[])fieldInfo.GetValue(biomeMap);

            Texture2D tex = new Texture2D(biomeMap.Width, biomeMap.Height, TextureFormat.R8, false);
            tex.LoadRawTextureData(data);
            tex.Apply(false, false);
            return tex;
        }
        void OnDestroy()
        {
            PQSStartPatch.onPQSStart -= DominantBodyLoaded;
            PQSStartPatch.onPQSUnload -= DominantBodyUnloaded;
        }
    }
}
