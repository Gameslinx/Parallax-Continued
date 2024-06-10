using Parallax.Harmony_Patches;
using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Parallax.Legacy.LegacyConfigLoader;

namespace Parallax
{
    //
    //  Loader Common
    //

    // Holds ParallaxGlobalSettings.cfg values for terrain shader and scatters
    public class ParallaxSettings
    {
        public TerrainGlobalSettings terrainGlobalSettings = new TerrainGlobalSettings();
        public ScatterGlobalSettings scatterGlobalSettings = new ScatterGlobalSettings();
        public DebugGlobalSettings debugGlobalSettings = new DebugGlobalSettings();
        public ObjectPoolSettings objectPoolSettings = new ObjectPoolSettings();
    }
    public struct TerrainGlobalSettings
    {
        public float maxTessellation;
        public float tessellationEdgeLength;
        public float maxTessellationRange;
    }
    public struct ScatterGlobalSettings
    {
        public float densityMultiplier;
        public float rangeMultiplier;
        public float fadeOutStartRange;
    }
    public struct DebugGlobalSettings
    {
        public bool wireframeTerrain;
    }
    public struct ObjectPoolSettings
    {
        public int cachedComputeShaderCount;
    }
    // Stores the loaded values from the configs for each planet, except for the textures which are stored via file path
    // Textures are loaded On-Demand and stored in loadedTextures, where they are unloaded on scene change
    public class ParallaxTerrainBody
    {
        public string planetName;
        public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

        // Terrain materials
        public ParallaxMaterials parallaxMaterials = new ParallaxMaterials();

        public ShaderProperties terrainShaderProperties;
        public bool loaded = false;
        public ParallaxTerrainBody(string planetName)
        {
            this.planetName = planetName;
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
        public void Load(bool loadTextures)
        {
            if (loadTextures)
            {
                foreach (KeyValuePair<string, string> textureValue in terrainShaderProperties.shaderTextures)
                {
                    bool linear = false;
                    Debug.Log("Attempting load: " + textureValue.Key + " at " + textureValue.Value);
                    if (loadedTextures.ContainsKey(textureValue.Key))
                    {
                        if (loadedTextures[textureValue.Key] != null)
                        {
                            Debug.Log("This texture is already loaded!");
                            continue;
                        }
                        else
                        {
                            Debug.Log("The key exists, but the texture is null");
                        }
                    }
                    // Bump maps need to be linear, while everything else sRGB
                    // This could be handled better, tbh, but at least we're accounting for linear textures this time around
                    linear = TextureUtils.IsLinear(textureValue.Key);
                    Texture2D tex = TextureLoader.LoadTexture(textureValue.Value, linear);

                    parallaxMaterials.parallaxLow.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxMid.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxHigh.SetTexture(textureValue.Key, tex);

                    parallaxMaterials.parallaxLowMid.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxMidHigh.SetTexture(textureValue.Key, tex);

                    parallaxMaterials.parallaxFull.SetTexture(textureValue.Key, tex);

                    loadedTextures.Add(textureValue.Key, tex);
                    // Add to active textures
                }
            }
            loaded = true;
        }
        public void Unload()
        {
            // Unload all textures
            Texture2D[] textures = loadedTextures.Values.ToArray();
            for (int i = 0 ; i < textures.Length; i++)
            {
                UnityEngine.Object.Destroy(textures[i]);
            }
            loadedTextures.Clear();
            loaded = false;
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
        public int maxRenderableObjects;
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
        public int alignToTerrainNormal;
        public LOD lod1;
        public LOD lod2;
        public HashSet<string> biomeBlacklist;
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
        public int nearestQuadSubdivisionLevel = 1;
        public float nearestQuadSubdivisionRange = 1.0f;

        /// <summary>
        /// Contains scatters and shared scatters
        /// </summary>
        public Dictionary<string, Scatter> scatters = new Dictionary<string, Scatter>();

        // Shared textures across the planet
        public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Contains all scatters for fast iteration, but not sharedScatters
        /// </summary>
        public Scatter[] fastScatters;
        public ParallaxScatterBody(string planetName)
        {
            this.planetName = planetName;
        }
        public void UnloadTextures()
        {
            ParallaxDebug.Log("Unloading textures for " + planetName);
            foreach (KeyValuePair<string, Texture2D> texturePair in loadedTextures)
            {
                UnityEngine.Object.Destroy(texturePair.Value);
            }
            loadedTextures.Clear();
        }
    }
    
    // Stores Scatter information
    public class Scatter
    {
        public string scatterName;
        public string modelPath;

        public OptimizationParams optimizationParams;
        public SubdivisionParams subdivisionParams;
        public NoiseParams noiseParams;
        public DistributionParams distributionParams;
        public MaterialParams materialParams;
        public BiomeBlacklistParams biomeBlacklistParams;

        public Texture2D biomeControlMap;
        public int biomeCount = 0;

        public bool isShared = false;

        public Scatter(string scatterName)
        {
            this.scatterName = scatterName;
        }
        public void ReinitializeDistribution()
        {
            foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
            {
                quadData.Value.ReinitializeScatters(this);
            }
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
            node.AddValue("collisionLevel", 1);
        }
        protected virtual void PopulateOptimizationNode(ConfigNode node)
        {
            node.AddValue("frustumCullingStartRange", optimizationParams.frustumCullingIgnoreRadius * distributionParams.range);
            node.AddValue("frustumCullingScreenMargin", optimizationParams.frustumCullingSafetyMargin);
            node.AddValue("maxObjects", optimizationParams.maxRenderableObjects);
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

            PopulateScatterNode(scatterNode);
            PopulateOptimizationNode(optimizationNode);
            PopulateMaterialNode(materialNode, materialParams);
            PopulateDistributionNode(distributionNode);

            return scatterNode;
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
    }
}
