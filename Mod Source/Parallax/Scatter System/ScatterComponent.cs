using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// The main entry point to the scatter system
    /// </summary>
    public class ScatterComponent
    {
        public static Dictionary<PQ, ScatterSystemQuadData> scatterQuadData = new Dictionary<PQ, ScatterSystemQuadData>();

        /// <summary>
        /// The quad has finished building and is now visible - generate scatters on it.
        /// This is when a quad has subdivided, like when generating new terrain.
        /// </summary>
        public static void OnQuadVisibleBuilt(PQ quad)
        {
            // I'd rather not do this every time a quad is built but alas, we may need to
            // Necessary evil?
            if (ConfigLoader.parallaxScatterBodies.TryGetValue(quad.sphereRoot.name, out ParallaxScatterBody scatterBody) && quad.subdivision >= scatterBody.minimumSubdivisionLevel)
            {
                ScatterSystemQuadData scatterData = new ScatterSystemQuadData(scatterBody, quad);
                scatterData.Initialize();
                scatterQuadData.Add(quad, scatterData);
            }
        }
        /// <summary>
        /// The quad is already built and is now visible.
        /// This is usually when a quad is unloading and its lower subdivision parent is now visible.
        /// </summary>
        public static void OnQuadVisible(PQ quad)
        {
            OnQuadVisibleBuilt(quad);
            return;
            if (ConfigLoader.parallaxScatterBodies.TryGetValue(quad.sphereRoot.name, out ParallaxScatterBody scatterBody))
            {
                if (scatterQuadData.TryGetValue(quad, out ScatterSystemQuadData scatterData))
                {
                    // Quad visible code
                }
            }
        }
        /// <summary>
        /// The quad is invisible.
        /// This is usually when this quad has children which have become visible, so this one becomes hidden.
        /// </summary>
        public static void OnQuadInvisible(PQ quad)
        {
            OnQuadDestroyed(quad);
            return;
            // If this scatter body is eligible
            if (ConfigLoader.parallaxScatterBodies.TryGetValue(quad.sphereRoot.name, out ParallaxScatterBody scatterBody))
            {
                if (scatterQuadData.TryGetValue(quad, out ScatterSystemQuadData scatterData))
                {
                    // Quad invisible code
                }
            }
            
        }
        /// <summary>
        /// The quad is unloaded.
        /// This happens when a quad is too far away or the scene is changing, so it collapses.
        /// </summary>
        public static void OnQuadDestroyed(PQ quad)
        {
            if (ConfigLoader.parallaxScatterBodies.TryGetValue(quad.sphereRoot.name, out ParallaxScatterBody scatterBody))
            {
                // Required, as even checking against 'isBuilt' can lead to dictionary fetch failing
                if (scatterQuadData.TryGetValue(quad, out ScatterSystemQuadData scatterData))
                {
                    scatterData.Cleanup();
                    scatterQuadData.Remove(quad);
                }
            }
        }
    }
}
