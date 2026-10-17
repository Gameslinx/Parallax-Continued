using Kopernicus;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Parallax.LateCompatibility
{
    /// <summary>
    /// Applies patches to Parallax where loading order does not permit Parallax to apply these compatibilities as part of its main loading process
    /// </summary>

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class SigmaDimensions : MonoBehaviour
    {
        public void Start()
        {
            RunCompatibility_SigmaDimensions();
        }

        /// <summary>
        /// Number of PQS subdivision levels to add so max level quads stay close to their unscaled size
        /// </summary>
        public static int GetMaxLevelRaise(float resizeValue)
        {
            return Mathf.RoundToInt(Mathf.Log(resizeValue, 2f));
        }

        /// <summary>
        /// Corrects scatter density for the leftover difference in quad area after raising the max level, between 0.5 and 2
        /// </summary>
        public static float GetDensityMultiplier(float resizeValue)
        {
            return resizeValue * resizeValue / Mathf.Pow(4f, GetMaxLevelRaise(resizeValue));
        }

        // PQS StartSphere reads maxLevel from these presets on every scene load so we need to update them first
        static void FixupPQSPresets()
        {
            HashSet<string> parallaxBodies = new HashSet<string>(ConfigLoader.parallaxTerrainBodies.Keys.Concat(ConfigLoader.parallaxScatterBodies.Keys));
            foreach (string bodyName in parallaxBodies)
            {
                CelestialBody cb = FlightGlobals.GetBodyByName(bodyName);
                if (cb == null || cb.pqsController == null)
                {
                    continue;
                }

                int maxLevelRaise = GetMaxLevelRaise((float)cb.Get<double>("resize"));
                if (maxLevelRaise == 0)
                {
                    continue;
                }

                // Includes the ocean sphere, which has its own preset
                foreach (PQS pqs in cb.pqsController.GetComponentsInChildren<PQS>(true))
                {
                    foreach (PQSCache.PQSPreset qualityPreset in PQSCache.PresetList.presets)
                    {
                        foreach (PQSCache.PQSSpherePreset spherePreset in qualityPreset.spherePresets)
                        {
                            if (spherePreset.name == pqs.name)
                            {
                                spherePreset.maxSubdivision = Math.Max(spherePreset.maxSubdivision + maxLevelRaise, 1);
                            }
                        }
                    }
                }

                ParallaxDebug.Log("[SigmaDimensions Compatibility] Raised PQS max level for " + bodyName + " by " + maxLevelRaise);
            }
        }

        public void RunCompatibility_SigmaDimensions()
        {
            UrlDir.UrlConfig sigDimConfig = ConfigLoader.GetConfigByName("SigmaDimensions");

            if (sigDimConfig == null)
            {
                return;
            }
            else
            {
                ParallaxDebug.Log("Sigma Dimensions detected, applying rescale values");
            }

            FixupPQSPresets();

            foreach (ParallaxScaledBody scaledBody in ConfigLoader.parallaxScaledBodies.Values)
            {
                CelestialBody cb = FlightGlobals.GetBodyByName(scaledBody.planetName);
                float resizeValue = (float)cb.Get<double>("resize");
                float landscapeValue = (float)cb.Get<double>("landscape");

                ParallaxDebug.Log("[SigmaDimensions Compatibility] Resize Value for " + scaledBody.planetName + " = " + resizeValue);
                ParallaxDebug.Log("[SigmaDimensions Compatibility] Landscape Value for " + scaledBody.planetName + " = " + landscapeValue);

                scaledBody.minTerrainAltitude *= resizeValue * landscapeValue;
                scaledBody.maxTerrainAltitude *= resizeValue * landscapeValue;

                // Just approximate the normal strength changes, it won't be perfect, but we can't derive it without regenerating the normals
                scaledBody.scaledMaterial.SetFloat("_PlanetBumpScale", Mathf.Pow(landscapeValue, 0.333f));

                if (scaledBody.mode == ParallaxScaledBodyMode.FromTerrain || scaledBody.mode == ParallaxScaledBodyMode.CustomRequiresTerrain)
                {
                    scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendStart"] *= resizeValue * landscapeValue;
                    scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendEnd"] *= resizeValue * landscapeValue;
                    scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendStart"] *= resizeValue * landscapeValue;
                    scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendEnd"] *= resizeValue * landscapeValue;

                    scaledBody.scaledMaterial.SetFloat("_LowMidBlendStart", scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendStart"]);
                    scaledBody.scaledMaterial.SetFloat("_LowMidBlendEnd", scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendEnd"]);
                    scaledBody.scaledMaterial.SetFloat("_MidHighBlendStart", scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendStart"]);
                    scaledBody.scaledMaterial.SetFloat("_MidHighBlendEnd", scaledBody.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendEnd"]);
                }
            }

            foreach (ParallaxTerrainBody terrainBody in ConfigLoader.parallaxTerrainBodies.Values)
            {
                CelestialBody cb = FlightGlobals.GetBodyByName(terrainBody.planetName);
                float resizeValue = (float)cb.Get<double>("resize");
                float landscapeValue = (float)cb.Get<double>("landscape");

                // Adjust fade ranges
                terrainBody.terrainShaderProperties.shaderFloats["_LowMidBlendStart"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_LowMidBlendEnd"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_MidHighBlendStart"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_MidHighBlendEnd"] *= resizeValue * landscapeValue;

                // Tessellation settings are left alone because the raised PQS max level keeps quads at their unscaled size
                terrainBody.SetMaterialValues();
            }

            foreach (ParallaxScatterBody scatterBody in ConfigLoader.parallaxScatterBodies.Values)
            {
                CelestialBody cb = FlightGlobals.GetBodyByName(scatterBody.planetName);
                float resizeValue = (float)cb.Get<double>("resize");
                float landscapeValue = (float)cb.Get<double>("landscape");
                float densityMultiplier = GetDensityMultiplier(resizeValue);

                foreach (Scatter scatter in scatterBody.fastScatters)
                {
                    scatter.distributionParams.minAltitude *= resizeValue * landscapeValue;
                    scatter.distributionParams.maxAltitude *= resizeValue * landscapeValue;

                    // Spawn chance is a probability, so it saturates at 1 and anything above that after scaling moves into the population multiplier
                    if (densityMultiplier != 1)
                    {
                        scatter.distributionParams.spawnChance = Mathf.Min(scatter.distributionParams.spawnChance, 1) * densityMultiplier;
                        if (scatter.distributionParams.spawnChance > 1)
                        {
                            // Stored so global density changes in the GUI can reapply it
                            int populationScale = Mathf.CeilToInt(scatter.distributionParams.spawnChance);
                            scatter.distributionParams.rescalePopulationMultiplier = populationScale;
                            scatter.distributionParams.populationMultiplier *= populationScale;
                            scatter.distributionParams.spawnChance /= populationScale;
                        }
                    }

                    // Adjust distribution noise frequency
                    scatter.noiseParams.frequency *= resizeValue;
                }
            }
        }
    }
}
