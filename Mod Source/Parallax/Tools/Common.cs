using Kopernicus.Configuration;
using LibNoise.Models;
using Parallax.Harmony_Patches;
using Parallax.Scaled_System;
using Parallax.Tools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using static System.Net.Mime.MediaTypeNames;

namespace Parallax
{
    //
    //  Loader Common
    //

    // Holds ParallaxGlobalSettings.cfg values for terrain shader and scatters
    public class ParallaxSettings
    {
        public TerrainGlobalSettings terrainGlobalSettings = TerrainGlobalSettings.Default;
        public ScatterGlobalSettings scatterGlobalSettings = ScatterGlobalSettings.Default;
        public LightingGlobalSettings lightingGlobalSettings = LightingGlobalSettings.Default;
        public ScaledGlobalSettings scaledGlobalSettings = ScaledGlobalSettings.Default;
        public DebugGlobalSettings debugGlobalSettings = DebugGlobalSettings.Default;
        public ObjectPoolSettings objectPoolSettings = ObjectPoolSettings.Default;

        // Logs all global settings using reflection
        public void LogAll()
        {
            Type type = typeof(ParallaxSettings);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            ParallaxDebug.Log("Parallax Global Settings:");
            foreach (FieldInfo field in fields)
            {
                object fieldValue = field.GetValue(this);
                Type fieldType = field.FieldType;
                FieldInfo[] structFields = fieldType.GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (FieldInfo structField in structFields)
                {
                    object value = structField.GetValue(fieldValue);
                    ParallaxDebug.Log($"  {structField.Name} = {value}");
                }
                ParallaxDebug.Log("");
            }
        }
        public void SaveSettings()
        {
            ConfigNode rootNode = new ConfigNode("ParallaxGlobal");
            ConfigNode globalNode = new ConfigNode("ParallaxGlobal");
            rootNode.AddNode(globalNode);

            ConfigNode terrainShaderSettingsNode = new ConfigNode("TerrainShaderSettings");
            ConfigNode scatterSystemSettingsNode = new ConfigNode("ScatterSystemSettings");
            ConfigNode lightingSettingsNode = new ConfigNode("LightingSettings");
            ConfigNode scaledSettingsNode = new ConfigNode("ScaledSystemSettings");
            ConfigNode debugSettingsNode = new ConfigNode("DebugSettings");
            ConfigNode objectPoolSettingsNode = new ConfigNode("ObjectPoolSettings");

            terrainShaderSettingsNode.AddValue("maxTessellation", terrainGlobalSettings.maxTessellation);
            terrainShaderSettingsNode.AddValue("tessellationEdgeLength", terrainGlobalSettings.tessellationEdgeLength);
            terrainShaderSettingsNode.AddValue("maxTessellationRange", terrainGlobalSettings.maxTessellationRange);
            terrainShaderSettingsNode.AddValue("useAdvancedTextureBlending", terrainGlobalSettings.advancedTextureBlending);
            terrainShaderSettingsNode.AddValue("useAmbientOcclusion", terrainGlobalSettings.ambientOcclusion);

            scatterSystemSettingsNode.AddValue("densityMultiplier", scatterGlobalSettings.densityMultiplier);
            scatterSystemSettingsNode.AddValue("rangeMultiplier", scatterGlobalSettings.rangeMultiplier);
            scatterSystemSettingsNode.AddValue("fadeOutStartRange", scatterGlobalSettings.fadeOutStartRange);
            scatterSystemSettingsNode.AddValue("collisionLevel", scatterGlobalSettings.collisionLevel);
            scatterSystemSettingsNode.AddValue("colliderLookaheadTime", scatterGlobalSettings.colliderLookaheadTime);

            lightingSettingsNode.AddValue("lightShadows", lightingGlobalSettings.lightShadows);
            lightingSettingsNode.AddValue("lightShadowQuality", lightingGlobalSettings.lightShadowsQuality.ToString());

            scaledSettingsNode.AddValue("scaledSpaceShadows", scaledGlobalSettings.scaledSpaceShadows);
            scaledSettingsNode.AddValue("smoothScaledSpaceShadows", scaledGlobalSettings.smoothScaledSpaceShadows);
            scaledSettingsNode.AddValue("scaledRaymarchedShadowStepCount", scaledGlobalSettings.scaledRaymarchedShadowStepCount);
            scaledSettingsNode.AddValue("loadTexturesImmediately", scaledGlobalSettings.loadTexturesImmediately);

            debugSettingsNode.AddValue("wireframeTerrain", debugGlobalSettings.wireframeTerrain);
            debugSettingsNode.AddValue("suppressCriticalMessages", debugGlobalSettings.suppressCriticalMessages);

            objectPoolSettingsNode.AddValue("cachedColliderCount", objectPoolSettings.cachedColliderCount);

            globalNode.AddNode(terrainShaderSettingsNode);
            globalNode.AddNode(scatterSystemSettingsNode);
            globalNode.AddNode(lightingSettingsNode);
            globalNode.AddNode(scaledSettingsNode);
            globalNode.AddNode(debugSettingsNode);
            globalNode.AddNode(objectPoolSettingsNode);
            
            rootNode.Save(KSPUtil.ApplicationRootPath + "GameData/ParallaxContinued/Config/ParallaxGlobalSettings.cfg");
        }
    }
    public struct TerrainGlobalSettings
    {
        public float maxTessellation;
        public float tessellationEdgeLength;
        public float maxTessellationRange;
        public bool advancedTextureBlending;
        public bool ambientOcclusion;

        private static readonly TerrainGlobalSettings defaultSetting = new TerrainGlobalSettings()
        {
            maxTessellation = 64,
            tessellationEdgeLength = 4,
            maxTessellationRange = 30,
            advancedTextureBlending = true,
            ambientOcclusion = true
        };
        public static TerrainGlobalSettings Default => defaultSetting;
    }
    public struct ScatterGlobalSettings
    {
        public float densityMultiplier;
        public float rangeMultiplier;
        public float fadeOutStartRange;
        public int collisionLevel;
        public float colliderLookaheadTime;

        private static readonly ScatterGlobalSettings defaultSetting = new ScatterGlobalSettings()
        {
            densityMultiplier = 1,
            rangeMultiplier = 1,
            fadeOutStartRange = 0.8f,
            collisionLevel = 2,
            colliderLookaheadTime = 0
        };

        public static ScatterGlobalSettings Default => defaultSetting;
    }
    public struct LightingGlobalSettings
    {
        public bool lightShadows;
        public LightShadowResolution lightShadowsQuality;

        private static readonly LightingGlobalSettings defaultSetting = new LightingGlobalSettings()
        {
            lightShadows = true,
            lightShadowsQuality = LightShadowResolution.Medium
        };

