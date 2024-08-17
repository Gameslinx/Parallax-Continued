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
        private static float[,] quadDensityData;
        void Start()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            // Get the quad density map
            Texture2D quadDensityMap = TextureLoader.LoadTexture("Parallax/Textures/PluginData/quadDensityMap.dds", true, false);
            quadDensityData = new float[quadDensityMap.width, quadDensityMap.height];

            // Copy data from texture to 2d float array for speed
            for (int i = 0; i < quadDensityMap.width; i++)
            {
                for (int j = 0; j < quadDensityMap.height; j++)
                {
                    quadDensityData[i, j] = quadDensityMap.GetPixel(i, j).grayscale;
                }
            }

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
                            }
                        }

                        Debug.Log("Scatter " + scatter.scatterName + " has " + numberOfEligibleBiomes + " biomes");
                        // We read this again in ScatterSystemQuadData.cs
                        biomeControltexture.Apply(false, false);
                        scatter.biomeControlMap = biomeControltexture;
                        scatter.biomeCount = numberOfEligibleBiomes;
                    }
                }
            }
            stopwatch.Stop();

            ParallaxDebug.Log("Biome processing took " + stopwatch.Elapsed.TotalMilliseconds + " ms");
        }

        public static float GetDensityAt(UnityEngine.Vector2d latlon)
        {
            Vector2Int pixelCoords = LatLonToEquirectangularPixelCoord(latlon, quadDensityData.GetLength(0), quadDensityData.GetLength(1));
            return Mathf.Clamp01(quadDensityData[pixelCoords.x, pixelCoords.y]);
        }

        // Converts a normalized direction to 2D array indices (x, y)
        public static Vector2Int LatLonToEquirectangularPixelCoord(UnityEngine.Vector2d latlon, int textureWidth, int textureHeight)
        {
            float x = (((float)latlon.y + 180.0f) / 360.0f) * (float)textureWidth;
            float y = ((90.0f - (float)latlon.x) / 180.0f) * (float)textureHeight;

            // Ensure pixel coordinates are within texture bounds
            x = Mathf.Clamp(x, 0, textureWidth - 1);
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            return new Vector2Int((int)x, (int)y);
        }
    }
}
