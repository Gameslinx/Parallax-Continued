using System;
using System.Collections.Generic;
using System.Linq;
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

                ScatterRenderer renderer = perPlanetRenderer.AddComponent<ScatterRenderer>();
                renderer.planetName = body.Key;
                scatterRenderers.Add(renderer);
                fastScatterRenderers.Add(body.Key, renderer);

                Debug.Log("Init new manager body: " + body.Key);
            }
        }
        void DominantBodyLoaded(string bodyName)
        {
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
        }
        // After any world origin shifts
        void LateUpdate()
        {
            Debug.Log("Manager: LateUpdate");
            foreach (ScatterRenderer renderer in activeScatterRenderers)
            {
                renderer.Render();
            }
        }
        void OnDestroy()
        {
            PQSStartPatch.onPQSStart -= DominantBodyLoaded;
            PQSStartPatch.onPQSUnload -= DominantBodyUnloaded;
        }
    }
}
