using Kopernicus.Configuration;
using Parallax.Debugging;
using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Rendering;
using static Targeting;

//
// Config upgrade notes:
// 1. "Textures" node in ParallaxTerrain.cfg needs to be renamed to ShaderProperties
// 2. Vars now need to be named according to the ShaderPropertiesTemplate.cfg

[assembly: KSPAssembly("Parallax", 1, 0)]
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

        // Stores all parallax scatter bodies by planet name
        public static Dictionary<string, ParallaxScatterBody> parallaxScatterBodies = new Dictionary<string, ParallaxScatterBody>();

        // Stores a cache of compute shaders to prevent slow instantiation
        public static ObjectPool<ComputeShader> computeShaderPool;
        public static ObjectPool<GameObject> colliderPool;

        // Stores transparent material for terrain quads
        public static Material transparentMaterial;

        // Debug wireframe material
        public static Material wireframeMaterial;

        // Configs that start with 'Parallax' as the root node
        public UrlDir.UrlConfig globalNode;
        public void Awake()
        {
            ParallaxDebug.Log("Starting!");
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(typeof(AsyncGPUReadbackRequest));
            Debug.Log(assembly.Location);
        }
        // Entry point
        public static void ModuleManagerPostLoad()
        {
            ParallaxDebug.Log("Beginning config load");

            // Get system information
            ParallaxSystemInfo.ReadInfo();
            ParallaxSystemInfo.LogInfo();

            AssetBundleLoader.Initialize();
            InitializeGlobalSettings(GetConfigByName("ParallaxGlobal"));

            // Initialize the shader template - holds the names of textures, floats, etc and initializes their defaults
            shaderPropertiesTemplate = new ShaderProperties();
            InitializeTemplateConfig(GetConfigByName("ParallaxShaderProperties").config, shaderPropertiesTemplate);

            LoadTerrainConfigs(GetConfigsByName("ParallaxTerrain"));
            LoadScatterConfigs(GetConfigsByName("ParallaxScatters"));
            LoadSharedScatterConfigs(GetConfigsByName("ParallaxScatters"));
            InitializeObjectPools(parallaxGlobalSettings);

            transparentMaterial = new Material(Shader.Find("Unlit/Transparent"));
            transparentMaterial.SetTexture("_MainTex", Resources.FindObjectsOfTypeAll<Texture>().FirstOrDefault(t => t.name == "Parallax/BlankAlpha"));

            wireframeMaterial = new Material(AssetBundleLoader.parallaxTerrainShaders["Custom/Wireframe"]);
        }
        public static UrlDir.UrlConfig[] GetConfigsByName(string name)
        {
            return GameDatabase.Instance.GetConfigs(name);
        }
        public static UrlDir.UrlConfig GetConfigByName(string name)
        {
            return GameDatabase.Instance.GetConfigs(name)[0];
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
        public static UrlDir.UrlConfig GetPlanetScatterNode(string planetName)
        {
            UrlDir.UrlConfig[] baseConfig = GetConfigsByName("ParallaxScatters");
            return baseConfig.FirstOrDefault(x => x.config.GetValue("body") == planetName);
        }
        public static ConfigNode GetScatterConfigNode(string planetName, string scatterName, bool isShared = false)
        {
            return GetScatterConfigNode(planetName, scatterName, GetPlanetScatterNode(planetName), isShared);
        }
        public static ConfigNode GetScatterConfigNode(string planetName, string scatterName, UrlDir.UrlConfig baseConfig, bool isShared = false)
        {
            // Gets the scatter node
            if (!isShared)
            {
                // Gets the scatter node
                return baseConfig.config.GetNodes("Scatter").FirstOrDefault(x => (planetName + "-" + x.GetValue("name")) == scatterName);
            }
            else
            {
                return baseConfig.config.GetNodes("SharedScatter").FirstOrDefault(x => (planetName + "-" + x.GetValue("name")) == scatterName);
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
            ConfigNode terrainSettingsNode = config.config.GetNode("TerrainShaderSettings");
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellation = float.Parse(terrainSettingsNode.GetValue("maxTessellation"));
            parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength = float.Parse(terrainSettingsNode.GetValue("tessellationEdgeLength"));
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange = float.Parse(terrainSettingsNode.GetValue("maxTessellationRange"));

            ConfigNode scatterSettingsNode = config.config.GetNode("ScatterSystemSettings");
            parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier = float.Parse(scatterSettingsNode.GetValue("densityMultiplier"));
            parallaxGlobalSettings.scatterGlobalSettings.rangeMultiplier = float.Parse(scatterSettingsNode.GetValue("rangeMultiplier"));
            parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange = float.Parse(scatterSettingsNode.GetValue("fadeOutStartRange"));
            parallaxGlobalSettings.scatterGlobalSettings.collisionLevel = float.Parse(scatterSettingsNode.GetValue("collisionLevel"));

            ConfigNode debugSettingsNode = config.config.GetNode("DebugSettings");
            parallaxGlobalSettings.debugGlobalSettings.wireframeTerrain = bool.Parse(debugSettingsNode.GetValue("wireframeTerrain"));

            ConfigNode objectPoolsNode = config.config.GetNode("ObjectPoolSettings");
            parallaxGlobalSettings.objectPoolSettings.cachedComputeShaderCount = int.Parse(objectPoolsNode.GetValue("cachedComputeShaderCount"));
            parallaxGlobalSettings.objectPoolSettings.cachedColliderCount = int.Parse(objectPoolsNode.GetValue("cachedColliderCount"));
        }
        public static void InitializeObjectPools(ParallaxSettings settings)
        {
            ComputeShader templateComputeShader = Instantiate(AssetBundleLoader.parallaxComputeShaders["TerrainScatters"]);
            computeShaderPool = new ObjectPool<ComputeShader>(templateComputeShader, settings.objectPoolSettings.cachedComputeShaderCount);

            GameObject templateCollider = new GameObject("ParallaxCollider");
            templateCollider.AddComponent<MeshCollider>();
            templateCollider.SetActive(false);
            colliderPool = new ObjectPool<GameObject>(templateCollider, settings.objectPoolSettings.cachedColliderCount);
        }
        // Looks up a shader in the shader bank
        public static ShaderProperties LookupTemplateConfig(UrlDir.UrlConfig config, string shaderName, List<string> keywords)
        {
            // Config starts at 'ParallaxScatterShaderProperties'
            // 'ParallaxShader'
            ShaderProperties shaderProperties = new ShaderProperties();
            List<string> keywordsToRemove = new List<string>();

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
                            ParallaxDebug.LogError("Keyword " + keyword + " could not be found!");
                            continue;
                        }

                        string supersededBy = keywordNode.GetValue("supersededBy");
                        if (supersededBy != null)
                        {
                            // This keyword is overridden by another keyword. Now check if that keyword is enabled on the shader (linear search)
                            if (keywords.Contains(supersededBy))
                            {
                                ParallaxDebug.Log("This keyword is superseded by " + supersededBy + ", skipping!");
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
        public static void LoadTerrainConfigs(UrlDir.UrlConfig[] allRootNodes)
        {
            // "ParallaxTerrain"
            foreach (UrlDir.UrlConfig rootNode in allRootNodes)
            {
                // "Body"
                ConfigNode.ConfigNodeList nodes = rootNode.config.nodes;
                foreach (ConfigNode planetNode in nodes)
                {
                    string planetName = planetNode.GetValue("name");
                    string emissive = planetNode.GetValue("emissive");
                    bool isEmissive = emissive == null ? false : (bool.Parse(emissive) == true ? true : false);
                    
                    ParallaxTerrainBody body = new ParallaxTerrainBody(planetName);
                    body.emissive = isEmissive;

                    ParseNewBody(body, planetNode.GetNode("ShaderProperties"));
                    body.LoadInitial();
                    parallaxTerrainBodies.Add(planetName, body);
                }
            }
        }
        public static void ParseNewBody(ParallaxTerrainBody body, ConfigNode bodyNode)
        {
            ParallaxDebug.Log("Parsing new body: " + body.planetName);
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
                ParallaxDebug.Log("Parsing texture name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderTextures[propertyName] = configValue;
            }
            foreach (string propertyName in floatProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(float), out object result);
                ParallaxDebug.Log("Parsing float name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderFloats[propertyName] = (float)result;
            }
            foreach (string propertyName in vectorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Vector3), out object result);
                ParallaxDebug.Log("Parsing vector name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderVectors[propertyName] = (Vector3)result;
            }
            foreach (string propertyName in colorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Color), out object result);
                ParallaxDebug.Log("Parsing color name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderColors[propertyName] = (Color)result;
            }
            foreach (string propertyName in intProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(int), out object result);
                ParallaxDebug.Log("Parsing float name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderFloats[propertyName] = (int)result;
            }
        }

        //
        //  Scatter parsing
        //

        public static void LoadScatterConfigs(UrlDir.UrlConfig[] allRootNodes)
        {
            // "ParallaxScatters"
            foreach (UrlDir.UrlConfig rootNode in allRootNodes)
            {
                string body = rootNode.config.GetValue("body");
                string configVersion = rootNode.config.GetValue("configVersion");
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
                ConfigNode[] nodes = rootNode.config.GetNodes("Scatter");
                foreach (ConfigNode node in nodes)
                {
                    string scatterName = body + "-" + ConfigUtils.TryGetConfigValue(node, "name");
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

                    if (collisionLevel >= parallaxGlobalSettings.scatterGlobalSettings.collisionLevel)
                    {
                        scatter.collideable = true;
                        collideableIndex++;
                        scatter.collideableArrayIndex = collideableIndex;
                        collideableScatters.Add(scatter);
                    }

                    Debug.Log("Adding scatter: " + scatterName);
                    scatterBody.scatters.Add(scatterName, scatter);
                }

                Debug.Log("Adding body with name: " + body + " to scatter bodies");
                scatterBody.fastScatters = scatterBody.scatters.Values.ToArray();
                scatterBody.collideableScatters = collideableScatters.ToArray();
                parallaxScatterBodies.Add(body, scatterBody);
            }
        }
        public static void LoadSharedScatterConfigs(UrlDir.UrlConfig[] allRootNodes)
        {
            foreach (UrlDir.UrlConfig rootNode in allRootNodes)
            {
                string body = rootNode.config.GetValue("body");

                ParallaxScatterBody scatterBody = parallaxScatterBodies[body];

                // Process shared scatters - These are scatters that share distribution data with another, so it doesn't need to be generated again

                ConfigNode[] sharedNodes = rootNode.config.GetNodes("SharedScatter");
                foreach (ConfigNode node in sharedNodes)
                {
                    string scatterName = body + "-" + ConfigUtils.TryGetConfigValue(node, "name");
                    string parentName = body + "-" + ConfigUtils.TryGetConfigValue(node, "parentName");
                    string model = ConfigUtils.TryGetConfigValue(node, "model");

                    if (!parallaxScatterBodies[body].scatters.ContainsKey(parentName))
                    {
                        ParallaxDebug.LogError("Shared scatter: " + scatterName + " inherits from parent scatter: " + parentName + " but that scatter does not exist!");
                        break;
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
        public static OptimizationParams GetOptimizationParams(string planetName, ConfigNode node)
        {
            OptimizationParams optimizationParams = new OptimizationParams();

            string frustumCullingIgnoreRadius = ConfigUtils.TryGetConfigValue(node, "frustumCullingStartRange");
            string frustumCullingSafetyMargin = ConfigUtils.TryGetConfigValue(node, "frustumCullingScreenMargin");
            string maxRenderableObjects = ConfigUtils.TryGetConfigValue(node, "maxObjects");

            optimizationParams.frustumCullingIgnoreRadius = (float)ConfigUtils.TryParse(planetName, "cullingRange", frustumCullingIgnoreRadius, typeof(float));
            optimizationParams.frustumCullingSafetyMargin = (float)ConfigUtils.TryParse(planetName, "cullingLimit", frustumCullingSafetyMargin, typeof(float));
            optimizationParams.maxRenderableObjects = (int)ConfigUtils.TryParse(planetName, "maxObjects", maxRenderableObjects, typeof(int));

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
                ParallaxDebug.LogError("Unable to locate the required amount of 2 LOD nodes for this scatter");
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

                // Textures
                string[] textureKeys = lod.materialOverride.shaderProperties.shaderTextures.Keys.ToArray();
                foreach (string key in textureKeys)
                {
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderTextures[key] = (string)ConfigUtils.TryParse(planetName, key, configValue, typeof(string));
                    }
                }

                // Floats
                string[] floatKeys = lod.materialOverride.shaderProperties.shaderFloats.Keys.ToArray();
                foreach (string key in floatKeys)
                {
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderFloats[key] = (float)ConfigUtils.TryParse(planetName, key, configValue, typeof(float));
                    }
                }

                // Vectors
                string[] vectorKeys = lod.materialOverride.shaderProperties.shaderVectors.Keys.ToArray();
                foreach (string key in vectorKeys)
                {
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderVectors[key] = (Vector3)ConfigUtils.TryParse(planetName, key, configValue, typeof(Vector3));
                    }
                }

                // Colors
                string[] colorKeys = lod.materialOverride.shaderProperties.shaderColors.Keys.ToArray();
                foreach (string key in colorKeys)
                {
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderColors[key] = (Color)ConfigUtils.TryParse(planetName, key, configValue, typeof(Color));
                    }
                }

                // Ints
                string[] intKeys = lod.materialOverride.shaderProperties.shaderInts.Keys.ToArray();
                foreach (string key in intKeys)
                {
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderInts[key] = (int)ConfigUtils.TryParse(planetName, key, configValue, typeof(int));
                    }
                }
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

            // Now we have the shader properties which contains the names (keys) and defaults (values) we can set everything except the textures, which use load on demand

            // Get values from config

            // Textures
            string[] textureKeys = materialParams.shaderProperties.shaderTextures.Keys.ToArray();
            foreach (string key in textureKeys)
            {
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
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

            return materialParams;
        }
        static void PerformNormalisationConversions(Scatter scatter)
        {
            // Normalise the LOD distances as a percentage of max range
            scatter.distributionParams.lod1.range /= scatter.distributionParams.range;
            scatter.distributionParams.lod2.range /= scatter.distributionParams.range;

            // Normalise the frustum culling start range as a percentage of max range
            scatter.optimizationParams.frustumCullingIgnoreRadius /= scatter.distributionParams.range;
        }
        static void PerformAdditionalOperations(Scatter scatter)
        {
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
        void OnDestroy()
        {

        }
    }
}
