using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    public partial class ParallaxGUI
    {
        static void ScatterMenu()
        {
            // Reset window size
            if (!showDistribution && !showMaterial && !showDistributionNoise && !showScatterExporter && !showDebug)
            {
                window.height = windowDefault.height;
            }

            if (ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.collisionLevel > -1)
            {
                GUILayout.Label("Scatter editing is disabled while colliders are enabled");
                GUILayout.Label("Set collisionLevel to -1 in ParallaxGlobalSettings.cfg");
                return;
            }

            GUIStyle labelSkin = HighLogic.Skin.label;
            labelSkin.alignment = TextAnchor.MiddleCenter;

            // Show current scatter
            Scatter scatter = GetScatter();
            GUILayout.Label("Currently displaying scatter: " + scatter.scatterName, labelSkin);

            // Align correctly
            labelSkin.alignment = TextAnchor.MiddleLeft;

            ProcessDistributionParams(scatter);
            ProcessDistributionNoiseParams(scatter);
            ProcessBaseMaterialParams(scatter);

            ProcessDebug(scatter);
            ProcessSaveButton(scatter);
        }
        static Scatter GetScatter()
        {
            // Advance scatter
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Scatter", HighLogic.Skin.button, GUILayout.Width(214)))
            {
                currentScatterIndex = DecrementScatterIndex();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Next Scatter", HighLogic.Skin.button, GUILayout.Width(214)))
            {
                currentScatterIndex = IncrementScatterIndex();
            }
            GUILayout.EndHorizontal();
            return scatters[currentScatterIndex];
        }
        static void ProcessDistributionParams(Scatter scatter)
        {
            if (GUILayout.Button("Distribution Params", GetButtonColor(showDistribution)))
            {
                showDistribution = !showDistribution;
            }
            if (showDistribution)
            {
                ParamCreator.ChangeMethod callback = scatter.ReinitializeDistribution;
                if (!scatter.isShared)
                {
                    // Callback method that regenerates scatters when these values are changed

                    GUILayout.Label("Distribution Params: ", HighLogic.Skin.label);

                    // General params
                    ParamCreator.CreateParam("Seed", ref scatter.distributionParams.seed, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Spawn Chance", ref scatter.distributionParams.spawnChance, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Range", ref scatter.distributionParams.range, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Population Mult", ref scatter.distributionParams.populationMultiplier, GUIHelperFunctions.IntField, callback);

                    // Scale params
                    ParamCreator.CreateParam("Min Scale", ref scatter.distributionParams.minScale, GUIHelperFunctions.Vector3Field, callback);
                    ParamCreator.CreateParam("Max Scale", ref scatter.distributionParams.maxScale, GUIHelperFunctions.Vector3Field, callback);
                    ParamCreator.CreateParam("Scale Randomness", ref scatter.distributionParams.scaleRandomness, GUIHelperFunctions.FloatField, callback);

                    ParamCreator.CreateParam("Noise Cutoff", ref scatter.distributionParams.noiseCutoff, GUIHelperFunctions.FloatField, callback);

                    // Steep params
                    ParamCreator.CreateParam("Steep Power", ref scatter.distributionParams.steepPower, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Steep Contrast", ref scatter.distributionParams.steepContrast, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Steep Midpoint", ref scatter.distributionParams.steepMidpoint, GUIHelperFunctions.FloatField, callback);

                    ParamCreator.CreateParam("Max Normal Deviance", ref scatter.distributionParams.maxNormalDeviance, GUIHelperFunctions.FloatField, callback);

                    // Altitude params
                    ParamCreator.CreateParam("Min Altitude", ref scatter.distributionParams.minAltitude, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Max Altitude", ref scatter.distributionParams.maxAltitude, GUIHelperFunctions.FloatField, callback);
                    ParamCreator.CreateParam("Altitude Fade Range", ref scatter.distributionParams.altitudeFadeRange, GUIHelperFunctions.FloatField, callback);

                    // Mesh params
                    ParamCreator.CreateParam("Align To Terrain Normal", ref scatter.distributionParams.alignToTerrainNormal, GUIHelperFunctions.IntField, callback);
                    ParamCreator.CreateParam("Use Terrain Colour", ref scatter.distributionParams.coloredByTerrain, GUIHelperFunctions.BoolField, callback);
                }

                if (GUILayout.Button("LOD 1 Params", GetButtonColor(showLOD1)))
                {
                    showLOD1 = !showLOD1;
                }
                if (showLOD1)
                {
                    GUILayout.Label("LOD 1 Params: ", HighLogic.Skin.label);
                    if (!scatter.isShared)
                    {
                        ParamCreator.CreateParam("Range", ref scatter.distributionParams.lod1.range, GUIHelperFunctions.FloatField, callback);
                    }

                    if (GUILayout.Button("LOD 1 Material Override", GetButtonColor(showLOD1Material)))
                    {
                        showLOD1Material = !showLOD1Material;
                    }
                    if (showLOD1Material)
                    {
                        ProcessMaterialParams(scatter,
                            scatter.distributionParams.lod1.materialOverride,
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName],
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].instancedMaterialLOD1,
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].SetLOD1MaterialParams,
                            ref showLOD1Keywords,
                            false);
                    }
                }

                if (GUILayout.Button("LOD 2 Params", GetButtonColor(showLOD2)))
                {
                    showLOD2 = !showLOD2;
                }
                if (showLOD2)
                {
                    GUILayout.Label("LOD 2 Params: ", HighLogic.Skin.label);
                    if (!scatter.isShared)
                    {
                        ParamCreator.CreateParam("Range", ref scatter.distributionParams.lod2.range, GUIHelperFunctions.FloatField, callback);
                    }
                    if (GUILayout.Button("LOD 2 Material Override", GetButtonColor(showLOD2Material)))
                    {
                        showLOD2Material = !showLOD2Material;
                    }
                    if (showLOD2Material)
                    {
                        ProcessMaterialParams(scatter,
                            scatter.distributionParams.lod2.materialOverride,
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName],
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].instancedMaterialLOD2,
                            ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].SetLOD2MaterialParams,
                            ref showLOD2Keywords,
                            false);
                    }
                }
                GUILayout.Space(15);
            }
        }
        static void ProcessBaseMaterialParams(Scatter scatter)
        {
            if (GUILayout.Button("Material Params", GetButtonColor(showMaterial)))
            {
                showMaterial = !showMaterial;
            }
            if (showMaterial)
            {
                ProcessMaterialParams(scatter,
                    scatter.materialParams,
                    ScatterManager.Instance.fastScatterRenderers[scatter.scatterName],
                    ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].instancedMaterialLOD0,
                    ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].SetLOD0MaterialParams,
                    ref showLOD0Keywords,
                    true);

                GUILayout.Space(15);
            }
        }
        static void ProcessDistributionNoiseParams(Scatter scatter)
        {
            if (scatter.isShared) { return; }
            if (GUILayout.Button("Noise Params", GetButtonColor(showDistributionNoise)))
            {
                showDistributionNoise = !showDistributionNoise;
            }
            if (showDistributionNoise)
            {
                // Callback method that regenerates scatters when these values are changed
                ParamCreator.ChangeMethod callback = scatter.ReinitializeDistribution;

                GUILayout.Label("Distribution Params: ", HighLogic.Skin.label);

                int noiseType = (int)scatter.noiseParams.noiseType;
                if (ParamCreator.CreateParam("Noise Type (" + ((NoiseType)noiseType).ToString() + ")", ref noiseType, GUIHelperFunctions.IntField))
                {
                    scatter.noiseParams.noiseType = (NoiseType)noiseType;
                    callback();
                }

                ParamCreator.CreateParam("Seed", ref scatter.noiseParams.seed, GUIHelperFunctions.IntField, callback);
                ParamCreator.CreateParam("Frequency", ref scatter.noiseParams.frequency, GUIHelperFunctions.FloatField, callback);
                ParamCreator.CreateParam("Lacunarity", ref scatter.noiseParams.lacunarity, GUIHelperFunctions.FloatField, callback);
                ParamCreator.CreateParam("Octaves", ref scatter.noiseParams.octaves, GUIHelperFunctions.IntField, callback);
                ParamCreator.CreateParam("Inverted", ref scatter.noiseParams.inverted, GUIHelperFunctions.BoolField, callback);

                GUILayout.Space(15);
            }
        }
        // Very ugly method which used to be quite elegant until I needed to parse keywords while keeping LOD material overrides consistent
        static void ProcessMaterialParams(Scatter scatter, in MaterialParams materialParams, ScatterRenderer renderer, Material material, ParamCreator.ChangeMethod callback, ref bool keywordBool, bool isBaseMaterial)
        {
            GUILayout.Label("Material Params: ", HighLogic.Skin.label);
            ShaderProperties properties = materialParams.shaderProperties;

            ProcessGenericMaterialParams(materialParams, callback);

            // Process keywords
            // This requires reading configs which can be slow, since we don't store keywords anywhere except ones that are already enabled
            if (GUILayout.Button("Show Keywords", GetButtonColor(keywordBool)))
            {
                keywordBool = !keywordBool;
            }
            if (keywordBool)
            {

                GUILayout.Label("Keywords", HighLogic.Skin.label);
                GUILayout.Label("Warning: The keywords menu has a significant performance impact!");
                // Get shader name
                string shaderName = materialParams.shader;

                // The linq incident
                // Gets "ParallaxShader" node
                ConfigNode config = GameDatabase.Instance.GetConfigs("ParallaxScatterShaderProperties").FirstOrDefault().config.GetNodes("ParallaxShader").Where(x => x.GetValue("name") == shaderName).FirstOrDefault();
                ConfigNode[] keywords = config.GetNode("Keywords").GetNodes();
                foreach (ConfigNode node in keywords)
                {
                    // These are our keywords
                    string keywordName = node.name;
                    bool enabled = materialParams.shaderKeywords.Contains(keywordName);
                    bool wasChanged = ParamCreator.CreateParam(keywordName, ref enabled, GUIHelperFunctions.BoolField);
                    if (wasChanged)
                    {
                        if (!enabled)
                        {
                            materialParams.shaderKeywords.Remove(keywordName);
                            material.DisableKeyword(keywordName);
                            // Remove the keyword specific params from the properties list, as they are unused
                            // and remove duplicates when the keyword is re-enabled again
                            RemoveKeywordValues(materialParams.shaderProperties, node);

                            // If the lod is a material override, then update its keywords as well
                            if (isBaseMaterial && scatter.distributionParams.lod1.inheritsMaterial)
                            {
                                RemoveKeywordValues(scatter.distributionParams.lod1.materialOverride.shaderProperties, node);
                                scatter.distributionParams.lod1.materialOverride.shaderKeywords.Remove(keywordName);
                                renderer.instancedMaterialLOD1.DisableKeyword(keywordName);
                                renderer.SetLOD1MaterialParams();
                            }
                            if (isBaseMaterial && scatter.distributionParams.lod2.inheritsMaterial)
                            {
                                RemoveKeywordValues(scatter.distributionParams.lod2.materialOverride.shaderProperties, node);
                                scatter.distributionParams.lod2.materialOverride.shaderKeywords.Remove(keywordName);
                                renderer.instancedMaterialLOD2.DisableKeyword(keywordName);
                                renderer.SetLOD2MaterialParams();
                            }
                        }
                        if (enabled)
                        {
                            materialParams.shaderKeywords.Add(keywordName);
                            // Now we need to initialize defaults for the keywords
                            ConfigLoader.InitializeTemplateConfig(node, materialParams.shaderProperties);

                            // If the lod is a material override, then update its keywords as well
                            if (isBaseMaterial && scatter.distributionParams.lod1.inheritsMaterial)
                            {
                                scatter.distributionParams.lod1.materialOverride.shaderKeywords.Add(keywordName);
                                ConfigLoader.InitializeTemplateConfig(node, scatter.distributionParams.lod1.materialOverride.shaderProperties);
                                renderer.instancedMaterialLOD1.EnableKeyword(keywordName);
                                renderer.SetLOD1MaterialParams();
                            }
                            if (isBaseMaterial && scatter.distributionParams.lod2.inheritsMaterial)
                            {
                                scatter.distributionParams.lod2.materialOverride.shaderKeywords.Add(keywordName);
                                ConfigLoader.InitializeTemplateConfig(node, scatter.distributionParams.lod2.materialOverride.shaderProperties);
                                renderer.instancedMaterialLOD2.EnableKeyword(keywordName);
                                renderer.SetLOD2MaterialParams();
                            }
                        }
                        callback();
                    }
                }
            }
        }
        static void ProcessDebug(Scatter scatter)
        {
            if (GUILayout.Button("Debug Settings", GetButtonColor(showDebug)))
            {
                showDebug = !showDebug;
            }
            if (showDebug)
            {
                GUILayout.Label("Debug Settings", HighLogic.Skin.label);
                if (ParamCreator.CreateParam("Show Face Orientation", ref debugShowFaceOrientation, GUIHelperFunctions.BoolField))
                {
                    ShowFaceOrientation(debugShowFaceOrientation, scatter);
                }
                if (ParamCreator.CreateParam("Show Collideable Scatters", ref debugShowCollideables, GUIHelperFunctions.BoolField))
                {
                    ShowCollideableScatters(debugShowCollideables);
                }
                if (GUILayout.Button("Log Performance Stats", HighLogic.Skin.button))
                {
                    LogPerformanceStats();
                }
            }
        }
        static void ShowFaceOrientation(bool enabled, Scatter scatter)
        {
            ScatterRenderer renderer = ScatterManager.Instance.fastScatterRenderers[scatter.scatterName];
            if (enabled)
            {
                renderer.instancedMaterialLOD0.EnableKeyword("DEBUG_FACE_ORIENTATION");
                renderer.instancedMaterialLOD1.EnableKeyword("DEBUG_FACE_ORIENTATION");
                renderer.instancedMaterialLOD2.EnableKeyword("DEBUG_FACE_ORIENTATION");
            }
            else
            {
                renderer.instancedMaterialLOD0.DisableKeyword("DEBUG_FACE_ORIENTATION");
                renderer.instancedMaterialLOD1.DisableKeyword("DEBUG_FACE_ORIENTATION");
                renderer.instancedMaterialLOD2.DisableKeyword("DEBUG_FACE_ORIENTATION");
            }
        }
        public static void ShowCollideableScatters(bool enabled)
        {
            if (enabled)
            {
                foreach (Scatter scatter in scatters)
                {
                    if (scatter.renderer.instancedMaterialLOD0.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD0.SetFloat("_FresnelPower", 1);
                    }
                    if (scatter.renderer.instancedMaterialLOD1.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD1.SetFloat("_FresnelPower", 1);
                    }
                    if (scatter.renderer.instancedMaterialLOD2.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD2.SetFloat("_FresnelPower", 1);
                    }

                    Color col = scatter.collideable ? new Color(0.2f, 1.0f, 0.2f) : new Color(1.0f, 0.2f, 0.2f);
                    if (scatter.renderer.instancedMaterialLOD0.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD0.SetColor("_FresnelColor", col);
                    }
                    if (scatter.renderer.instancedMaterialLOD1.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD1.SetColor("_FresnelColor", col);
                    }
                    if (scatter.renderer.instancedMaterialLOD2.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD2.SetColor("_FresnelColor", col);
                    }

                    scatter.renderer.instancedMaterialLOD0.SetColor("_Color", col);
                    scatter.renderer.instancedMaterialLOD1.SetColor("_Color", col);
                    scatter.renderer.instancedMaterialLOD2.SetColor("_Color", col);
                }
            }
            else
            {
                foreach (Scatter scatter in scatters)
                {
                    if (scatter.renderer.instancedMaterialLOD0.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD0.SetFloat("_FresnelPower", scatter.materialParams.shaderProperties.shaderFloats["_FresnelPower"]);
                    }
                    if (scatter.renderer.instancedMaterialLOD1.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD1.SetFloat("_FresnelPower", scatter.distributionParams.lod1.materialOverride.shaderProperties.shaderFloats["_FresnelPower"]);
                    }
                    if (scatter.renderer.instancedMaterialLOD2.HasProperty("_FresnelPower"))
                    {
                        scatter.renderer.instancedMaterialLOD2.SetFloat("_FresnelPower", scatter.distributionParams.lod2.materialOverride.shaderProperties.shaderFloats["_FresnelPower"]);
                    }

                    if (scatter.renderer.instancedMaterialLOD0.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD0.SetColor("_FresnelColor", scatter.materialParams.shaderProperties.shaderColors["_FresnelColor"]);
                    }
                    if (scatter.renderer.instancedMaterialLOD1.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD1.SetColor("_FresnelColor", scatter.distributionParams.lod1.materialOverride.shaderProperties.shaderColors["_FresnelColor"]);
                    }
                    if (scatter.renderer.instancedMaterialLOD2.HasProperty("_FresnelColor"))
                    {
                        scatter.renderer.instancedMaterialLOD2.SetColor("_FresnelColor", scatter.distributionParams.lod2.materialOverride.shaderProperties.shaderColors["_FresnelColor"]);
                    }

                    scatter.renderer.instancedMaterialLOD0.SetColor("_Color", scatter.materialParams.shaderProperties.shaderColors["_Color"]);
                    scatter.renderer.instancedMaterialLOD1.SetColor("_Color", scatter.distributionParams.lod2.materialOverride.shaderProperties.shaderColors["_Color"]);
                    scatter.renderer.instancedMaterialLOD2.SetColor("_Color", scatter.distributionParams.lod2.materialOverride.shaderProperties.shaderColors["_Color"]);
                }
            }
        }
        static void LogPerformanceStats()
        {
            int numTris = 0;
            foreach (Scatter scatter in scatters)
            {
                ScatterRenderer scatterRenderer = scatter.renderer;

                numTris += scatterRenderer.LogStats();
            }

            ParallaxDebug.Log("");
            ParallaxDebug.Log("Total number of triangles being rendered right now by Parallax: " + numTris);
            ParallaxDebug.Log("Performance logging complete");
        }
        static void RemoveKeywordValues(ShaderProperties shaderProperties, ConfigNode keywordNode)
        {
            ConfigNode[] allTypeNodes = keywordNode.GetNodes();
            foreach (ConfigNode node in allTypeNodes)
            {
                string[] keys = node.GetValues("name");
                foreach (string key in keys)
                {
                    // Blindly just remove them all. Shader properties can't share the same name, so this is safe
                    shaderProperties.shaderTextures.Remove(key);
                    shaderProperties.shaderFloats.Remove(key);
                    shaderProperties.shaderInts.Remove(key);
                    shaderProperties.shaderVectors.Remove(key);
                    shaderProperties.shaderColors.Remove(key);
                }
            }
        }
        static int IncrementScatterIndex()
        {
            int newIndex = currentScatterIndex + 1;
            if (newIndex >= scatters.Length)
            {
                newIndex = 0;
            }
            return newIndex;
        }
        static int DecrementScatterIndex()
        {
            int newIndex = currentScatterIndex - 1;
            if (newIndex < 0)
            {
                newIndex = scatters.Length - 1;
            }
            return newIndex;
        }
    }
}