        public static LightingGlobalSettings Default => defaultSetting;
    }
    public struct ScaledGlobalSettings
    {
        public bool scaledSpaceShadows;
        public bool smoothScaledSpaceShadows;
        public int scaledRaymarchedShadowStepCount;
        public bool loadTexturesImmediately;

        private static readonly ScaledGlobalSettings defaultSetting = new ScaledGlobalSettings()
        {
            scaledSpaceShadows = true,
            smoothScaledSpaceShadows = true,
            scaledRaymarchedShadowStepCount = 48,
            loadTexturesImmediately = false
        };

        public static ScaledGlobalSettings Default => defaultSetting;
    }
    public struct DebugGlobalSettings
    {
        public bool wireframeTerrain;
        public bool suppressCriticalMessages;

        private static readonly DebugGlobalSettings defaultSetting = new DebugGlobalSettings()
        {
            wireframeTerrain = false,
            suppressCriticalMessages = false
        };

        public static DebugGlobalSettings Default => defaultSetting;
    }
    public struct ObjectPoolSettings
    {
        public int cachedColliderCount;

        private static readonly ObjectPoolSettings defaultSetting = new ObjectPoolSettings()
        {
            cachedColliderCount = 1000
        };

        public static ObjectPoolSettings Default => defaultSetting;
    }
    // Stores the loaded values from the configs for each planet, except for the textures which are stored via file path
    // Textures are loaded On-Demand and stored in loadedTextures, where they are unloaded on scene change
    public class ParallaxTerrainBody
    {
        public string planetName;
        public Dictionary<string, TextureLoadManager.TextureHandle<Texture2D>> loadedTextures = [];

        // Terrain materials
        public ParallaxMaterials parallaxMaterials = new ParallaxMaterials();

        public ShaderProperties terrainShaderProperties;
        public bool emissive = false;

        private bool loaded = false;
        public bool Loaded
        {
            get { return loaded; }
        }
        private bool isLoading = false;
        public bool IsLoading
        {
            get { return isLoading; }
        }
        public ParallaxTerrainBody(string planetName)
        {
            this.planetName = planetName;
        }
        public ConfigNode ToConfigNode()
        {
            ConfigNode node = new ConfigNode("Body");
            node.AddValue("name", planetName);
            node.AddValue("emissive", emissive);

            ConfigNode materialNode = node.AddNode("ShaderProperties");

            foreach (KeyValuePair<string, string> texturePair in terrainShaderProperties.shaderTextures)
            {
                materialNode.AddValue(texturePair.Key, texturePair.Value);
            }

            foreach (KeyValuePair<string, float> floatPair in terrainShaderProperties.shaderFloats)
            {
                materialNode.AddValue(floatPair.Key, floatPair.Value);
            }

            foreach (KeyValuePair<string, Vector3> vectorPair in terrainShaderProperties.shaderVectors)
            {
                materialNode.AddValue(vectorPair.Key, vectorPair.Value);
            }

            foreach (KeyValuePair<string, Color> colorPair in terrainShaderProperties.shaderColors)
            {
                materialNode.AddValue(colorPair.Key, colorPair.Value);
            }

            foreach (KeyValuePair<string, int> intPair in terrainShaderProperties.shaderInts)
            {
                materialNode.AddValue(intPair.Key, intPair.Value);
            }

            return node;
        }
        // Create materials and set most properties, except the textures which use load on demand
        public void LoadInitial()
        {
            Material baseMaterial = new Material(AssetBundleLoader.parallaxTerrainShaders["Custom/Parallax"]);
            baseMaterial.EnableKeyword("INFLUENCE_MAPPING");

            foreach (KeyValuePair<string, float> floatValue in terrainShaderProperties.shaderFloats)
            {
                baseMaterial.SetFloat(floatValue.Key, floatValue.Value);
            }
            foreach (KeyValuePair<string, Vector3> vectorValue in terrainShaderProperties.shaderVectors)
            {
                baseMaterial.SetVector(vectorValue.Key, vectorValue.Value);
            }
            foreach (KeyValuePair<string, Color> colorValue in terrainShaderProperties.shaderColors)
            {
                baseMaterial.SetColor(colorValue.Key, colorValue.Value);
            }
            foreach (KeyValuePair<string, int> intValue in terrainShaderProperties.shaderInts)
            {
                baseMaterial.SetInt(intValue.Key, intValue.Value);
            }

            baseMaterial.SetFloat("_MaxTessellation", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation);
            baseMaterial.SetFloat("_TessellationEdgeLength", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength);
            baseMaterial.SetFloat("_MaxTessellationRange", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange);

            // Instantiate materials - Keywords are set in the Parallax PQSMod
            // These are then updated at runtime with the incoming textures
            parallaxMaterials.parallaxLow = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxMid = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxHigh = UnityEngine.Object.Instantiate(baseMaterial);

            parallaxMaterials.parallaxLowMid = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxMidHigh = UnityEngine.Object.Instantiate(baseMaterial);

            parallaxMaterials.parallaxFull = UnityEngine.Object.Instantiate(baseMaterial);
        }
        /// <summary>
        /// Used by the GUI to set values at runtime
        /// </summary>
        public void SetMaterialValues()
        {
            foreach (KeyValuePair<string, float> floatPair in terrainShaderProperties.shaderFloats)
            {
                parallaxMaterials.parallaxLow.SetFloat(floatPair.Key, floatPair.Value);
                parallaxMaterials.parallaxMid.SetFloat(floatPair.Key, floatPair.Value);
                parallaxMaterials.parallaxHigh.SetFloat(floatPair.Key, floatPair.Value);
                parallaxMaterials.parallaxLowMid.SetFloat(floatPair.Key, floatPair.Value);
                parallaxMaterials.parallaxMidHigh.SetFloat(floatPair.Key, floatPair.Value);
                parallaxMaterials.parallaxFull.SetFloat(floatPair.Key, floatPair.Value);
            }
            foreach (KeyValuePair<string, Vector3> vectorPair in terrainShaderProperties.shaderVectors)
            {
                parallaxMaterials.parallaxLow.SetVector(vectorPair.Key, vectorPair.Value);
                parallaxMaterials.parallaxMid.SetVector(vectorPair.Key, vectorPair.Value);
                parallaxMaterials.parallaxHigh.SetVector(vectorPair.Key, vectorPair.Value);
                parallaxMaterials.parallaxLowMid.SetVector(vectorPair.Key, vectorPair.Value);
                parallaxMaterials.parallaxMidHigh.SetVector(vectorPair.Key, vectorPair.Value);
                parallaxMaterials.parallaxFull.SetVector(vectorPair.Key, vectorPair.Value);
            }
            foreach (KeyValuePair<string, Color> colorPair in terrainShaderProperties.shaderColors)
            {
                parallaxMaterials.parallaxLow.SetColor(colorPair.Key, colorPair.Value);
                parallaxMaterials.parallaxMid.SetColor(colorPair.Key, colorPair.Value);
                parallaxMaterials.parallaxHigh.SetColor(colorPair.Key, colorPair.Value);
                parallaxMaterials.parallaxLowMid.SetColor(colorPair.Key, colorPair.Value);
                parallaxMaterials.parallaxMidHigh.SetColor(colorPair.Key, colorPair.Value);
                parallaxMaterials.parallaxFull.SetColor(colorPair.Key, colorPair.Value);
            }
        }

