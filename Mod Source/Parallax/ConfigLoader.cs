using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

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

        // Stores transparent material for terrain quads
        public static Material transparentMaterial;

        // Debug wireframe material
        public static Material wireframeMaterial;

        // Configs that start with 'Parallax' as the root node
        public UrlDir.UrlConfig globalNode;
        public void Awake()
        {
            ParallaxDebug.Log("Starting!");
        }
        // Entry point
        public static void ModuleManagerPostLoad()
        {
            ParallaxDebug.Log("Beginning config load");
            AssetBundleLoader.Initialize();
            InitializeGlobalSettings(GetConfigByName("ParallaxGlobal"));

            // Initialize the shader template - holds the names of textures, floats, etc and initializes their defaults
            shaderPropertiesTemplate = new ShaderProperties();
            InitializeTemplateConfig(GetConfigByName("ParallaxShaderProperties").config, shaderPropertiesTemplate);

            LoadTerrainConfigs(GetConfigsByName("Parallax"));
            LoadScatterConfigs(GetConfigsByName("ParallaxScatters"));

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
        public static void InitializeGlobalSettings(UrlDir.UrlConfig config)
        {
            ConfigNode terrainSettingsNode = config.config.GetNode("TerrainShaderSettings");
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellation = float.Parse(terrainSettingsNode.GetValue("maxTessellation"));
            parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength = float.Parse(terrainSettingsNode.GetValue("tessellationEdgeLength"));
            parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange = float.Parse(terrainSettingsNode.GetValue("maxTessellationRange"));

            ConfigNode debugSettingsNode = config.config.GetNode("DebugSettings");
            parallaxGlobalSettings.debugGlobalSettings.wireframeTerrain = bool.Parse(debugSettingsNode.GetValue("wireframeTerrain"));
        }
        public static ShaderProperties LookupTemplateConfig(UrlDir.UrlConfig config, string shaderName, List<string> keywords)
        {
            // Config starts at 'ParallaxScatterShaderProperties'
            // 'ParallaxShader'
            ShaderProperties shaderProperties = new ShaderProperties();

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

                    ConfigNode keywordsNode = node.GetNode("Keywords");
                    foreach (string keyword in keywords)
                    {
                        ConfigNode keywordNode = keywordsNode.GetNode(keyword);

                        if (keywordNode == null) 
                        { 
                            ParallaxDebug.LogError("Keyword " + keyword + " could not be found!");
                            continue;
                        }

                        // Initialize the properties this keyword adds
                        InitializeTemplateConfig(keywordNode, shaderProperties);
                    }
                }
            }
            return shaderProperties;
        }
        // Template configs tell Parallax what variable names and type are supported by the shader
        // We can use this recursively almost with shader keywords
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
            // "Parallax"
            foreach (UrlDir.UrlConfig rootNode in allRootNodes)
            {
                // "Body"
                ConfigNode.ConfigNodeList nodes = rootNode.config.nodes;
                foreach (ConfigNode planetNode in nodes)
                {
                    string planetName = planetNode.GetValue("name");

                    // TODO: Add emissive support here
                    ParallaxTerrainBody body = new ParallaxTerrainBody(planetName);

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
                object result = 0;
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(float), out result);
                ParallaxDebug.Log("Parsing float name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderFloats[propertyName] = (float)result;
            }
            foreach (string propertyName in vectorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                object result = Vector3.zero;
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Vector3), out result);
                ParallaxDebug.Log("Parsing vector name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderVectors[propertyName] = (Vector3)result;
            }
            foreach (string propertyName in colorProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                object result = Color.black;
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(Color), out result);
                ParallaxDebug.Log("Parsing color name: " + configValue);
                ParallaxDebug.Log("Property name: " + propertyName);
                body.terrainShaderProperties.shaderColors[propertyName] = (Color)result;
            }
            foreach (string propertyName in intProperties)
            {
                string configValue = bodyNode.GetValue(propertyName);
                object result = 0;
                ConfigUtils.TryParse(body.planetName, propertyName, configValue, typeof(int), out result);
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

                string nearestQuadSubdivisionLevel = rootNode.config.GetValue("nearestQuadSubdivisionLevel");
                string nearestQuadSubdivisionRange = rootNode.config.GetValue("nearestQuadSubdivisionRange");

                int nearestQuadSubdivisionLevelValue = (int)ConfigUtils.TryParse(body, "nearestQuadSubdivisionLevel", nearestQuadSubdivisionLevel, typeof(int));
                float nearestQuadSubdivisionRangeValue = (float)ConfigUtils.TryParse(body, "nearestQuadSubdivisionRange", nearestQuadSubdivisionRange, typeof(float));

                ParallaxScatterBody scatterBody = new ParallaxScatterBody(body);
                scatterBody.nearestQuadSubdivisionLevel = nearestQuadSubdivisionLevelValue;
                scatterBody.nearestQuadSubdivisionRange = nearestQuadSubdivisionRangeValue;
                // "Scatter"
                ConfigNode.ConfigNodeList nodes = rootNode.config.nodes;
                foreach (ConfigNode node in nodes)
                {
                    string scatterName = body + "-" + ConfigUtils.TryGetConfigValue(node, "name");
                    string model = ConfigUtils.TryGetConfigValue(node, "model");

                    Scatter scatter = new Scatter(scatterName);
                    scatter.modelPath = model;

                    OptimizationParams optimizationParams = GetOptimizationParams(body, node);
                    SubdivisionParams subdivisionParams = GetSubdivisionParams(body, node.GetNode("SubdivisionSettings"));
                    NoiseParams noiseParams = GetNoiseParams(body, node.GetNode("DistributionNoise"));
                    MaterialParams materialParams = GetMaterialParams(body, node.GetNode("Material"));
                    DistributionParams distributionParams = GetDistributionParams(body, node.GetNode("Distribution"), materialParams);

                    scatter.optimizationParams = optimizationParams;
                    scatter.subdivisionParams = subdivisionParams;
                    scatter.noiseParams = noiseParams;
                    scatter.materialParams = materialParams;
                    scatter.distributionParams = distributionParams;

                    PerformNormalisationConversions(scatter);

                    scatterBody.scatters.Add(scatterName, scatter);
                }
                scatterBody.fastScatters = scatterBody.scatters.Values.ToArray();
                Debug.Log("Adding body with name: " + body + " to scatter bodies");
                parallaxScatterBodies.Add(body, scatterBody);
            }
        }
        public static OptimizationParams GetOptimizationParams(string planetName, ConfigNode node)
        {
            OptimizationParams optimizationParams = new OptimizationParams();

            string frustumCullingIgnoreRadius = ConfigUtils.TryGetConfigValue(node, "cullingRange");
            string frustumCullingSafetyMargin = ConfigUtils.TryGetConfigValue(node, "cullingLimit");
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

            if (subdivisionParams.subdivisionMode != SubdivisionMode.noSubdivision)
            {
                string subdivisionLevel = ConfigUtils.TryGetConfigValue(node, "subdivisionLevel");
                subdivisionParams.maxSubdivisionLevel = (int)ConfigUtils.TryParse(planetName, "subdivisionLevel", subdivisionLevel, typeof(int));
            }

            return subdivisionParams;
        }
        public static NoiseParams GetNoiseParams(string planetName, ConfigNode node)
        {
            NoiseParams noiseParams = new NoiseParams();

            string frequency = ConfigUtils.TryGetConfigValue(node, "frequency");
            string octaves = ConfigUtils.TryGetConfigValue(node, "octaves");
            string persistence = ConfigUtils.TryGetConfigValue(node, "persistence");
            string seed = ConfigUtils.TryGetConfigValue(node, "seed");
            string noiseType = ConfigUtils.TryGetConfigValue(node, "noiseType");

            noiseParams.frequency = (float)ConfigUtils.TryParse(planetName, "frequency", frequency, typeof(float));
            noiseParams.octaves = (int)ConfigUtils.TryParse(planetName, "octaves", octaves, typeof(int));
            noiseParams.persistence = (float)ConfigUtils.TryParse(planetName, "persistence", persistence, typeof(float));
            noiseParams.seed = (int)ConfigUtils.TryParse(planetName, "seed", seed, typeof(int));
            noiseParams.noiseType = (NoiseType)Enum.Parse(typeof(NoiseType), noiseType);

            return noiseParams;
        }
        public static DistributionParams GetDistributionParams(string planetName, ConfigNode node, in MaterialParams baseMaterial)
        {
            DistributionParams distributionParams = new DistributionParams();

            string seed = ConfigUtils.TryGetConfigValue(node, "seed");
            string spawnChance = ConfigUtils.TryGetConfigValue(node, "spawnChance");
            string range = ConfigUtils.TryGetConfigValue(node, "range");
            string populationMultiplier = ConfigUtils.TryGetConfigValue(node, "populationMultiplier");
            string minScale = ConfigUtils.TryGetConfigValue(node, "minScale");
            string maxScale = ConfigUtils.TryGetConfigValue(node, "maxScale");
            string noiseCutoff = ConfigUtils.TryGetConfigValue(node, "cutoffScale");
            string steepPower = ConfigUtils.TryGetConfigValue(node, "steepPower");
            string steepContrast = ConfigUtils.TryGetConfigValue(node, "steepContrast");
            string steepMidpoint = ConfigUtils.TryGetConfigValue(node, "steepMidpoint");
            string maxNormalDeviance = ConfigUtils.TryGetConfigValue(node, "maxNormalDeviance");
            string minAltitude = ConfigUtils.TryGetConfigValue(node, "minAltitude");
            string maxAltitude = ConfigUtils.TryGetConfigValue(node, "maxAltitude");
            
            distributionParams.seed = (float)ConfigUtils.TryParse(planetName, "seed", seed, typeof(float));
            distributionParams.spawnChance = (float)ConfigUtils.TryParse(planetName, "spawnChance", spawnChance, typeof(float));
            distributionParams.range = (float)ConfigUtils.TryParse(planetName, "range", range, typeof(float));
            distributionParams.populationMultiplier = (int)ConfigUtils.TryParse(planetName, "populationMultiplier", populationMultiplier, typeof(int));
            distributionParams.minScale = (Vector3)ConfigUtils.TryParse(planetName, "minScale", minScale, typeof(Vector3));
            distributionParams.maxScale = (Vector3)ConfigUtils.TryParse(planetName, "maxScale", maxScale, typeof(Vector3));
            distributionParams.noiseCutoff = (float)ConfigUtils.TryParse(planetName, "noiseCutoff", noiseCutoff, typeof(float));
            distributionParams.steepPower = (float)ConfigUtils.TryParse(planetName, "steepPower", steepPower, typeof(float));
            distributionParams.steepContrast = (float)ConfigUtils.TryParse(planetName, "steepContrast", steepContrast, typeof(float));
            distributionParams.steepMidpoint = (float)ConfigUtils.TryParse(planetName, "steepMidpoint", steepMidpoint, typeof(float));
            distributionParams.maxNormalDeviance = (float)ConfigUtils.TryParse(planetName, "maxNormalDeviance", maxNormalDeviance, typeof(float));
            distributionParams.minAltitude = (float)ConfigUtils.TryParse(planetName, "minAltitude", minAltitude, typeof(float));
            distributionParams.maxAltitude = (float)ConfigUtils.TryParse(planetName, "maxAltitude", maxAltitude, typeof(float));

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
                lod.materialOverride.shaderKeywords = baseMaterial.shaderKeywords;

                // Now work out what has been replaced

                // Textures
                string[] textureKeys = lod.materialOverride.shaderProperties.shaderTextures.Keys.ToArray();
                for (int i = 0; i < textureKeys.Length; i++)
                {
                    string key = textureKeys[i];
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderTextures[key] = (string)ConfigUtils.TryParse(planetName, key, configValue, typeof(string));
                    }
                }

                // Floats
                string[] floatKeys = lod.materialOverride.shaderProperties.shaderFloats.Keys.ToArray();
                for (int i = 0; i < floatKeys.Length; i++)
                {
                    string key = floatKeys[i];
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderFloats[key] = (float)ConfigUtils.TryParse(planetName, key, configValue, typeof(float));
                    }
                }

                // Vectors
                string[] vectorKeys = lod.materialOverride.shaderProperties.shaderVectors.Keys.ToArray();
                for (int i = 0; i < vectorKeys.Length; i++)
                {
                    string key = vectorKeys[i];
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderVectors[key] = (Vector3)ConfigUtils.TryParse(planetName, key, configValue, typeof(Vector3));
                    }
                }

                // Colors
                string[] colorKeys = lod.materialOverride.shaderProperties.shaderColors.Keys.ToArray();
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    string key = colorKeys[i];
                    string configValue = overrideNode.GetValue(key);
                    if (configValue != null)
                    {
                        lod.materialOverride.shaderProperties.shaderColors[key] = (Color)ConfigUtils.TryParse(planetName, key, configValue, typeof(Color));
                    }
                }

                // Ints
                string[] intKeys = lod.materialOverride.shaderProperties.shaderInts.Keys.ToArray();
                for (int i = 0; i < intKeys.Length; i++)
                {
                    string key = intKeys[i];
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
            for (int i = 0; i < textureKeys.Length; i++)
            {
                string key = textureKeys[i];
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderTextures[key] = (string)ConfigUtils.TryParse(planetName, key, configValue, typeof(string));
            }

            // Floats
            string[] floatKeys = materialParams.shaderProperties.shaderFloats.Keys.ToArray();
            for (int i = 0; i < floatKeys.Length; i++)
            {
                string key = floatKeys[i];
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderFloats[key] = (float)ConfigUtils.TryParse(planetName, key, configValue, typeof(float));
            }

            // Vectors
            string[] vectorKeys = materialParams.shaderProperties.shaderVectors.Keys.ToArray();
            for (int i = 0; i < vectorKeys.Length; i++)
            {
                string key = vectorKeys[i];
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderVectors[key] = (Vector3)ConfigUtils.TryParse(planetName, key, configValue, typeof(Vector3));
            }

            // Colors
            string[] colorKeys = materialParams.shaderProperties.shaderColors.Keys.ToArray();
            for (int i = 0; i < colorKeys.Length; i++)
            {
                string key = colorKeys[i];
                string configValue = ConfigUtils.TryGetConfigValue(node, key);
                materialParams.shaderProperties.shaderColors[key] = (Color)ConfigUtils.TryParse(planetName, key, configValue, typeof(Color));
            }

            // Ints
            string[] intKeys = materialParams.shaderProperties.shaderInts.Keys.ToArray();
            for (int i = 0; i < intKeys.Length; i++)
            {
                string key = intKeys[i];
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
        void OnDestroy()
        {

        }
    }
}
