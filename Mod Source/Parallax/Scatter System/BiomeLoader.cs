using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSPTextureLoader;
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
            var options = new TextureLoadOptions
            {
                Linear = true,
                Unreadable = false,
                Hint = TextureLoadHint.Synchronous
            };
            var handle = TextureLoader
                .LoadTexture<Texture2D>("ParallaxContinued/Textures/PluginData/quadDensityMap.dds", options);
            ParallaxDebug.LogTextureLoaded(handle);
            Texture2D quadDensityMap = handle.TakeTexture();
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
                ParallaxDebug.Log("Biome Loader: Body: " + body.name);
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

        /// <summary>
        /// PQS is a subdivided cube-sphere. At the corners, the vertices are much closer together. This function accounts for that by approximating an appropriate reduction in density
        /// </summary>
        /// <param name="directionFromCenter"></param>
        /// <returns></returns>
        public static float GetSphereRelativeDensityMult(CelestialBody body, PQ quad)
        {
            UnityEngine.Vector2d latlon = LatLon.GetLatitudeAndLongitude(body.BodyFrame, body.gameObject.transform.position, quad.gameObject.transform.position);

            float normalisedDensityMultiplier = 1.0f - BiomeLoader.GetDensityAt(latlon);

            // Square it, as we're working with area
            normalisedDensityMultiplier = normalisedDensityMultiplier * normalisedDensityMultiplier;

            float multiplier = Mathf.Clamp01(Mathf.Lerp(0.18f, 1.0f, normalisedDensityMultiplier));

            return multiplier;
        }

        /// <summary>
        /// Get the relative subdivision multiplier.
        /// This calculates the relative area of quads compared to at latlon 0 0.
        /// However, subdiv divides area into 4 each level. When density is less than half, we need to back out a subdiv level (closer to 1/4).
        /// </summary>
        /// <param name="body"></param>
        /// <param name="quad"></param>
        /// <returns></returns>
        public static int GetSphereRelativeSubdivisionLevel(CelestialBody body, PQ quad, int subdivisionLevelIn)
        {
            UnityEngine.Vector2d latlon = LatLon.GetLatitudeAndLongitude(body.BodyFrame, body.gameObject.transform.position, quad.gameObject.transform.position);

            float normalisedDensityMultiplier = 1.0f - BiomeLoader.GetDensityAt(latlon);

            // Square it, as we're working with area
            normalisedDensityMultiplier = normalisedDensityMultiplier * normalisedDensityMultiplier;

            // The correct calculation is log_0.25 (densitymult / 2) then floor the result to get the amount we need to subtract
            // But in this case we'll never have quads more than 4x denser in one area than another, so we can just drop a subdiv level when the area mult is < 0.5
            if (normalisedDensityMultiplier < 0.5f)
            {
                return subdivisionLevelIn - 1;
            }
            else
            {
                return subdivisionLevelIn;
            }
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