        internal void StartLoadingTextures()
        {
            foreach (var (name, path) in terrainShaderProperties.shaderTextures)
            {
                if (loadedTextures.ContainsKey(name))
                    continue;

                var options = new TextureLoadOptions
                {
                    linear = TextureUtils.IsLinear(name),
                    unreadable = true
                };
                loadedTextures.Add(name, TextureLoadManager.LoadTexture(path, options));
            }
        }

        public void Load()
        {
            if (loaded)
            {
                return;
            }
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            StartLoadingTextures();

            foreach (var name in terrainShaderProperties.shaderTextures.Keys)
            {
                Texture2D tex;
                var request = loadedTextures[name];

                try
                {
                    tex = request.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {request.Path}");
                    Debug.LogException(e);
                    continue;
                }

                // Bump maps need to be linear, while everything else sRGB
                // This could be handled better, tbh, but at least we're accounting for linear textures this time around

                parallaxMaterials.parallaxLow.SetTexture(name, tex);
                parallaxMaterials.parallaxMid.SetTexture(name, tex);
                parallaxMaterials.parallaxHigh.SetTexture(name, tex);

                parallaxMaterials.parallaxLowMid.SetTexture(name, tex);
                parallaxMaterials.parallaxMidHigh.SetTexture(name, tex);

                parallaxMaterials.parallaxFull.SetTexture(name, tex);
            }

            loaded = true;
        }
        public IEnumerator LoadAsync()
        {
            isLoading = true;
            if (loaded)
            {
                isLoading = false;
                yield break;
            }

            StartLoadingTextures();

            foreach (var name in terrainShaderProperties.shaderTextures.Keys)
            {
                Texture2D tex;
                var request = loadedTextures[name];

                yield return request.Wait();
                try
                {
                    tex = request.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {request.Path}");
                    Debug.LogException(e);
                    continue;
                }

                // Bump maps need to be linear, while everything else sRGB
                // This could be handled better, tbh, but at least we're accounting for linear textures this time around

                parallaxMaterials.parallaxLow.SetTexture(name, tex);
                parallaxMaterials.parallaxMid.SetTexture(name, tex);
                parallaxMaterials.parallaxHigh.SetTexture(name, tex);

                parallaxMaterials.parallaxLowMid.SetTexture(name, tex);
                parallaxMaterials.parallaxMidHigh.SetTexture(name, tex);

                parallaxMaterials.parallaxFull.SetTexture(name, tex);
            }

            loaded = true;
            isLoading = false;
        }
        public static Texture2D LoadTexIfUnloaded(ParallaxTerrainBody body, string path, string key)
        {
            if (!body.loadedTextures.TryGetValue(key, out var handle))
            {
                var options = new TextureLoadOptions
                {
                    linear = TextureUtils.IsLinear(key),
                    unreadable = true
                };
                handle = TextureLoadManager.LoadTexture(path, options);
                body.loadedTextures.Add(key, handle);
            }

            try
            {
                return handle.Texture;
            }
            catch (Exception e)
            {
                ParallaxDebug.LogError($"Failed to load texture {handle.Path ?? "<null>"}");
                Debug.LogException(e);
                return null;
            }
        }
        /// <summary>
        /// Used by the GUI to load textures on texture changes
        /// </summary>
        public void Reload()
        {
            Unload();
            Load();
        }
        public void Unload()
        {
            isLoading = false;
            // Unload all textures
            foreach (var handle in loadedTextures.Values)
                handle.Dispose();
            loadedTextures.Clear();
            loaded = false;
        }
    }

    public enum ParallaxScaledBodyMode
    {
        FromTerrain,
        Baked,
        CustomRequiresTerrain,
        Custom
    }
    // Parallax Scaled Body
    public class ParallaxScaledBody
    {
        public string planetName;

        /// <summary>
        /// Reference to the stock scaled body
        /// </summary>

        public ParallaxTerrainBody terrainBody;

        // Material for shadow casting
        public Material shadowCasterMaterial;

        public MaterialParams scaledMaterialParams;
        public ParallaxScaledBodyMode mode = ParallaxScaledBodyMode.FromTerrain;
        public Material scaledMaterial;

        public float minTerrainAltitude = 0;
        public float maxTerrainAltitude = 0;

        public bool disableDeformity = false;

        public Dictionary<string, TextureLoadManager.TextureHandle<Texture2D>> loadedTextures = [];
        public float worldSpaceMeshRadius;

        private bool loaded = false;
        public bool Loaded
        {
            get { return loaded; }
        }
        private bool isLoading = false;
        public bool IsLoading
        { 
            get { return isLoading; } 
        }

        public ParallaxScaledBody(string name)
        {
            planetName = name;
        }
        /// <summary>
        /// When using Custom/ParallaxScaled shader, copy over the terrain shader properties
        /// </summary>
        public void ReadTerrainShaderProperties()
        {
            scaledMaterialParams.shaderProperties.Append(terrainBody.terrainShaderProperties);
        }
        public void LoadInitial(string shader)
        {
            Material baseMaterial = new Material(AssetBundleLoader.parallaxScaledShaders[shader]);
            scaledMaterial = baseMaterial;
            shadowCasterMaterial = new Material(AssetBundleLoader.parallaxScaledShaders["Custom/RaymarchedShadows"]);

            // Enable keywords
            foreach (string keyword in scaledMaterialParams.shaderKeywords)
            {
                Debug.Log("Enabling keyword: " + keyword);

                scaledMaterial.EnableKeyword(keyword);

                // Shadow caster won't have all the keywords that the main material will, but enable them anyway in case we're using custom shaders
                shadowCasterMaterial.EnableKeyword(keyword);
            }
            
            UpdateBaseMaterialParams();

            // Scaled meshes are denser than terrain, lower the tessellation but reduce edge length
            scaledMaterial.SetFloat("_MaxTessellation", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation / 6);
            scaledMaterial.SetFloat("_TessellationEdgeLength", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength / 4);
            scaledMaterial.SetFloat("_MaxTessellationRange", float.MaxValue);
        }
        public void UpdateBaseMaterialParams()
        {
            ShaderProperties scaledShaderProperties = scaledMaterialParams.shaderProperties;

            foreach (KeyValuePair<string, float> floatValue in scaledShaderProperties.shaderFloats)
            {
                scaledMaterial.SetFloat(floatValue.Key, floatValue.Value);
            }
            foreach (KeyValuePair<string, Vector3> vectorValue in scaledShaderProperties.shaderVectors)
            {
                scaledMaterial.SetVector(vectorValue.Key, vectorValue.Value);
            }
            foreach (KeyValuePair<string, Color> colorValue in scaledShaderProperties.shaderColors)
            {
                scaledMaterial.SetColor(colorValue.Key, colorValue.Value);
            }
            foreach (KeyValuePair<string, int> intValue in scaledShaderProperties.shaderInts)
            {
                scaledMaterial.SetInt(intValue.Key, intValue.Value);
            }
        }
        public void UpdateBaseMaterialParamsFromGUI()
        {
            UpdateBaseMaterialParams();
            SetScaledMaterialParams(FlightGlobals.GetBodyByName(planetName));
        }
        /// <summary>
        /// Sets the ScaledSpace shader material params correctly scaled by the scale factor
        /// </summary>
        public void SetScaledMaterialParams(CelestialBody kspBody)
        {
            Material scaledMaterial = this.scaledMaterial;

            float _PlanetRadius = (float)kspBody.Radius;
            float _MinAltitude = minTerrainAltitude;
            float _MaxAltitude = maxTerrainAltitude;

            float _MeshRadius = GetMeshRadiusScaledSpace(kspBody);
            worldSpaceMeshRadius = _MeshRadius;
            float scalingFactor = _MeshRadius / _PlanetRadius;

            scaledMaterial.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
            scaledMaterial.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);

