using HarmonyLib;
using Kopernicus.Configuration;
using Parallax.Debugging;
using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: KSPAssembly("Parallax", 1, 0)]
[assembly: KSPAssemblyDependency("Kopernicus", 1, 0)]
[assembly: KSPAssemblyDependency("KSPBurst", 1, 0)]
namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ConfigLoader : MonoBehaviour
    {
        // Stores the types of the properties set on the shader
        // Each ParallaxBody has a clone of this
        public static ShaderProperties shaderPropertiesTemplate = new ShaderProperties();

        // Holds global settings defined in ParallaxGlobalSettings.cfg
        public static ParallaxSettings parallaxGlobalSettings = new ParallaxSettings();

        // Stores all parallax terrain bodies by planet name
        public static Dictionary<string, ParallaxTerrainBody> parallaxTerrainBodies = new Dictionary<string, ParallaxTerrainBody>();

        // Stores all parallax scaled bodies by planet name
        public static Dictionary<string, ParallaxScaledBody> parallaxScaledBodies = new Dictionary<string, ParallaxScaledBody>();

        // Stores all parallax scatter bodies by planet name
        public static Dictionary<string, ParallaxScatterBody> parallaxScatterBodies = new Dictionary<string, ParallaxScatterBody>();

        public static ObjectPool<GameObject> colliderPool;

        // Stores transparent material for terrain quads
        public static Material transparentMaterial;

        // Debug wireframe material
        public static Material wireframeMaterial;

        /// <summary>
        /// GameData path. Includes a trailing forward slash - GameData/
        /// </summary>
        public static string GameDataPath;

        // Configs that start with 'Parallax' as the root node
        public UrlDir.UrlConfig globalNode;
        public void Awake()
        {
            ParallaxDebug.Log("Starting!");
            GameDataPath = KSPUtil.ApplicationRootPath + "GameData/";
        }
        // Entry point
        public static void ModuleManagerPostLoad()
        {
            ParallaxDebug.Log("Beginning config load");
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            // Get system information
            ParallaxSystemInfo.ReadInfo();
            ParallaxSystemInfo.LogInfo();

            AssetBundleLoader.Initialize();
            InitializeGlobalSettings(GetConfigByName("ParallaxGlobal"));
            Shader.SetGlobalInt("_ParallaxScaledShadowStepSize", ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount);

            ParallaxDebug.Log("Starting Harmony patching...");
            var harmony = new Harmony("Parallax");
            harmony.PatchAll();
            ParallaxDebug.Log("Harmony patching complete");

            InitializeTemplateConfig(GetConfigByName("ParallaxShaderProperties").config, shaderPropertiesTemplate);

            LoadTerrainConfigs(GetConfigsByName("ParallaxTerrain"));
            LoadScatterConfigs(GetConfigsByName("ParallaxScatters"));
            LoadSharedScatterConfigs(GetConfigsByName("ParallaxScatters"));
            InitializeObjectPools(parallaxGlobalSettings);

            transparentMaterial = new Material(AssetBundleLoader.parallaxTerrainShaders["Custom/DiscardAll"]);
            wireframeMaterial = new Material(AssetBundleLoader.parallaxTerrainShaders["Custom/Wireframe"]);

            sw.Stop();
            ParallaxDebug.Log("Loading took " + sw.Elapsed.TotalSeconds.ToString("F2") + " seconds");

            ApplyCompatibilityPatches();
            CheckSettings();
        }

        private static void CheckSettings()
        {
            // If the terrain detail is not maxed out
            if (PQSCache.PresetList.presetIndex != PQSCache.PresetList.presets.Count - 1)
            {
                PopupDialog dialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), "Parallax Error", "Parallax Error",
                                     "Parallax Installation Error: Your 'Terrain Detail' setting is not set to 'High'. You must set this correctly in the main menu graphics settings!", "I'll change this!", true, HighLogic.UISkin);
            }
        }

        public static UrlDir.UrlConfig[] GetConfigsByName(string name)
        {
            return GameDatabase.Instance.GetConfigs(name);
        }
        public static UrlDir.UrlConfig GetConfigByName(string name)
        {
            UrlDir.UrlConfig[] configs = GameDatabase.Instance.GetConfigs(name);
            if (configs != null && configs.Length > 0)
            {
                return configs[0];
            }
            return null;
        }
        public static UrlDir.UrlConfig GetBaseParallaxNode(string planetName)
        {
            UrlDir.UrlConfig[] baseConfig = GetConfigsByName("ParallaxTerrain");
            foreach (UrlDir.UrlConfig config in baseConfig)
            {
                ConfigNode[] bodyNode = config.config.GetNodes("Body");
                foreach (ConfigNode body in bodyNode)
                {
                    if (body.GetValue("name") == planetName)
                    {
                        return config;
                    }
                }
            }
            return null;
        }
        public static ConfigNode GetPlanetTerrainNode(string planetName)
        {
            UrlDir.UrlConfig[] baseConfig = GetConfigsByName("ParallaxTerrain");
            foreach (UrlDir.UrlConfig config in baseConfig)
            {
                ConfigNode[] bodyNode = config.config.GetNodes("Body");
                foreach (ConfigNode body in bodyNode)
                {
                    if (body.GetValue("name") == planetName)
                    {
                        return body;
                    }
                }
            }
            return null;
        }
        // Gets "ParallaxScatters" node
        public static UrlDir.UrlConfig GetRootScatterNode(string planetName)
        {
            UrlDir.UrlConfig[] baseConfig = GetConfigsByName("ParallaxScatters");
            // "ParallaxScatters"
            foreach (UrlDir.UrlConfig cfg in baseConfig)
            {
                ConfigNode bodyNode = cfg.config.GetNode("Body");
                if (bodyNode.GetValue("name") == planetName)
                {
                    return cfg;
                }
            }
            return null;
        }
        // Gets "Body" node
        public static ConfigNode GetPlanetScatterNode(string planetName)
        {
            UrlDir.UrlConfig[] baseConfig = GetConfigsByName("ParallaxScatters");
            return baseConfig.SelectMany(x => x.config.GetNodes("Body")).FirstOrDefault(bodyNode => bodyNode.GetValue("name") == planetName);
        }
        public static ConfigNode GetScatterConfigNode(string planetName, string scatterName, bool isShared = false)
        {
            return GetScatterConfigNode(planetName, scatterName, GetPlanetScatterNode(planetName), isShared);
        }
        public static ConfigNode GetScatterConfigNode(string planetName, string scatterName, ConfigNode baseConfig, bool isShared = false)
        {
            // Gets the scatter node
            if (!isShared)
            {
                // Gets the scatter node
                return baseConfig.GetNodes("Scatter").FirstOrDefault(x => (planetName + "-" + x.GetValue("name")) == scatterName);
            }
            else
            {
                return baseConfig.GetNodes("SharedScatter").FirstOrDefault(x => (planetName + "-" + x.GetValue("name")) == scatterName);
            }
        }
        // Used to preserve a scatter's position in the config
        public static void DeterminePrecedingAndTrailingNodes(ConfigNode parent, ConfigNode node, List<ConfigNode> preceding, List<ConfigNode> trailing)
        {
            bool isPreceding = true;
            ConfigNode[] nodes = parent.GetNodes();
            foreach (ConfigNode searchNode in nodes)
            {
                if (searchNode == node)
                {
                    isPreceding = false;
                    continue;
                }
                if (isPreceding)
                {
                    preceding.Add(searchNode);
                }
                else
                {
                    trailing.Add(searchNode);
                }
            }
        }
        // Preserve order
        public static void OverwriteConfigNode(ConfigNode parentNode, ConfigNode newNode, List<ConfigNode> preceding, List<ConfigNode> trailing, params string[] removeNodeNames)
        {
            foreach (string nodeName in removeNodeNames)
            {
                parentNode.RemoveNodes(nodeName);
            }
            
            foreach (ConfigNode node in preceding)
            {
                parentNode.AddNode(node);
            }

            parentNode.AddNode(newNode);

            foreach (ConfigNode node in trailing)
            {
                parentNode.AddNode(node);
            }
        }
        /// <summary>
        /// Save a config node. Optionally include the node name in the saved file (defaults true). Returns true if save was successful
        /// </summary>
        /// <param name="node"></param>
        /// <param name="path"></param>
        /// <param name="createParent"></param>
        /// <returns></returns>
        public static bool SaveConfigNode(ConfigNode node, string path, bool createParent = true)
        {
            if (!createParent)
            {
                return node.Save(path);
            }
            string nodeName = node.name;
            ConfigNode parentNode = new ConfigNode(nodeName);
            parentNode.AddNode(node);
            return parentNode.Save(path);
        }
        public static void InitializeGlobalSettings(UrlDir.UrlConfig config)
        {
            if (config == null)
            {
                ParallaxDebug.Log("Generating default settings config");

                // Save the defaults
                parallaxGlobalSettings.SaveSettings();

                return;
            }
            ConfigNode terrainSettingsNode = config.config.GetNode("TerrainShaderSettings");
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellation = float.Parse(terrainSettingsNode.GetValue("maxTessellation"));
            parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength = float.Parse(terrainSettingsNode.GetValue("tessellationEdgeLength"));
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange = float.Parse(terrainSettingsNode.GetValue("maxTessellationRange"));
            parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending = bool.Parse(terrainSettingsNode.GetValue("useAdvancedTextureBlending"));
            parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion = bool.Parse(terrainSettingsNode.GetValue("useAmbientOcclusion"));

            ConfigNode scatterSettingsNode = config.config.GetNode("ScatterSystemSettings");
            parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier = float.Parse(scatterSettingsNode.GetValue("densityMultiplier"));
            parallaxGlobalSettings.scatterGlobalSettings.rangeMultiplier = float.Parse(scatterSettingsNode.GetValue("rangeMultiplier"));
            parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange = float.Parse(scatterSettingsNode.GetValue("fadeOutStartRange"));
            parallaxGlobalSettings.scatterGlobalSettings.collisionLevel = int.Parse(scatterSettingsNode.GetValue("collisionLevel"));
            parallaxGlobalSettings.scatterGlobalSettings.colliderLookaheadTime = float.Parse(scatterSettingsNode.GetValue("colliderLookaheadTime"));

            ConfigNode lightingSettingsNode = config.config.GetNode("LightingSettings");
            parallaxGlobalSettings.lightingGlobalSettings.lightShadows = bool.Parse(lightingSettingsNode.GetValue("lightShadows"));
            parallaxGlobalSettings.lightingGlobalSettings.lightShadowsQuality = (LightShadowResolution)Enum.Parse(typeof(LightShadowResolution), lightingSettingsNode.GetValue("lightShadowQuality"));

            ConfigNode scaledSettingsNode = config.config.GetNode("ScaledSystemSettings");
            parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows = bool.Parse(scaledSettingsNode.GetValue("scaledSpaceShadows"));
            parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows = bool.Parse(scaledSettingsNode.GetValue("smoothScaledSpaceShadows"));
            parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount = int.Parse(scaledSettingsNode.GetValue("scaledRaymarchedShadowStepCount"));
            parallaxGlobalSettings.scaledGlobalSettings.loadTexturesImmediately = bool.Parse(scaledSettingsNode.GetValue("loadTexturesImmediately"));

            ConfigNode debugSettingsNode = config.config.GetNode("DebugSettings");
            parallaxGlobalSettings.debugGlobalSettings.wireframeTerrain = bool.Parse(debugSettingsNode.GetValue("wireframeTerrain"));
            parallaxGlobalSettings.debugGlobalSettings.suppressCriticalMessages = bool.Parse(debugSettingsNode.GetValue("suppressCriticalMessages"));

            ConfigNode objectPoolsNode = config.config.GetNode("ObjectPoolSettings");
            parallaxGlobalSettings.objectPoolSettings.cachedColliderCount = int.Parse(objectPoolsNode.GetValue("cachedColliderCount"));
        }
        public static void InitializeObjectPools(ParallaxSettings settings)
        {
            GameObject templateCollider = new GameObject("ParallaxCollider");
            templateCollider.AddComponent<MeshCollider>();
            templateCollider.SetActive(false);
            templateCollider.layer = 15;
            colliderPool = new ObjectPool<GameObject>(templateCollider, settings.objectPoolSettings.cachedColliderCount);
        }
        // Looks up a shader in the shader bank
        public static ShaderProperties LookupTemplateConfig(UrlDir.UrlConfig config, string shaderName, List<string> keywords)
        {
            // Config starts at 'ParallaxScatterShaderProperties'
            ShaderProperties shaderProperties = new ShaderProperties();
            List<string> keywordsToRemove = new List<string>();

            // 'ParallaxShader'
            ConfigNode[] allShaders = config.config.GetNodes("ParallaxShader");
            foreach (ConfigNode node in allShaders)
            {
                string name = node.GetValue("name");
                if (shaderName == name)
                {
                    // We've found the right shader in the shader bank
                    // Parse global properties

                    ConfigNode globalPropertiesNode = node.GetNode("GlobalProperties");
                    InitializeTemplateConfig(globalPropertiesNode, shaderProperties);

                    // "Keywords"
                    ConfigNode keywordsNode = node.GetNode("Keywords");
                    foreach (string keyword in keywords)
                    {
                        // The keywords node 
                        ConfigNode keywordNode = keywordsNode.GetNode(keyword);

                        if (keywordNode == null) 
                        { 
                            ParallaxDebug.LogCritical("Template shader config error: Keyword " + keyword + " could not be found!");
                            continue;
                        }

                        string supersededBy = keywordNode.GetValue("supersededBy");
                        if (supersededBy != null)
                        {
                            // This keyword is overridden by another keyword. Now check if that keyword is enabled on the shader (linear search)
                            if (keywords.Contains(supersededBy))
                            {
                                ParallaxDebug.Log("[Warning] This keyword (" + keyword + " ) on a scatter material is superseded by " + supersededBy + ", skipping!");
                                keywordsToRemove.Add(keyword);
                                continue;
                            }
                        }

                        // Initialize the properties this keyword adds
                        InitializeTemplateConfig(keywordNode, shaderProperties);
                    }
                }
            }

            // Now remove the keywords that got overridden from the keywords list
            keywords.RemoveAll(item => keywordsToRemove.Contains(item));

            return shaderProperties;
        }
        // Template configs tell Parallax what variable names and type are supported by the shader
        // We can use this recursively with shader keywords
        public static void InitializeTemplateConfig(ConfigNode config, ShaderProperties properties)
        {
            ConfigNode.ConfigNodeList nodes = config.nodes;
            ConfigNode texturesNode = nodes.GetNode("Textures");
            ConfigNode floatsNode = nodes.GetNode("Floats");
            ConfigNode vectorsNode = nodes.GetNode("Vectors");
            ConfigNode colorsNode = nodes.GetNode("Colors");
            ConfigNode intsNode = nodes.GetNode("Ints");

            string[] texturesNames = texturesNode.GetValues("name");
            string[] floatsNames = floatsNode.GetValues("name");
            string[] vectorsNames = vectorsNode.GetValues("name");
            string[] colorsNames = colorsNode.GetValues("name");
            string[] intsNames = intsNode.GetValues("name");
            
            // Add template names
            foreach (string value in texturesNames)
            {
                properties.shaderTextures.Add(value, "");
            }
            foreach (string value in floatsNames)
            {
                properties.shaderFloats.Add(value, 0);
            }
            foreach (string value in vectorsNames)
            {
                properties.shaderVectors.Add(value, Vector3.zero);
            }
            foreach (string value in colorsNames)
            {
                properties.shaderColors.Add(value, Color.black);
            }
            foreach (string value in intsNames)
            {
                properties.shaderInts.Add(value, 0);
            }

        }
        public static void LoadTerrainConfigs(UrlDir.UrlConfig[] rootNodes)
        {
            // "ParallaxTerrain"
            foreach (UrlDir.UrlConfig rootNode in rootNodes)
            {
                // "Body"
                ConfigNode[] nodes = rootNode.config.GetNodes("Body");
                foreach (ConfigNode planetNode in nodes)
                {
                    string planetName = planetNode.GetValue("name");
                    ParallaxDebug.Log("Parsing new terrain body: " + planetName);
                    if (parallaxTerrainBodies.ContainsKey(planetName))
                    {
                        ParallaxDebug.LogError("Parallax Terrain config for " + planetName + " already exists, skipping!");
                        ParallaxDebug.LogError("Sanity check - Are you using a module manager patch? Check you're not patching a ParallaxTerrain or ParallaxScatters node, or it will apply whatever you're doing to every config!");
                        continue;
                    }

                    string emissive = planetNode.GetValue("emissive");
                    bool isEmissive = emissive == null ? false : (bool.Parse(emissive) == true ? true : false);

                    ParallaxTerrainBody body = new ParallaxTerrainBody(planetName);
                    body.emissive = isEmissive;

                    ParseNewBody(body, planetNode.GetNode("ShaderProperties"));

                    // Parse scaled body, if present
                    ConfigNode scaledBodyNode = planetNode.GetNode("ParallaxScaledProperties");
                    if (scaledBodyNode != null)
                    {
                        ParseNewScaledBody(body, scaledBodyNode);
                    }
                    
                    body.LoadInitial();
                    parallaxTerrainBodies.Add(planetName, body);
                }
            }
        }
        public static void ParseNewBody(ParallaxTerrainBody body, ConfigNode bodyNode)
        {
            // Grab the template
            body.terrainShaderProperties = shaderPropertiesTemplate.Clone() as ShaderProperties;

            // Now get every value defined in the template
            string[] textureProperties = body.terrainShaderProperties.shaderTextures.Keys.ToArray();
            string[] floatProperties = body.terrainShaderProperties.shaderFloats.Keys.ToArray();
            string[] vectorProperties = body.terrainShaderProperties.shaderVectors.Keys.ToArray();
            string[] colorProperties = body.terrainShaderProperties.shaderColors.Keys.ToArray();
            string[] intProperties = body.terrainShaderProperties.shaderInts.Keys.ToArray();

            // Parse correct value type and set on the shader properties
            foreach (string propertyName in textureProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                if (configValue == null)
                {
                    // Default to this texture if the requested texture couldn't be found
                    Debug.Log("No texture (" + propertyName + ") found on " + body.planetName + ", setting it to default white");
                    configValue = "ParallaxContinued/white.dds";
                }
                if (!File.Exists(KSPUtil.ApplicationRootPath + "GameData/" + configValue))
                {
                    ParallaxDebug.LogCritical("This texture file doesn't exist: " + configValue + " for planet: " + body.planetName);
                }
                body.terrainShaderProperties.shaderTextures[propertyName] = configValue;
            }
            foreach (string propertyName in floatProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(float), out object result);
                body.terrainShaderProperties.shaderFloats[propertyName] = (float)result;
            }
            foreach (string propertyName in vectorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Vector3), out object result);
                body.terrainShaderProperties.shaderVectors[propertyName] = (Vector3)result;
            }
            foreach (string propertyName in colorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Color), out object result);
                body.terrainShaderProperties.shaderColors[propertyName] = (Color)result;
            }
            foreach (string propertyName in intProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(int), out object result);
                body.terrainShaderProperties.shaderFloats[propertyName] = (int)result;
            }
        }

        // ConfigNode is "ParallaxScaledProperties"
        public static void ParseNewScaledBody(ParallaxTerrainBody body, ConfigNode bodyNode)
        {
            // Prevent duplicate
            ParallaxDebug.Log("Parsing new scaled body: " + body.planetName);
            if (parallaxScaledBodies.ContainsKey(body.planetName))
            {
                ParallaxDebug.LogError("Parallax Scaled config for " + body.planetName + " already exists, skipping!");
                ParallaxDebug.LogError("Sanity check - Are you using a module manager patch? Check you're not patching a ParallaxTerrain or ParallaxScatters node, or it will apply whatever you're doing to every config!");
                return;
            }

            // Parse scaled body
            ParallaxScaledBody scaledBody = new ParallaxScaledBody(body.planetName);
            scaledBody.terrainBody = body;

            string mode = bodyNode.GetValue("mode");
            bool success = Enum.TryParse(mode, out ParallaxScaledBodyMode bodyMode);
            if (!success)
            {
                bodyMode = ParallaxScaledBodyMode.FromTerrain;
                ParallaxDebug.LogCritical("Unable to parse the scaled body mode '" + mode + "'. Possible values are 'FromTerrain' and 'Custom'. 'Defaulting to 'FromTerrain'");
            }
            scaledBody.mode = bodyMode;

            // Planet properties
            string minTerrainAltitudeString = ConfigUtils.TryGetConfigValue(bodyNode, "minTerrainAltitude");
            string maxTerrainAltitudeString = ConfigUtils.TryGetConfigValue(bodyNode, "maxTerrainAltitude");

            scaledBody.minTerrainAltitude = (float)ConfigUtils.TryParse(scaledBody.planetName, "minTerrainAltitude", minTerrainAltitudeString, typeof(float));
            scaledBody.maxTerrainAltitude = (float)ConfigUtils.TryParse(scaledBody.planetName, "maxTerrainAltitude", maxTerrainAltitudeString, typeof(float));

            ParseScaledMaterialProperties(scaledBody, bodyMode, bodyNode.GetNode("Material"));
            ParseScaledMaterialOverride(scaledBody, bodyMode, bodyNode.GetNode("TerrainMaterialOverride"));

            //
            // Optional properties
            //

            // Stock mesh
            bool usingStockMesh = false;
            if (bodyNode.TryGetValue("usingStockMesh", ref usingStockMesh))
            {
                scaledBody.disableDeformity = true;
            }

            // Load the base properties (no textures) and create the material
            scaledBody.LoadInitial(scaledBody.scaledMaterialParams.shader);

            parallaxScaledBodies.Add(body.planetName, scaledBody);
        }
        public static void ParseScaledMaterialProperties(ParallaxScaledBody body, ParallaxScaledBodyMode mode, ConfigNode materialNode)
        {
            body.scaledMaterialParams = new MaterialParams();

            // Get keywords on this material
            ConfigNode keywordsNode = materialNode.GetNode("Keywords");
            List<string> keywords = new List<string>();
            if (keywordsNode != null)
            {
                keywords.AddRange(keywordsNode.GetValuesList("name"));
            }
            body.scaledMaterialParams.shaderKeywords = keywords;

            // From terrain
            if (mode == ParallaxScaledBodyMode.FromTerrain)
            {
                body.scaledMaterialParams.shader = "Custom/ParallaxScaled";

                // Read material properties - For FromTerrain this'll be _ColorMap, _HeightMap, _BumpMap
                body.scaledMaterialParams.shaderProperties = LookupTemplateConfig(GetConfigByName("ParallaxScaledShaderProperties"), body.scaledMaterialParams.shader, keywords);

                // Populate the values from this material. Do this before appending the terrain shader stuff, as that is already initialised by this point
                PopulateMaterialValues(ref body.scaledMaterialParams, materialNode, body.planetName);

                // Finally, copy terrain properties over to the scaled body
                body.ReadTerrainShaderProperties();
                DetermineScaledKeywordsFromTerrain(body, body.scaledMaterialParams.shaderKeywords);
            }
            if (mode == ParallaxScaledBodyMode.Baked)
            {
                body.scaledMaterialParams.shader = "Custom/ParallaxScaledBaked";

                // Read material properties - For FromTerrain this'll be _ColorMap, _HeightMap, _BumpMap
                body.scaledMaterialParams.shaderProperties = LookupTemplateConfig(GetConfigByName("ParallaxScaledShaderProperties"), body.scaledMaterialParams.shader, keywords);

                // Populate the values from this material
                PopulateMaterialValues(ref body.scaledMaterialParams, materialNode, body.planetName);
            }
            if (mode == ParallaxScaledBodyMode.CustomRequiresTerrain)
            {
                string shaderName = materialNode.GetValue("customShaderName");
                if (shaderName == null)
                {
                    ParallaxDebug.LogCritical("Scaled body mode is CustomRequiresTerrain, but customShaderName value in the Material node is missing - Parallax has no idea what shader name to look for!");
                    return;
                }

                body.scaledMaterialParams.shader = shaderName;

                // Read the properties
                body.scaledMaterialParams.shaderProperties = LookupTemplateConfig(GetConfigByName("ParallaxScaledShaderProperties"), body.scaledMaterialParams.shader, keywords);

                // Populate the values from this material
                PopulateMaterialValues(ref body.scaledMaterialParams, materialNode, body.planetName);

                // Append terrain properties and enable appropriate keywords
                body.ReadTerrainShaderProperties();
                DetermineScaledKeywordsFromTerrain(body, body.scaledMaterialParams.shaderKeywords);
            }
            if (mode == ParallaxScaledBodyMode.Custom)
            {
                string shaderName = materialNode.GetValue("customShaderName");
                if (shaderName == null)
                {
                    ParallaxDebug.LogCritical("Scaled body mode is CustomRequiresTerrain, but customShaderName value in the Material node is missing - Parallax has no idea what shader name to look for!");
                    return;
                }

                body.scaledMaterialParams.shader = shaderName;

                // Read the properties
                body.scaledMaterialParams.shaderProperties = LookupTemplateConfig(GetConfigByName("ParallaxScaledShaderProperties"), body.scaledMaterialParams.shader, keywords);

                // Populate the values from this material
                PopulateMaterialValues(ref body.scaledMaterialParams, materialNode, body.planetName);
            }
        }
        public static void ParseScaledMaterialOverride(ParallaxScaledBody body, ParallaxScaledBodyMode mode, ConfigNode overrideNode)
        {
            // We're not inheriting from terrain, so there's nothing to override
            if (mode != ParallaxScaledBodyMode.FromTerrain && mode != ParallaxScaledBodyMode.CustomRequiresTerrain)
            {
                return;
            }
            // Nothing to override
            if (overrideNode == null)
            {
                return;
            }
            ProcessMaterialOverride(overrideNode, ref body.scaledMaterialParams, body.planetName);
        }
        public static void DetermineScaledKeywordsFromTerrain(ParallaxScaledBody body, List<string> keywords)
        {
            float minAltitude = body.minTerrainAltitude;
            float maxAltitude = body.maxTerrainAltitude;

            float blendLowMidStart = body.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendStart"];
            float blendLowMidEnd = body.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendEnd"];
            float blendMidHighStart = body.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendStart"];
            float blendMidHighEnd = body.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendEnd"];

            // Parse keywords from quality settings
            if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion)
            {
                keywords.Add("AMBIENT_OCCLUSION");
            }
            if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending)
            {
                keywords.Add("ADVANCED_BLENDING");
            }
            if (body.terrainBody.emissive)
            {
                keywords.Add("EMISSION");
            }

            // Parse keywords from what textures are used on this planet
            // This quad uses all three textures
            if (minAltitude < blendLowMidEnd && maxAltitude > blendMidHighStart)
            {
                keywords.Add("PARALLAX_FULL");
                return;
            }

            // This quad uses entirely 'High' texture
            if (minAltitude > blendMidHighEnd)
            {
                keywords.Add("PARALLAX_SINGLE_HIGH");
                return;
            }

            // This quad uses entirely 'Mid' texture
            if (minAltitude > blendLowMidEnd && maxAltitude < blendMidHighStart)
            {
                keywords.Add("PARALLAX_SINGLE_MID");
                return;
            }

            // This quad uses entirely 'Low' texture
            if (maxAltitude < blendLowMidStart)
            {
                keywords.Add("PARALLAX_SINGLE_LOW");
                return;
            }

            // This quad uses 'Low' and 'Mid' textures
            // Since any other combination has already been returned
            if ((minAltitude < blendLowMidStart && maxAltitude > blendLowMidEnd) || (minAltitude < blendLowMidStart && maxAltitude < blendLowMidEnd && maxAltitude > blendLowMidStart) || (minAltitude > blendLowMidStart && minAltitude < blendLowMidEnd && maxAltitude > blendLowMidEnd) || (minAltitude > blendLowMidStart && minAltitude < blendLowMidEnd && maxAltitude > blendLowMidStart && maxAltitude < blendLowMidEnd))
            {
                keywords.Add("PARALLAX_DOUBLE_LOWMID");
                return;
            }

            // This quad uses 'Mid' and 'high' textures
            // Since any other combination has already been returned
            if ((minAltitude < blendMidHighStart && maxAltitude > blendMidHighEnd) || (minAltitude < blendMidHighStart && maxAltitude < blendMidHighEnd && maxAltitude > blendMidHighStart) || (minAltitude > blendMidHighStart && minAltitude < blendMidHighEnd && maxAltitude > blendMidHighEnd) || (minAltitude > blendMidHighStart && minAltitude < blendMidHighEnd && maxAltitude > blendMidHighStart && maxAltitude < blendMidHighEnd))
            {
                keywords.Add("PARALLAX_DOUBLE_MIDHIGH");
                return;
            }

            // Fallback
            keywords.Add("PARALLAX_FULL");
            return;
        }

        //
        //  Scatter parsing
        //

        public static void LoadScatterConfigs(UrlDir.UrlConfig[] rootNodes)
        {
            // "ParallaxScatters"
            foreach (UrlDir.UrlConfig rootNode in rootNodes)
            {
                ConfigNode[] bodyNodes = rootNode.config.GetNodes("Body");
                foreach (ConfigNode bodyNode in bodyNodes)
                {
                    string body = bodyNode.GetValue("name");

                    if (parallaxScatterBodies.ContainsKey(body))
                    {
                        ParallaxDebug.LogError("Parallax Scatter config for " + body + " already exists, skipping!");
                        ParallaxDebug.LogError("Sanity check - Are you using a module manager patch? Check you're not patching a ParallaxTerrain or ParallaxScatters node, or it will apply whatever you're doing to every config!");
                        continue;
                    }

                    string configVersion = bodyNode.GetValue("configVersion");
                    if (configVersion == null)
                    {
                        ParallaxDebug.LogError("Legacy configs detected for: " + body + ", either configVersion is missing from the file, or your configs are not supported on this version!");
                        continue;
                    }

                    ParallaxScatterBody scatterBody = new ParallaxScatterBody(body);

                    // The scatters that are collideable, which depends on the collision level of the scatter and the level set in the global config
                    List<Scatter> collideableScatters = new List<Scatter>();
                    int collideableIndex = -1;

                    // "Scatter"
                    ConfigNode[] nodes = bodyNode.GetNodes("Scatter");
                    foreach (ConfigNode node in nodes)
                    {
                        string scatterName = body + "-" + ConfigUtils.TryGetConfigValue(node, "name");
                        ParallaxDebug.Log("Parsing new scatter: " + scatterName);

                        if (scatterBody.scatters.ContainsKey(scatterName))
                        {
                            ParallaxDebug.LogError("Scatter " + scatterName + " already exists on " + body + ", skipping!");
                            ParallaxDebug.LogError("Sanity check - Are you using a module manager patch? Check you're not patching a ParallaxTerrain or ParallaxScatters node, or it will apply whatever you're doing to every config!");
                            continue;
                        }

                        string model = ConfigUtils.TryGetConfigValue(node, "model");
                        string collisionLevelString = ConfigUtils.TryGetConfigValue(node, "collisionLevel");
                        int collisionLevel = (int)ConfigUtils.TryParse(body, "collisionLevel", collisionLevelString, typeof(int));

                        Scatter scatter = new Scatter(scatterName);
                        scatter.modelPath = model;

                        OptimizationParams optimizationParams = GetOptimizationParams(body, node.GetNode("Optimizations"));
                        SubdivisionParams subdivisionParams = GetSubdivisionParams(body, node.GetNode("SubdivisionSettings"));
                        NoiseParams noiseParams = GetNoiseParams(body, node.GetNode("DistributionNoise"));
                        MaterialParams materialParams = GetMaterialParams(body, node.GetNode("Material"));
                        DistributionParams distributionParams = GetDistributionParams(body, node.GetNode("Distribution"), materialParams, false);
                        BiomeBlacklistParams biomeBlacklistParams = GetBiomeBlacklistParams(body, node.GetNode("Distribution"));

                        scatter.optimizationParams = optimizationParams;
                        scatter.subdivisionParams = subdivisionParams;
                        scatter.noiseParams = noiseParams;
                        scatter.materialParams = materialParams;
                        scatter.distributionParams = distributionParams;
                        scatter.biomeBlacklistParams = biomeBlacklistParams;

                        PerformNormalisationConversions(scatter);
                        PerformAdditionalOperations(scatter);

                        scatter.collisionLevel = collisionLevel;

                        // -1 disables colliders
                        if (parallaxGlobalSettings.scatterGlobalSettings.collisionLevel > -1 && collisionLevel >= parallaxGlobalSettings.scatterGlobalSettings.collisionLevel)
                        {
                            scatter.collideable = true;
                            collideableIndex++;
                            scatter.collideableArrayIndex = collideableIndex;
                            collideableScatters.Add(scatter);
                        }

                        scatterBody.scatters.Add(scatterName, scatter);
                    }

                    scatterBody.fastScatters = scatterBody.scatters.Values.ToArray();
                    scatterBody.collideableScatters = collideableScatters.ToArray();
                    parallaxScatterBodies.Add(body, scatterBody);
                }
            }
        }
        public static void LoadSharedScatterConfigs(UrlDir.UrlConfig[] rootNodes)
        {
            foreach (UrlDir.UrlConfig rootNode in rootNodes)
            {
                ConfigNode[] bodyNodes = rootNode.config.GetNodes("Body");
                foreach (ConfigNode bodyNode in bodyNodes)
                {
                    string body = bodyNode.GetValue("name");

                    ParallaxDebug.Log("[Shared Scatter Pass] Searching for scatter body: " + body);

                    if (!parallaxScatterBodies.ContainsKey(body))
                    {
                        ParallaxDebug.LogError("Trying to parse a shared scatter on " + body + ", but this planet doesn't exist. Skipping!");
                        continue;
                    }

                    ParallaxScatterBody scatterBody = parallaxScatterBodies[body];

                    // Process shared scatters - These are scatters that share distribution data with another, so it doesn't need to be generated again

                    ConfigNode[] sharedNodes = bodyNode.GetNodes("SharedScatter");
                    foreach (ConfigNode node in sharedNodes)
                    {
                        string scatterName = body + "-" + ConfigUtils.TryGetConfigValue(node, "name");

                        ParallaxDebug.Log("[Shared Scatter Pass] Parsing new shared scatter: " + scatterName);

                        string parentName = body + "-" + ConfigUtils.TryGetConfigValue(node, "parentName");

                        ParallaxDebug.Log("[Shared Scatter Pass] - Searching for Parent Scatter: " + parentName);

                        string model = ConfigUtils.TryGetConfigValue(node, "model");

                        if (!parallaxScatterBodies[body].scatters.ContainsKey(parentName))
                        {
                            ParallaxDebug.LogError("Shared scatter: " + scatterName + " inherits from parent scatter: " + parentName + " but that scatter does not exist. Skipping!");
                            continue;
                        }
                        Scatter parentScatter = parallaxScatterBodies[body].scatters[parentName];

                        SharedScatter sharedScatter = new SharedScatter(scatterName, parentScatter);
                        sharedScatter.modelPath = model;

                        OptimizationParams optimizationParams = GetOptimizationParams(body, node.GetNode("Optimizations"));
                        MaterialParams materialParams = GetMaterialParams(body, node.GetNode("Material"));
                        DistributionParams distributionParams = GetDistributionParams(body, node.GetNode("Distribution"), materialParams, true);

                        sharedScatter.optimizationParams = optimizationParams;
                        sharedScatter.materialParams = materialParams;
                        sharedScatter.distributionParams = distributionParams;

                        PerformNormalisationConversions(sharedScatter);
                        scatterBody.scatters.Add(scatterName, sharedScatter);
                    }
                }
            }
        }
        public static OptimizationParams GetOptimizationParams(string planetName, ConfigNode node)
        {
            OptimizationParams optimizationParams = new OptimizationParams();

            string frustumCullingIgnoreRadius = ConfigUtils.TryGetConfigValue(node, "frustumCullingStartRange");
            string frustumCullingSafetyMargin = ConfigUtils.TryGetConfigValue(node, "frustumCullingScreenMargin");

            bool isUsingLegacyObjectCount = node.HasValue("maxObjects");
            if (isUsingLegacyObjectCount)
            {
                string maxRenderableObjects = ConfigUtils.TryGetConfigValue(node, "maxObjects");
                int maxRenderableObjectsValue = (int)ConfigUtils.TryParse(planetName, "maxObjects", maxRenderableObjects, typeof(int));

                optimizationParams.maxRenderableObjectsLOD0 = maxRenderableObjectsValue / 2;
                optimizationParams.maxRenderableObjectsLOD1 = maxRenderableObjectsValue / 4;
                optimizationParams.maxRenderableObjectsLOD2 = maxRenderableObjectsValue;
            }
            else
            {
                string maxRenderableObjectsLOD0 = ConfigUtils.TryGetConfigValue(node, "maxRenderableObjectsLOD0");
                string maxRenderableObjectsLOD1 = ConfigUtils.TryGetConfigValue(node, "maxRenderableObjectsLOD1");
                string maxRenderableObjectsLOD2 = ConfigUtils.TryGetConfigValue(node, "maxRenderableObjectsLOD2");

                optimizationParams.maxRenderableObjectsLOD0 = (int)ConfigUtils.TryParse(planetName, "maxRenderableObjectsLOD0", maxRenderableObjectsLOD0, typeof(int));
                optimizationParams.maxRenderableObjectsLOD1 = (int)ConfigUtils.TryParse(planetName, "maxRenderableObjectsLOD1", maxRenderableObjectsLOD1, typeof(int));
                optimizationParams.maxRenderableObjectsLOD2 = (int)ConfigUtils.TryParse(planetName, "maxRenderableObjectsLOD2", maxRenderableObjectsLOD2, typeof(int));
            }

            optimizationParams.frustumCullingIgnoreRadius = (float)ConfigUtils.TryParse(planetName, "cullingRange", frustumCullingIgnoreRadius, typeof(float));
            optimizationParams.frustumCullingSafetyMargin = (float)ConfigUtils.TryParse(planetName, "cullingLimit", frustumCullingSafetyMargin, typeof(float));
            

            return optimizationParams;
        }
        public static SubdivisionParams GetSubdivisionParams(string planetName, ConfigNode node)
        {
            SubdivisionParams subdivisionParams = new SubdivisionParams();

            string subdivisionRangeMode = ConfigUtils.TryGetConfigValue(node, "subdivisionRangeMode");

            subdivisionParams.subdivisionMode = (SubdivisionMode)Enum.Parse(typeof(SubdivisionMode), subdivisionRangeMode);

            if (subdivisionParams.subdivisionMode == SubdivisionMode.noSubdivision)
            {
                return subdivisionParams;
            }
            
            string subdivisionLevel = ConfigUtils.TryGetConfigValue(node, "subdivisionLevel");
            subdivisionParams.maxSubdivisionLevel = (int)ConfigUtils.TryParse(planetName, "subdivisionLevel", subdivisionLevel, typeof(int));

            return subdivisionParams;
        }
        public static NoiseParams GetNoiseParams(string planetName, ConfigNode node)
        {
            NoiseParams noiseParams = new NoiseParams();

            string frequency = ConfigUtils.TryGetConfigValue(node, "frequency");
            string octaves = ConfigUtils.TryGetConfigValue(node, "octaves");
            string lacunarity = ConfigUtils.TryGetConfigValue(node, "lacunarity");
            string seed = ConfigUtils.TryGetConfigValue(node, "seed");
            string noiseType = ConfigUtils.TryGetConfigValue(node, "noiseType");
            string inverted = ConfigUtils.TryGetConfigValue(node, "inverted");

            noiseParams.frequency = (float)ConfigUtils.TryParse(planetName, "frequency", frequency, typeof(float));
            noiseParams.octaves = (int)ConfigUtils.TryParse(planetName, "octaves", octaves, typeof(int));
            noiseParams.lacunarity = (float)ConfigUtils.TryParse(planetName, "lacunarity", lacunarity, typeof(float));
            noiseParams.seed = (int)ConfigUtils.TryParse(planetName, "seed", seed, typeof(int));
            noiseParams.noiseType = (NoiseType)Enum.Parse(typeof(NoiseType), noiseType);
            noiseParams.inverted = (bool)ConfigUtils.TryParse(planetName, "inverted", inverted, typeof(bool));

            return noiseParams;
        }
        public static DistributionParams GetDistributionParams(string planetName, ConfigNode node, in MaterialParams baseMaterial, bool onlyLODs)
        {
            DistributionParams distributionParams = new DistributionParams();

            if (!onlyLODs)
            {
                string seed = ConfigUtils.TryGetConfigValue(node, "seed");
                string spawnChance = ConfigUtils.TryGetConfigValue(node, "spawnChance");
                string range = ConfigUtils.TryGetConfigValue(node, "range");
                string populationMultiplier = ConfigUtils.TryGetConfigValue(node, "populationMultiplier");
                string minScale = ConfigUtils.TryGetConfigValue(node, "minScale");
                string maxScale = ConfigUtils.TryGetConfigValue(node, "maxScale");
                string scaleRandomness = ConfigUtils.TryGetConfigValue(node, "scaleRandomness");
                string noiseCutoff = ConfigUtils.TryGetConfigValue(node, "cutoffScale");
                string steepPower = ConfigUtils.TryGetConfigValue(node, "steepPower");
                string steepContrast = ConfigUtils.TryGetConfigValue(node, "steepContrast");
                string steepMidpoint = ConfigUtils.TryGetConfigValue(node, "steepMidpoint");
                string maxNormalDeviance = ConfigUtils.TryGetConfigValue(node, "maxNormalDeviance");
                string minAltitude = ConfigUtils.TryGetConfigValue(node, "minAltitude");
                string maxAltitude = ConfigUtils.TryGetConfigValue(node, "maxAltitude");
                string altitudeFadeRange = ConfigUtils.TryGetConfigValue(node, "altitudeFadeRange");
                string alignToTerrainNormal = ConfigUtils.TryGetConfigValue(node, "alignToTerrainNormal");
                string coloredByTerrain = ConfigUtils.TryGetConfigValue(node, "coloredByTerrain");
                string placementAltitude = ConfigUtils.TryGetConfigValue(node, "placementAltitude", false);

                distributionParams.seed = (float)ConfigUtils.TryParse(planetName, "seed", seed, typeof(float));
                distributionParams.spawnChance = (float)ConfigUtils.TryParse(planetName, "spawnChance", spawnChance, typeof(float));
                distributionParams.range = (float)ConfigUtils.TryParse(planetName, "range", range, typeof(float));
                distributionParams.populationMultiplier = (int)ConfigUtils.TryParse(planetName, "populationMultiplier", populationMultiplier, typeof(int));
                distributionParams.minScale = (Vector3)ConfigUtils.TryParse(planetName, "minScale", minScale, typeof(Vector3));
                distributionParams.maxScale = (Vector3)ConfigUtils.TryParse(planetName, "maxScale", maxScale, typeof(Vector3));
                distributionParams.scaleRandomness = (float)ConfigUtils.TryParse(planetName, "scaleRandomness", scaleRandomness, typeof(float));
                distributionParams.noiseCutoff = (float)ConfigUtils.TryParse(planetName, "noiseCutoff", noiseCutoff, typeof(float));
                distributionParams.steepPower = (float)ConfigUtils.TryParse(planetName, "steepPower", steepPower, typeof(float));
                distributionParams.steepContrast = (float)ConfigUtils.TryParse(planetName, "steepContrast", steepContrast, typeof(float));
                distributionParams.steepMidpoint = (float)ConfigUtils.TryParse(planetName, "steepMidpoint", steepMidpoint, typeof(float));
                distributionParams.maxNormalDeviance = (float)ConfigUtils.TryParse(planetName, "maxNormalDeviance", maxNormalDeviance, typeof(float));
                distributionParams.minAltitude = (float)ConfigUtils.TryParse(planetName, "minAltitude", minAltitude, typeof(float));
                distributionParams.maxAltitude = (float)ConfigUtils.TryParse(planetName, "maxAltitude", maxAltitude, typeof(float));
                distributionParams.altitudeFadeRange = (float)ConfigUtils.TryParse(planetName, "altitudeFadeRange", altitudeFadeRange, typeof(float));
                distributionParams.alignToTerrainNormal = (bool)(ConfigUtils.TryParse(planetName, "alignToTerrainNormal", alignToTerrainNormal, typeof(bool))) ? 1 : 0;
                distributionParams.coloredByTerrain = (bool)ConfigUtils.TryParse(planetName, "coloredByTerrain", coloredByTerrain, typeof(bool));

                // If distributing to a fixed altitude
                if (placementAltitude != null)
                {
                    distributionParams.placementAltitude = (float)ConfigUtils.TryParse(planetName, "placementAltitude", placementAltitude, typeof(float));
                    distributionParams.fixedAltitude = true;
                }
                else
                {
                    distributionParams.fixedAltitude = false;
                }
            }

            ConfigNode lods = node.GetNode("LODs");
            ConfigNode[] lodNodes = lods.GetNodes("LOD");
            if (lodNodes.Length < 2)
            {
                ParallaxDebug.LogCritical("Unable to locate the required amount of 2 LOD nodes for a scatter on " + planetName);
            }
            distributionParams.lod1 = ParseLOD(planetName, lodNodes[0], baseMaterial);
            distributionParams.lod2 = ParseLOD(planetName, lodNodes[1], baseMaterial);

            return distributionParams;
        }
        public static BiomeBlacklistParams GetBiomeBlacklistParams(string planetName, ConfigNode node)
        {
            BiomeBlacklistParams blacklist = new BiomeBlacklistParams
            {
                blacklistedBiomes = new List<string>(),
                fastBlacklistedBiomes = new HashSet<string>()
            };

            ConfigNode blacklistNode = node.GetNode("BiomeBlacklist");
            if (blacklistNode == null)
            {
                return blacklist;
            }
            else
            {
                blacklist.blacklistedBiomes.AddRange(blacklistNode.GetValuesList("name"));
                blacklist.fastBlacklistedBiomes.AddAll(blacklist.blacklistedBiomes);
                return blacklist;
            }
        }
        public static LOD ParseLOD(string planetName, ConfigNode node, in MaterialParams baseMaterial)
        {
            LOD lod = new LOD();

            string range = ConfigUtils.TryGetConfigValue(node, "range");
            string model = ConfigUtils.TryGetConfigValue(node, "model");

            lod.range = (float)ConfigUtils.TryParse(planetName, "range", range, typeof(float));
            lod.modelPathOverride = model;

            //
            //  Material was overridden
            //

            ConfigNode overrideNode = node.GetNode("MaterialOverride");
            if (overrideNode != null)
            {
                // Copy existing data over
                lod.materialOverride = new MaterialParams();
                lod.materialOverride.shader = baseMaterial.shader;
                lod.materialOverride.shaderProperties = baseMaterial.shaderProperties.Clone() as ShaderProperties;
                lod.materialOverride.shaderKeywords = new List<string>(baseMaterial.shaderKeywords);
                lod.inheritsMaterial = true;

                // Now work out what has been replaced
                ProcessMaterialOverride(overrideNode, ref lod.materialOverride, planetName);
            }

            //
            // Material was replaced
            //

            ConfigNode materialNode = node.GetNode("Material");
            if (materialNode != null)
            {
                lod.materialOverride = GetMaterialParams(planetName, materialNode);
                lod.inheritsMaterial = false;
            }

            return lod;
        }
        public static void ProcessMaterialOverride(ConfigNode overrideNode, ref MaterialParams materialOverride, string planetName)
        {
            // Textures
            string[] textureKeys = materialOverride.shaderProperties.shaderTextures.Keys.ToArray();
            foreach (string key in textureKeys)
            {
                string configValue = overrideNode.GetValue(key);
                if (configValue != null)
                {
                    materialOverride.shaderProperties.shaderTextures[key] = (string)ConfigUtils.TryParse(planetName, key, configValue, typeof(string));
                }
            }

            // Floats
            string[] floatKeys = materialOverride.shaderProperties.shaderFloats.Keys.ToArray();
            foreach (string key in floatKeys)
            {
                string configValue = overrideNode.GetValue(key);
                if (configValue != null)
                {
                    materialOverride.shaderProperties.shaderFloats[key] = (float)ConfigUtils.TryParse(planetName, key, configValue, typeof(float));
                }
            }

            // Vectors
            string[] vectorKeys = materialOverride.shaderProperties.shaderVectors.Keys.ToArray();
            foreach (string key in vectorKeys)
            {
                string configValue = overrideNode.GetValue(key);
                if (configValue != null)
                {
                    materialOverride.shaderProperties.shaderVectors[key] = (Vector3)ConfigUtils.TryParse(planetName, key, configValue, typeof(Vector3));
                }
            }

            // Colors
            string[] colorKeys = materialOverride.shaderProperties.shaderColors.Keys.ToArray();
            foreach (string key in colorKeys)
            {
                string configValue = overrideNode.GetValue(key);
                if (configValue != null)
                {
                    materialOverride.shaderProperties.shaderColors[key] = (Color)ConfigUtils.TryParse(planetName, key, configValue, typeof(Color));
                }
            }

            // Ints
            string[] intKeys = materialOverride.shaderProperties.shaderInts.Keys.ToArray();
            foreach (string key in intKeys)
            {
                string configValue = overrideNode.GetValue(key);
                if (configValue != null)
                {
                    materialOverride.shaderProperties.shaderInts[key] = (int)ConfigUtils.TryParse(planetName, key, configValue, typeof(int));
                }
            }
        }
        public static MaterialParams GetMaterialParams(string planetName, ConfigNode node)
        {
            MaterialParams materialParams = new MaterialParams();

            // Look up the shader and then get the other params the config should contain from the shader bank
            string shader = ConfigUtils.TryGetConfigValue(node, "shader");

            materialParams.shader = shader;
            // Now parse the keywords
            ConfigNode keywordsNode = node.GetNode("Keywords");
            List<string> keywords = new List<string>();
            if (keywordsNode != null)
            {
                keywords.AddRange(keywordsNode.GetValuesList("name"));
            }
            materialParams.shaderKeywords = keywords;
            materialParams.shaderProperties = LookupTemplateConfig(GetConfigByName("ParallaxScatterShaderProperties"), shader, keywords);

            PopulateMaterialValues(ref materialParams, node, planetName);

            return materialParams;
        }
        public static void PopulateMaterialValues(ref MaterialParams materialParams, ConfigNode node, string planetName)
        {
            // Now we have the shader properties which contains the names (keys) and defaults (values) we can set everything except the textures, which use load on demand
            // Get values from config

            // Textures
            string[] textureKeys = materialParams.shaderProperties.shaderTextures.Keys.ToArray();
            foreach (string key in textureKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);

                if (!File.Exists(KSPUtil.ApplicationRootPath + "GameData/" + configValue))
                {
                    ParallaxDebug.LogCritical("This texture file doesn't exist: " + configValue + " for planet: " + planetName);
                }

                materialParams.shaderProperties.shaderTextures[key] = (string)ConfigUtils.TryParse(planetName, key, configValue, typeof(string));
            }

            // Floats
            string[] floatKeys = materialParams.shaderProperties.shaderFloats.Keys.ToArray();
            foreach (string key in floatKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderFloats[key] = (float)ConfigUtils.TryParse(planetName, key, configValue, typeof(float));
            }

            // Vectors
            string[] vectorKeys = materialParams.shaderProperties.shaderVectors.Keys.ToArray();
            foreach (string key in vectorKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderVectors[key] = (Vector3)ConfigUtils.TryParse(planetName, key, configValue, typeof(Vector3));
            }

            // Colors
            string[] colorKeys = materialParams.shaderProperties.shaderColors.Keys.ToArray();
            foreach (string key in colorKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderColors[key] = (Color)ConfigUtils.TryParse(planetName, key, configValue, typeof(Color));
            }

            // Ints
            string[] intKeys = materialParams.shaderProperties.shaderInts.Keys.ToArray();
            foreach (string key in intKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderInts[key] = (int)ConfigUtils.TryParse(planetName, key, configValue, typeof(int));
            }
        }
        public static void PerformNormalisationConversions(Scatter scatter)
        {
            // Back up originals
            scatter.distributionParams.originalRange = scatter.distributionParams.range;
            scatter.distributionParams.originalPopulationMultiplier = scatter.distributionParams.populationMultiplier;

            // Apply global settings
            scatter.distributionParams.range *= parallaxGlobalSettings.scatterGlobalSettings.rangeMultiplier;
            scatter.distributionParams.populationMultiplier = (int)((float)scatter.distributionParams.populationMultiplier * parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier);
            if (scatter.distributionParams.populationMultiplier == 0) { scatter.distributionParams.populationMultiplier = 1; }

            // Normalise the LOD distances as a percentage of max range
            scatter.distributionParams.lod1.range /= scatter.distributionParams.range;
            scatter.distributionParams.lod2.range /= scatter.distributionParams.range;

            // Normalise the frustum culling start range as a percentage of max range
            scatter.optimizationParams.frustumCullingIgnoreRadius /= scatter.distributionParams.range;
        }
        public static void ReverseNormalisationConversions(Scatter scatter)
        {
            // Reverse global settings
            scatter.distributionParams.lod1.range *= scatter.distributionParams.range;
            scatter.distributionParams.lod2.range *= scatter.distributionParams.range;

            scatter.optimizationParams.frustumCullingIgnoreRadius *= scatter.distributionParams.range;

            scatter.distributionParams.range = scatter.distributionParams.originalRange;
            scatter.distributionParams.populationMultiplier = scatter.distributionParams.originalPopulationMultiplier;
        }
        static void PerformAdditionalOperations(Scatter scatter)
        {
            // Check if the models actually exist
            string lod0ModelPath = GameDataPath + scatter.modelPath + ".mu";
            string lod1ModelPath = GameDataPath + scatter.distributionParams.lod1.modelPathOverride + ".mu";
            string lod2ModelPath = GameDataPath + scatter.distributionParams.lod2.modelPathOverride + ".mu";
            if (!File.Exists(lod0ModelPath) || GameDatabase.Instance.GetModel(scatter.modelPath) == null)
            {
                ParallaxDebug.LogCritical("This model file doesn't exist: " + scatter.modelPath + " on scatter: " + scatter.scatterName);
            }
            if (!File.Exists(lod1ModelPath) || GameDatabase.Instance.GetModel(scatter.distributionParams.lod1.modelPathOverride) == null)
            {
                ParallaxDebug.LogCritical("This model file doesn't exist: " + scatter.distributionParams.lod1.modelPathOverride + " on scatter: " + scatter.scatterName);
            }
            if (!File.Exists(lod2ModelPath) || GameDatabase.Instance.GetModel(scatter.distributionParams.lod2.modelPathOverride) == null)
            {
                ParallaxDebug.LogCritical("This model file doesn't exist: " + scatter.distributionParams.lod2.modelPathOverride + " on scatter: " + scatter.scatterName);
            }

            // Cube the normal deviance, as it gives better results and becomes more sensitive to larger values
            scatter.distributionParams.maxNormalDeviance = scatter.distributionParams.maxNormalDeviance * scatter.distributionParams.maxNormalDeviance * scatter.distributionParams.maxNormalDeviance;

            // Fade range function is NaN when fade range is 0
            if (scatter.distributionParams.altitudeFadeRange == 0)
            {
                scatter.distributionParams.altitudeFadeRange = 0.05f;
            }

            // Calculate the LOD1 mesh's largest bound for colliders
            scatter.sqrMeshBound = scatter.CalculateSqrLargestBound(scatter.distributionParams.lod1.modelPathOverride);
        }
        static void ApplyCompatibilityPatches()
        {
            // Sigma Dimensions
            UrlDir.UrlConfig sigDimConfig = GetConfigByName("SigmaDimensions");

            if (sigDimConfig == null)
            {
                return;
            }
            else
            {
                ParallaxDebug.Log("Sigma Dimensions detected, applying rescale values");
            }

            ConfigNode sigmaDimensionsNode = sigDimConfig.config;

            float resizeValue = float.Parse(sigmaDimensionsNode.GetValue("Resize"));
            float landscapeValue = float.Parse(sigmaDimensionsNode.GetValue("landscape"));

            foreach (ParallaxScaledBody scaledBody in parallaxScaledBodies.Values)
            {
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

            foreach (ParallaxTerrainBody terrainBody in parallaxTerrainBodies.Values)
            {
                terrainBody.terrainShaderProperties.shaderFloats["_LowMidBlendStart"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_LowMidBlendEnd"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_MidHighBlendStart"] *= resizeValue * landscapeValue;
                terrainBody.terrainShaderProperties.shaderFloats["_MidHighBlendEnd"] *= resizeValue * landscapeValue;

                terrainBody.SetMaterialValues();
            }
        }
        void OnDestroy()
        {

        }
    }
}
