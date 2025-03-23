using Kopernicus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.LateCompatibility
{
    /// <summary>
    /// Applies patches to Parallax where loading order does not permit Parallax to apply these compatibilities as part of its main loading process
    /// </summary>

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class LateCompatibility : MonoBehaviour
    {
        public void Start()
        {
            RunCompatibility_SigmaDimensions();
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

            ConfigNode sigmaDimensionsNode = sigDimConfig.config;

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

                // Adjust subdivision settings
                PQSMod_Parallax parallaxPQSMod = (PQSMod_Parallax)cb.pqsController.mods.FirstOrDefault(mod => mod.GetType() == typeof(PQSMod_Parallax));
                
                // Subdiv level scales with log2(area) so just add 1 per power of 2 scale to the parallaxPQSMod.subdivisionLevel
                if (parallaxPQSMod != null)
                {
                    // Don't care about landscape value because that just scales altitude
                    // Plus 1 because subdivLevel is proportional to area
                    parallaxPQSMod.subdivisionLevel += Mathf.CeilToInt(Mathf.Log(resizeValue + 1, 2));
                    parallaxPQSMod.subdivisionRadius *= resizeValue;

                    // Unlikely to reach 14 but it gets slow if it does
                    // Fwiw rescale of 10 will only add 4, and Tylo uses the most at a depth of 9, so we'll hit 13.
                    parallaxPQSMod.subdivisionLevel = Mathf.Min(parallaxPQSMod.subdivisionLevel, 14);
                }

                terrainBody.SetMaterialValues();
            }

            foreach (ParallaxScatterBody scatterBody in ConfigLoader.parallaxScatterBodies.Values)
            {
                CelestialBody cb = FlightGlobals.GetBodyByName(scatterBody.planetName);
                float resizeValue = (float)cb.Get<double>("resize");
                float landscapeValue = (float)cb.Get<double>("landscape");

                foreach (Scatter scatter in scatterBody.fastScatters)
                {
                    scatter.distributionParams.minAltitude *= resizeValue * landscapeValue;
                    scatter.distributionParams.maxAltitude *= resizeValue * landscapeValue;

                    // Don't care about landscape value because that just scales altitude
                    // Limit density scalar to 5.5x scale - anything after that we take the density losses to conserve RAM (would be 100x at 10x scale)
                    float densityScalar = resizeValue;
                    densityScalar = Mathf.Min(densityScalar, 5.5f);

                    // Now scale density appropriately
                    // Pop mult scales with square of rescale factor - Floor to be conservative, 
                    scatter.distributionParams.populationMultiplier *= Mathf.FloorToInt(densityScalar * densityScalar);
                    scatter.distributionParams.populationMultiplier = Mathf.Max(scatter.distributionParams.populationMultiplier, 1);

                    // Adjust distribution noise frequency
                    scatter.noiseParams.frequency *= resizeValue;
                }
            }
        }
    }
}