            // Terrain shader specific
            if (mode == ParallaxScaledBodyMode.FromTerrain || mode == ParallaxScaledBodyMode.CustomRequiresTerrain)
            {
                scaledMaterial.SetFloat("_LowMidBlendStart", (scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendStart"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_LowMidBlendEnd", (scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendEnd"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_MidHighBlendStart", (scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendStart"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_MidHighBlendEnd", (scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendEnd"] + _PlanetRadius) * scalingFactor);

                // Required for land mask
                scaledMaterial.SetFloat("_WorldPlanetRadius", _MeshRadius);
            }

            if (disableDeformity)
            {
                scaledMaterial.SetInt("_MaxTessellation", 1);
                scaledMaterial.SetFloat("_TessellationEdgeLength", 50.0f);
                scaledMaterial.SetInt("_DisableDisplacement", 1);
            }
            else
            {
                scaledMaterial.SetInt("_DisableDisplacement", 0);
            }

            // Setup shadow caster - must set this manually
            shadowCasterMaterial.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
            shadowCasterMaterial.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);
            shadowCasterMaterial.SetFloat("_WorldPlanetRadius", _MeshRadius);
            shadowCasterMaterial.SetFloat("_ScaleFactor", scalingFactor);
            shadowCasterMaterial.SetInt("_DisableDisplacement", disableDeformity ? 1 : 0);

            if (scaledMaterialParams.shaderKeywords.Contains("OCEAN") || scaledMaterialParams.shaderKeywords.Contains("OCEAN_FROM_COLORMAP"))
            {
                // Colormap doesn't matter for the shadow caster, just enable one of them
                shadowCasterMaterial.EnableKeyword("OCEAN");
                shadowCasterMaterial.SetFloat("_OceanAltitude", scaledMaterialParams.shaderProperties.shaderFloats["_OceanAltitude"]);
            }

            if (ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows)
            {
                shadowCasterMaterial.EnableKeyword("BLUE_NOISE");
            }
            else
            {
                shadowCasterMaterial.DisableKeyword("BLUE_NOISE");
            }

            // Computed the max shadow ray distance
            float worldDistance = maxTerrainAltitude * scalingFactor + _MeshRadius;
            float theta = Mathf.Asin(_MeshRadius / worldDistance);
            float tangentDist = _MeshRadius / Mathf.Tan(theta);

            shadowCasterMaterial.SetFloat("_MaxRayDistance", tangentDist);

            // Computed at runtime, but the default is computed from Kerbin's SMA around the Sun
            shadowCasterMaterial.SetFloat("_LightWidth", 0.0384f);

            // Setup environment
            scaledMaterial.SetTexture("_Skybox", SkyboxControl.cubeMap);

            // Pretend we have a main tex parameter
            scaledMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        }

        // Averages all vert distances from the planet to get the radius in scaled space
        // Potential optimization here - just calculate the scaled space mesh radius manually
        public float GetMeshRadiusScaledSpace(CelestialBody celestialBody)
        {
            return (float)(celestialBody.Radius * ScaledSpace.InverseScaleFactor);
        }

        internal void StartLoadingTextures()
        {
            foreach (var (name, path) in scaledMaterialParams.shaderProperties.shaderTextures)
            {
                if (loadedTextures.ContainsKey(name))
                    continue;

                // Check to see if the texture we're trying to load is a terrain texture
                if ((mode == ParallaxScaledBodyMode.FromTerrain || mode == ParallaxScaledBodyMode.CustomRequiresTerrain)
                    && terrainBody.loadedTextures.TryGetValue(name, out var handle))
                {
                    loadedTextures.Add(name, handle.Acquire());
                    continue;
                }

                var options = new TextureLoadOptions
                {
                    linear = TextureUtils.IsLinear(name),
                    unreadable = true
                };
                loadedTextures.Add(name, TextureLoadManager.LoadTexture(path, options));
            }
        }

        public void Load()
        {

            // First check for terrain body's terrain textures and load them
            if (!terrainBody.Loaded && mode != ParallaxScaledBodyMode.Baked)
                terrainBody.StartLoadingTextures();

            // Start loading our own textures in the background while that happens
            StartLoadingTextures();

            if (!terrainBody.Loaded && mode != ParallaxScaledBodyMode.Baked)
                terrainBody.Load();

            // Now load the textures needed here
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            foreach (var name in scaledMaterialParams.shaderProperties.shaderTextures.Keys)
            {
                Texture2D tex;
                var request = loadedTextures[name];

                try
                {
                    tex = request.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {request.Path}");
                    Debug.LogException(e);
                    continue;
                }

                scaledMaterial.SetTexture(name, tex);
                if (name == "_HeightMap")
                {
                    shadowCasterMaterial.SetTexture("_HeightMap", tex);
                }
            }

            // Set ocean color
            if (scaledMaterialParams.shaderKeywords.Contains("OCEAN"))
            {
                if (FlightGlobals.GetBodyByName(planetName).pqsController == null)
                {
                    ParallaxDebug.LogError("Planet " + planetName + " scaled material has OCEAN keyword but the planet doesn't have any PQS");
                    return;
                }
                Color pqsMapOceanColor = FlightGlobals.GetBodyByName(planetName).pqsController.mapOceanColor;
                scaledMaterial.SetColor("_OceanColor", pqsMapOceanColor);
            }

            Debug.Log("Elapsed (scaled): " + sw.Elapsed.Milliseconds.ToString("F3"));

            loaded = true;
        }

        // Textures that need to be loaded during the first frame.
        private static readonly string[] BaseTextures = ["_HeightMap", "_NormalMap", "_BumpMap", "_ColorMap"];

        /// <summary>
        /// Spread the load over a few frames. Not fully async, due to Unity Texture2D limitation
        /// </summary>
        /// <returns></returns>
        public IEnumerator LoadAsync()
        {
            // Instantly load the height, color and normal maps to get a 'base' level of detail
            // Then wait for the rest "higher detail" to load - most of the load time will be spent loading terrain textures

            isLoading = true;

            Coroutine terrainBodyLoad = null;

            // If we need them, make sure that all the terrain body texture entries are there
            if (!terrainBody.Loaded && mode != ParallaxScaledBodyMode.Baked)
                terrainBodyLoad = ScaledManager.Instance.StartCoroutine(terrainBody.LoadAsync());

            StartLoadingTextures();

            // Now load the textures needed here
            foreach (var name in scaledMaterialParams.shaderProperties.shaderTextures.Keys)
            {
                var handle = loadedTextures[name];
                if (!handle.IsComplete)
                    yield return handle.Wait();

                if (!isLoading)
                    yield break;

                Texture2D tex;
                try
                {
                    tex = handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                scaledMaterial.SetTexture(name, tex);
                if (name == "_HeightMap")
                {
                    shadowCasterMaterial.SetTexture("_HeightMap", tex);
                }
            }

            if (terrainBodyLoad is not null)
                yield return terrainBodyLoad;

            #if false
            // // Give the background loads a chance to work during the frame.
            // yield return new WaitForEndOfFrame();

            // if (!isLoading)
            //     yield break;

            // Now load the textures we need to have loaded immediately
            foreach (var name in BaseTextures)
            {
                if (!loadedTextures.TryGetValue(name, out var handle))
                    continue;

                Texture2D tex;
                try
                {
                    tex = handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                scaledMaterial.SetTexture(name, tex);
                if (name == "_HeightMap")
                {
                    shadowCasterMaterial.SetTexture("_HeightMap", tex);
                }
            }

            //
            //  Base maps loaded, now load the terrain
            //

            // First check for terrain body's terrain textures and load them
            if (!terrainBody.Loaded && mode != ParallaxScaledBodyMode.Baked)
            {
                ScaledManager.Instance.StartCoroutine(terrainBody.LoadAsync());
                yield return new WaitUntil(() => terrainBody.Loaded == true);
            }

            if (!isLoading)
                yield break;

            // Now load the textures needed here
            foreach (var name in scaledMaterialParams.shaderProperties.shaderTextures.Keys)
            {
                if (BaseTextures.Contains(name))
                    continue;

                var handle = loadedTextures[name];
                yield return handle.Wait();

                if (!isLoading)
                    yield break;

                Texture2D tex;
                try
                {
                    tex = handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load texture {handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                scaledMaterial.SetTexture(name, tex);
                if (name == "_HeightMap")
                {
                    shadowCasterMaterial.SetTexture("_HeightMap", tex);
                }
            }
            #endif

            bool hasOcean = scaledMaterialParams.shaderKeywords.Contains("OCEAN");
            bool hasOceanColormap = scaledMaterialParams.shaderKeywords.Contains("OCEAN_FROM_COLORMAP");
            if (hasOcean || hasOceanColormap)
            {
                if (hasOcean)
                {
                    // Set ocean color
                    if (FlightGlobals.GetBodyByName(planetName).pqsController == null)
                    {
                        ParallaxDebug.LogError("Planet " + planetName + " scaled material has OCEAN keyword but the planet doesn't have any PQS");
                        yield break;
                    }
                    Color pqsMapOceanColor = FlightGlobals.GetBodyByName(planetName).pqsController.mapOceanColor;
                    scaledMaterial.SetColor("_OceanColor", pqsMapOceanColor);

                    // Set shadow material keyword

                }
            }

            loaded = true;
            isLoading = false;
        }

        public void Unload()
        {
            // Prevent LoadAsync from continuing, if it was
            isLoading = false;

            // First destroy our textures.
            // We may be sharing the textures with the terrain but the handle
            // takes care of tracking that and only disposing them when needed.
            foreach (var handle in loadedTextures.Values)
                handle.Dispose();

            loadedTextures.Clear();
            loaded = false;
            

            // Scaled body is always loaded when terrain is loaded
            // So, if terrain is loaded, it could be hanging around for the scaled body
            // Unload the terrain now that the scaled body is no longer needed
            if (terrainBody.Loaded)
            {
                terrainBody.Unload();
            }
        }
    }

    // Stores all material quality variants
    public class ParallaxMaterials
    {
        public Material parallaxLow;
        public Material parallaxMid;
        public Material parallaxHigh;

        public Material parallaxLowMid;
        public Material parallaxMidHigh;

        public Material parallaxFull;

        //
        //  Functions for setting properties easily
        //
        private delegate void MaterialPropertySetter(Material material, string propertyName, object value);

        private static readonly Dictionary<Type, MaterialPropertySetter> propertySetters = new Dictionary<Type, MaterialPropertySetter>
        {
            { typeof(int), (material, propertyName, value) => material.SetInt(propertyName, (int)value) },
            { typeof(float), (material, propertyName, value) => material.SetFloat(propertyName, (float)value) },
            { typeof(Color), (material, propertyName, value) => material.SetColor(propertyName, (Color)value) },
            { typeof(Vector4), (material, propertyName, value) => material.SetVector(propertyName, (Vector4)value) },
            { typeof(Texture), (material, propertyName, value) => material.SetTexture(propertyName, (Texture)value) }
        };

        public void SetAll(string propertyName, object value)
        {
            // Exception if property type is unsupported
            Type valueType = value.GetType();
            if (!propertySetters.ContainsKey(valueType))
            {
                throw new ArgumentException($"Unsupported property type: {valueType}");
            }

            // Set all material properties
            MaterialPropertySetter setter = propertySetters[valueType];

            setter(parallaxLow, propertyName, value);
            setter(parallaxMid, propertyName, value);
            setter(parallaxHigh, propertyName, value);

            setter(parallaxLowMid, propertyName, value);
            setter(parallaxMidHigh, propertyName, value);

            setter(parallaxFull, propertyName, value);
        }
    }

    //
    //  Parallax Scatters
    //

    // Structs
    // Precomputed / preset in the configs by the user, used purely for optimization purposes
    public struct OptimizationParams
    {
        public float frustumCullingIgnoreRadius;
        public float frustumCullingSafetyMargin;
        public int maxRenderableObjectsLOD0;
        public int maxRenderableObjectsLOD1;
        public int maxRenderableObjectsLOD2;
    }
    public enum SubdivisionMode
    {
        noSubdivision,
        nearestQuads
    }
    public enum NoiseType
    {
        simplexPerlin,
        simplexCellular,
        simplexPolkaDot,
        
        // Maybe implement
        cubist,
        sparseConvolution,
        hermite
    }
    public struct SubdivisionParams
    {
        // If the quad needs subdividing
        public SubdivisionMode subdivisionMode;
        public int maxSubdivisionLevel;
    }
    public struct NoiseParams
    {
        public NoiseType noiseType;
        public bool inverted;
        public int octaves;
        public float lacunarity;
        public float frequency;
        public int seed;
    }
    public struct DistributionParams
    {
        public float seed;
        public float spawnChance;
        public float range;
        public int populationMultiplier;
        public Vector3 minScale;
        public Vector3 maxScale;
        public float scaleRandomness;
        public float noiseCutoff;
        public float steepPower;
        public float steepContrast;
        public float steepMidpoint;
        public float maxNormalDeviance;
        public float minAltitude;
        public float maxAltitude;
        public float altitudeFadeRange;
        public bool fixedAltitude;
        public float placementAltitude;
        public int alignToTerrainNormal;
        public bool coloredByTerrain;
        public LOD lod1;
        public LOD lod2;
        public HashSet<string> biomeBlacklist;
        /// <summary>
        /// Range before being scaled by the global settings
        /// </summary>
        public float originalRange;
        /// <summary>
        /// Population Multiplier before being scaled by the global settings
        /// </summary>
        public int originalPopulationMultiplier;
    }
    public struct LOD
    {
        public string modelPathOverride;
        public MaterialParams materialOverride;
        public float range;
        public bool inheritsMaterial;
    }
    // Holds shader and its variations - rest is processed at load time from shaderbank
    public struct MaterialParams
    {
        public string shader;
        public List<string> shaderKeywords;
        public ShaderProperties shaderProperties;
    }
    public struct BiomeBlacklistParams
    {
        // The name of each biome and the colours they correspond to where this scatter can appear - Max 8
        public List<string> blacklistedBiomes;
        public HashSet<string> fastBlacklistedBiomes;
    }
    // Stores scatter information
    public class ParallaxScatterBody
    {
        public string planetName;

        /// <summary>
        /// Configs are set up via module manager. When we want to save and overwrite a config we need its exact path to know what to replace.
        /// </summary>
        public string configFilePath;

        /// <summary>
        /// Minimum subdivision level for scatters to appear. Initialized at something big.
        /// </summary>
        public int minimumSubdivisionLevel = 100;

        /// <summary>
        /// Contains scatters and shared scatters
        /// </summary>
        public Dictionary<string, Scatter> scatters = new Dictionary<string, Scatter>();

        // Shared textures across the planet
        // Holds Texture2D and Cubemaps
        public Dictionary<string, TextureLoadManager.TextureHandle<Texture>> loadedTextures = [];

        /// <summary>
        /// Contains all scatters for fast iteration, but not sharedScatters
        /// </summary>
        public Scatter[] fastScatters;

        /// <summary>
        /// The scatters that can be collided with on this planet
        /// </summary>
        public Scatter[] collideableScatters;
        public ParallaxScatterBody(string planetName)
        {
            this.planetName = planetName;
        }
        public void SetSubdivisionRequirements(double[] subdivisionThresholds, double thresholdMultiplier, int maxLevel)
        {
            // Each index is a quad subdivision level, ending at maxLevel inclusive (0 to maxLevel)
            foreach (KeyValuePair<string, Scatter> scatter in scatters)
            {
                int minimumSubdivision = maxLevel;
                float scatterRange = scatter.Value.distributionParams.range;
                foreach (double threshold in subdivisionThresholds)
                {
                    double subdivisionRange = threshold * thresholdMultiplier;
                    // The scatter will appear on this quad
                    if (scatterRange > subdivisionRange)
                    {
                        minimumSubdivision--;
                    }
                }
                if (minimumSubdivision < minimumSubdivisionLevel)
                {
                    minimumSubdivisionLevel = minimumSubdivision;
                }
            }
            ParallaxDebug.Log("Minimum Subdivision Level for " + planetName + " = " + minimumSubdivisionLevel + " (MaxLevel is " + maxLevel + ")");
        }
        public void UnloadTextures()
        {
            ParallaxDebug.Log("Unloading textures for " + planetName);
            foreach (var handle in loadedTextures.Values)
                handle.Dispose();
            loadedTextures.Clear();
        }
    }
    
    // Stores Scatter information
    public class Scatter
    {
        public string scatterName;
        public string modelPath;
        public bool useCraftPosition = false;

        public OptimizationParams optimizationParams;
        public SubdivisionParams subdivisionParams;
        public NoiseParams noiseParams;
        public DistributionParams distributionParams;
        public MaterialParams materialParams;
        public BiomeBlacklistParams biomeBlacklistParams;

        public Texture2D biomeControlMap;
        public int biomeCount = 0;

        public bool isShared = false;

        public bool collideable = false;
        // Only used for saving from GUI
        public int collisionLevel;

        public int collideableArrayIndex = -1;
        public float sqrMeshBound = 0;

        public ScatterRenderer renderer;
        public ComputeShader shader;

        public Scatter(string scatterName)
        {
            this.scatterName = scatterName;
        }
        public void InitShader()
        {
            shader = UnityEngine.Object.Instantiate(AssetBundleLoader.parallaxComputeShaders["TerrainScatters"]);
        }
        public void UnloadShader()
        {
            UnityEngine.Object.Destroy(shader);
        }
        public void ReinitializeDistribution()
        {
            foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
            {
                quadData.Value.ReinitializeScatters(this);
            }
        }

        /// <summary>
        /// Computes the largest distance a vertex is from the origin. Used for collision checks
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public float CalculateSqrLargestBound(string modelPath)
        {
            Debug.Log("Searching for model at " + modelPath);
            Mesh mesh = GameDatabase.Instance.GetModel(modelPath).GetComponent<MeshFilter>().mesh;
            if (mesh == null || mesh.vertexCount == 0)
            {
                ParallaxDebug.LogError("Mesh is null or has no vertices.");
                return 0f;
            }

            Vector3[] vertices = mesh.vertices;
            float maxDistance = 0f;

            foreach (Vector3 vertex in vertices)
            {
                float distance = vertex.sqrMagnitude; // Distance from the origin
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                }
            }

            return maxDistance;
        }

        public virtual ConfigNode ToConfigNode()
        {
            ConfigNode scatterNode = new ConfigNode("Scatter");
            ConfigNode optimizationNode = scatterNode.AddNode("Optimizations");
            ConfigNode subdivisionSettingsNode = scatterNode.AddNode("SubdivisionSettings");
            ConfigNode distributionNoiseNode = scatterNode.AddNode("DistributionNoise");
            ConfigNode materialNode = scatterNode.AddNode("Material");
            ConfigNode distributionNode = scatterNode.AddNode("Distribution");

            PopulateScatterNode(scatterNode);
            PopulateOptimizationNode(optimizationNode);
            PopulateSubdivisionSettingsNode(subdivisionSettingsNode);
            PopulateDistributionNoiseNode(distributionNoiseNode);
            PopulateMaterialNode(materialNode, materialParams);
            PopulateDistributionNode(distributionNode);

            return scatterNode;
        }
        protected virtual void PopulateScatterNode(ConfigNode node)
        {
            // Remove planet name from the start of the scatter name
            node.AddValue("name", scatterName.Split('-')[1]);

            node.AddValue("model", modelPath);
            node.AddValue("collisionLevel", collisionLevel);
        }
        protected virtual void PopulateOptimizationNode(ConfigNode node)
        {
            node.AddValue("frustumCullingStartRange", optimizationParams.frustumCullingIgnoreRadius * distributionParams.range);
            node.AddValue("frustumCullingScreenMargin", optimizationParams.frustumCullingSafetyMargin);
            node.AddValue("maxRenderableObjectsLOD0", optimizationParams.maxRenderableObjectsLOD0);
            node.AddValue("maxRenderableObjectsLOD1", optimizationParams.maxRenderableObjectsLOD1);
            node.AddValue("maxRenderableObjectsLOD2", optimizationParams.maxRenderableObjectsLOD2);
        }
        protected virtual void PopulateSubdivisionSettingsNode(ConfigNode node)
        {
            node.AddValue("subdivisionRangeMode", "noSubdivision");
        }
        protected virtual void PopulateDistributionNoiseNode(ConfigNode node)
        {
            node.AddValue("noiseType", noiseParams.noiseType.ToString());
            node.AddValue("inverted", noiseParams.inverted);
            node.AddValue("frequency", noiseParams.frequency);
            node.AddValue("octaves", noiseParams.octaves);
            node.AddValue("lacunarity", noiseParams.lacunarity);
            node.AddValue("seed", noiseParams.seed);
        }
        protected virtual void PopulateMaterialNode(ConfigNode node, MaterialParams materialParams)
        {
            node.AddValue("shader", materialParams.shader);
            ShaderProperties properties = materialParams.shaderProperties;

            foreach (KeyValuePair<string, string> texturePair in properties.shaderTextures)
            {
                node.AddValue(texturePair.Key, texturePair.Value);
            }

            foreach (KeyValuePair<string, float> floatPair in properties.shaderFloats)
            {
                node.AddValue(floatPair.Key, floatPair.Value);
            }

            foreach (KeyValuePair<string, Vector3> vectorPair in properties.shaderVectors)
            {
                node.AddValue(vectorPair.Key, vectorPair.Value);
            }

            foreach (KeyValuePair<string, Color> colorPair in properties.shaderColors)
            {
                node.AddValue(colorPair.Key, colorPair.Value);
            }

            foreach (KeyValuePair<string, int> intPair in properties.shaderInts)
            {
                node.AddValue(intPair.Key, intPair.Value);
            }

            ConfigNode keywordsNode = node.AddNode("Keywords");
            foreach (string keyword in materialParams.shaderKeywords)
            {
                keywordsNode.AddValue("name", keyword);
            }

        }
        protected virtual void PopulateDistributionNode(ConfigNode node)
        {
            node.AddValue("seed", distributionParams.seed);
            node.AddValue("spawnChance", distributionParams.spawnChance);
            node.AddValue("range", distributionParams.range);
            node.AddValue("populationMultiplier", (int)distributionParams.populationMultiplier);
            node.AddValue("minScale", distributionParams.minScale);
            node.AddValue("maxScale", distributionParams.maxScale);
            node.AddValue("scaleRandomness", distributionParams.scaleRandomness);
            node.AddValue("cutoffScale", distributionParams.noiseCutoff);
            node.AddValue("steepPower", distributionParams.steepPower);
            node.AddValue("steepContrast", distributionParams.steepContrast);
            node.AddValue("steepMidpoint", distributionParams.steepMidpoint);
            node.AddValue("maxNormalDeviance", Mathf.Pow(distributionParams.maxNormalDeviance, 0.333333f));
            node.AddValue("minAltitude", distributionParams.minAltitude);
            node.AddValue("maxAltitude", distributionParams.maxAltitude);
            node.AddValue("altitudeFadeRange", distributionParams.altitudeFadeRange);
            node.AddValue("alignToTerrainNormal", distributionParams.alignToTerrainNormal == 1 ? true : false);
            node.AddValue("coloredByTerrain", distributionParams.coloredByTerrain);

            ConfigNode lodNode = node.AddNode("LODs");

            ConfigNode lod1 = lodNode.AddNode("LOD");
            ConfigNode lod2 = lodNode.AddNode("LOD");
            ProcessLOD(lod1, distributionParams.lod1);
            ProcessLOD(lod2, distributionParams.lod2);

            if (biomeBlacklistParams.blacklistedBiomes.Count > 0)
            {
                ConfigNode biomeBlacklistNode = node.AddNode("BiomeBlacklist");
                foreach (string biome in biomeBlacklistParams.blacklistedBiomes)
                {
                    biomeBlacklistNode.AddValue("name", biome);
                }
            }


        }
        protected virtual void ProcessLOD(ConfigNode lodNode, LOD lod)
        {
            lodNode.AddValue("model", lod.modelPathOverride);
            lodNode.AddValue("range", lod.range * distributionParams.range);

            ShaderProperties properties = lod.materialOverride.shaderProperties;

            if (lod.materialOverride.shaderKeywords.SequenceEqual(materialParams.shaderKeywords) && lod.materialOverride.shader == materialParams.shader)
            {
                // Material can be stored as an override - now work out what changed
                ConfigNode materialNode = lodNode.AddNode("MaterialOverride");

                List<string> texKeyDifferences = properties.shaderTextures.GetDifferingKeys(materialParams.shaderProperties.shaderTextures);
                foreach (string textureDiff in texKeyDifferences)
                {
                    materialNode.AddValue(textureDiff, properties.shaderTextures[textureDiff]);
                }

                List<string> floatKeyDifferences = properties.shaderFloats.GetDifferingKeys(materialParams.shaderProperties.shaderFloats);
                foreach (string floatDiff in floatKeyDifferences)
                {
                    materialNode.AddValue(floatDiff, properties.shaderFloats[floatDiff]);
                }

                List<string> vectorKeyDifferences = properties.shaderVectors.GetDifferingKeys(materialParams.shaderProperties.shaderVectors);
                foreach (string vectorDiff in vectorKeyDifferences)
                {
                    materialNode.AddValue(vectorDiff, properties.shaderVectors[vectorDiff]);
                }

                List<string> colorKeyDifferences = properties.shaderColors.GetDifferingKeys(materialParams.shaderProperties.shaderColors);
                foreach (string colorDiff in colorKeyDifferences)
                {
                    materialNode.AddValue(colorDiff, properties.shaderColors[colorDiff]);
                }

                List<string> intKeyDifferences = properties.shaderInts.GetDifferingKeys(materialParams.shaderProperties.shaderInts);
                foreach (string intDiff in intKeyDifferences)
                {
                    materialNode.AddValue(intDiff, properties.shaderInts[intDiff]);
                }
            }
            else
            {
                // Keywords were changed, so we must define the material explicitly
                PopulateMaterialNode(lodNode.AddNode("Material"), lod.materialOverride);
            }
        }
    }
    // Scatter that can have a unique material but shares its distribution with its parent to avoid generating it twice
    public class SharedScatter : Scatter
    {
        public Scatter parent;
        public SharedScatter(string scatterName, Scatter parent) : base(scatterName)
        {
            this.parent = parent;
            this.isShared = true;
        }
        public override ConfigNode ToConfigNode()
        {
            ConfigNode scatterNode = new ConfigNode("SharedScatter");
            ConfigNode optimizationNode = scatterNode.AddNode("Optimizations");
            ConfigNode materialNode = scatterNode.AddNode("Material");
            ConfigNode distributionNode = scatterNode.AddNode("Distribution");

            PopulateUnused();

            PopulateScatterNode(scatterNode);
            PopulateOptimizationNode(optimizationNode);
            PopulateMaterialNode(materialNode, materialParams);
            PopulateDistributionNode(distributionNode);

            return scatterNode;
        }
        void PopulateUnused()
        {
            this.distributionParams.lod1.range = parent.distributionParams.lod1.range;
            this.distributionParams.lod2.range = parent.distributionParams.lod2.range;

            this.optimizationParams.frustumCullingSafetyMargin = parent.optimizationParams.frustumCullingSafetyMargin;
            this.optimizationParams.frustumCullingIgnoreRadius = parent.optimizationParams.frustumCullingIgnoreRadius;
            this.optimizationParams.maxRenderableObjectsLOD0 = parent.optimizationParams.maxRenderableObjectsLOD0;
            this.optimizationParams.maxRenderableObjectsLOD1 = parent.optimizationParams.maxRenderableObjectsLOD1;
            this.optimizationParams.maxRenderableObjectsLOD2 = parent.optimizationParams.maxRenderableObjectsLOD2;
        }
        protected override void PopulateDistributionNode(ConfigNode node)
        {
            // Do not process distribution params

            ConfigNode lodNode = node.AddNode("LODs");

            ConfigNode lod1 = lodNode.AddNode("LOD");
            ConfigNode lod2 = lodNode.AddNode("LOD");

            ProcessLOD(lod1, distributionParams.lod1);
            ProcessLOD(lod2, distributionParams.lod2);

            // Do not process biome blacklist params
        }
        protected override void PopulateScatterNode(ConfigNode node)
        {
            // Remove planet name from the start of the scatter name
            node.AddValue("name", scatterName.Split('-')[1]);

            node.AddValue("model", modelPath);
            node.AddValue("collisionLevel", 1);
            node.AddValue("parentName", parent.scatterName.Split('-')[1]);
        }
    }
    // Stores the names of the variables, then the types as defined in the ShaderPropertiesTemplate.cfg
    public class ShaderProperties : ICloneable
    {
        public Dictionary<string, string> shaderTextures = new Dictionary<string, string>();
        public Dictionary<string, float> shaderFloats = new Dictionary<string, float>();
        public Dictionary<string, Vector3> shaderVectors = new Dictionary<string, Vector3>();
        public Dictionary<string, Color> shaderColors = new Dictionary<string, Color>();
        public Dictionary<string, int> shaderInts = new Dictionary<string, int>();
        public object Clone()
        {
            var clone = new ShaderProperties();
            foreach (var textureValue in shaderTextures)
            {
                clone.shaderTextures.Add(textureValue.Key, textureValue.Value);
            }
            foreach (var floatValue in shaderFloats)
            {
                clone.shaderFloats.Add(floatValue.Key, floatValue.Value);
            }
            foreach (var vectorValue in shaderVectors)
            {
                clone.shaderVectors.Add(vectorValue.Key, vectorValue.Value);
            }
            foreach (var colorValue in shaderColors)
            {
                clone.shaderColors.Add(colorValue.Key, colorValue.Value);
            }
            foreach (var intValue in shaderInts)
            {
                clone.shaderInts.Add(intValue.Key, intValue.Value);
            }
            return clone;
        }
        /// <summary>
        /// Append incoming ShaderProperties to this one. No duplicate values allowed - will prioritise source properties if conflicted, and will log an error.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="incoming"></param>
        public void Append(ShaderProperties incoming)
        {
            foreach (var textureValue in incoming.shaderTextures)
            {
                if (!shaderTextures.ContainsKey(textureValue.Key))
                {
                    shaderTextures.Add(textureValue.Key, textureValue.Value);
                }
                else
                {
                    ParallaxDebug.LogError("Attempting to append duplicate shader property: " + textureValue.Key);
                }
            }
            foreach (var floatValue in incoming.shaderFloats)
            {
                if (!shaderFloats.ContainsKey(floatValue.Key))
                {
                    shaderFloats.Add(floatValue.Key, floatValue.Value);
                }
                else
                {
                    ParallaxDebug.LogError("Attempting to append duplicate shader property: " + floatValue.Key);
                }
            }
            foreach (var vectorValue in incoming.shaderVectors)
            {
                if (!shaderVectors.ContainsKey(vectorValue.Key))
                {
                    shaderVectors.Add(vectorValue.Key, vectorValue.Value);
                }
                else
                {
                    ParallaxDebug.LogError("Attempting to append duplicate shader property: " + vectorValue.Key);
                }
            }
            foreach (var colorValue in incoming.shaderColors)
            {
                if (!shaderColors.ContainsKey(colorValue.Key))
                {
                    shaderColors.Add(colorValue.Key, colorValue.Value);
                }
                else
                {
                    ParallaxDebug.LogError("Attempting to append duplicate shader property: " + colorValue.Key);
                }
            }
            foreach (var intValue in incoming.shaderInts)
            {
                if (!shaderInts.ContainsKey(intValue.Key))
                {
                    shaderInts.Add(intValue.Key, intValue.Value);
                }
                else
                {
                    ParallaxDebug.LogError("Attempting to append duplicate shader property: " + intValue.Key);
                }
            }
        }
    }
}
