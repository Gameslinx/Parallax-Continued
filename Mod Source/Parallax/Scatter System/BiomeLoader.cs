using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// Starts on PSystemSpawn and converts the biome names in the blacklist to biome colours
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class BiomeLoader : MonoBehaviour
    {
        void Start()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            // Process all bodies
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                string bodyName = body.name;
                // Process all scatter bodies if the body is included
                if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
                {
                    ParallaxScatterBody scatterBody = ConfigLoader.parallaxScatterBodies[bodyName];
                    CBAttributeMapSO.MapAttribute[] biomeMapAttributes = FlightGlobals.GetBodyByName(bodyName).BiomeMap.Attributes;

                    // Process all scatters on this scatter body
                    foreach (Scatter scatter in scatterBody.fastScatters)
                    {
                        BiomeBlacklistParams blacklist = scatter.biomeBlacklistParams;
                        List<string> blacklistedBiomes = blacklist.blacklistedBiomes;

                        int numberOfEligibleBiomes = biomeMapAttributes.Length - blacklistedBiomes.Count;

                        Texture2D biomeControltexture = new Texture2D(1, numberOfEligibleBiomes, TextureFormat.RGBA32, false);
                        int pixelIndex = 0;

                        foreach (CBAttributeMapSO.MapAttribute attribute in biomeMapAttributes)
                        {
                            Color biomeColor = attribute.mapColor;
                            string biomeName = attribute.name;

                            // This biome is eligible
                            if (!blacklistedBiomes.Contains(biomeName))
                            {
                                biomeControltexture.SetPixel(0, pixelIndex, biomeColor);
                                pixelIndex++;
                                Debug.Log("Biome eligible - " + biomeName);
                            }
                        }

                        Debug.Log("Scatter " + scatter.scatterName + " has " + numberOfEligibleBiomes + " biomes");
                        biomeControltexture.Apply(false, true);
                        scatter.biomeControlMap = biomeControltexture;
                        scatter.biomeCount = numberOfEligibleBiomes;
                    }
                }
            }
            stopwatch.Stop();

            // Usually around 0.5ms for Kerbin (has a lot of biomes) so this is acceptable performance
            Debug.Log("Biome processing took " + stopwatch.Elapsed.TotalMilliseconds + " ms");
        }
    }
}
