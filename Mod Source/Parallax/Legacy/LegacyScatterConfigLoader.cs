using LibNoise;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Legacy
{
    public class LegacyScatterConfigLoader
    {
        public struct Properties
        {
            public Distribution scatterDistribution;
            public ScatterMaterial scatterMaterial;
            public SubdivisionProperties subdivisionSettings;
            public int subObjectCount;
            public float memoryMultiplier; //Manual VRAM management until I figure out how to automate it
            public float maxCount;
        }
        public struct DistributionNoise
        {
            public DistributionNoiseMode noiseMode;
            public float _Frequency;                //Size of noise
            public float _Lacunarity;
            public float _Persistence;
            public float _Octaves;
            public int _Seed;
            public NoiseQuality _NoiseQuality;
            public int _NoiseType;  //0 perlin, 1 rmf, 2 billow

            public float _SizeNoiseScale;
            public float _ColorNoiseScale;
            public float _SizeNoiseOffset;

            public int _MaxStacks;
            public float _StackSeparation;

            public string useNoiseProfile;

            public float _PlacementAltitude;        //For fixedalt scatters this is the altitude stuff spawns at
        }
        public struct Distribution
        {
            public DistributionNoise noise;
            public LODs lods;
            public BiomeBlacklist blacklist;
            public float _Range;                    //How far from the camera to render at the max graphics setting
            public float _SqrRange;                 //Use for fast range calculation in distance checks CPU side
            public float _RangePow;                 //Range fade power
            public float _PopulationMultiplier;     //How many scatters to render
            public float _SizeNoiseStrength;        //Strength of perlin noise - How varied the scatter size is
            public Vector3 _MinScale;               //Smallest scatter size
            public Vector3 _MaxScale;               //Largest scatter size
            public float _CutoffScale;              //Minimum scale at which, below that scale, the scatter is not placed
            public float _SteepPower;
            public float _SteepContrast;
            public float _SteepMidpoint;
            public float _SpawnChance;
            public float _MaxNormalDeviance;
            public float _MinAltitude;
            public float _MaxAltitude;
            public float _Seed;
            public float _AltitudeFadeRange;        //Fade out a scatter over a vertical distance according to size noise. Reduces harshness of a sudden cutoff
            public float _RotationMult;             //Max amount of rotation applied to an object, from 0 to 1
        }
        public struct LODs
        {
            public LOD[] lods;
            public int LODCount;
        }
        public struct BiomeBlacklist
        {
            public string[] biomes;
            public Dictionary<string, string> fastBiomes;   //When wanting to request a biome name but unsure if it is contained, use the much faster dictionary
        }
        public struct LOD
        {
            public float range;
            public string modelName;
            public string mainTexName;
            public string normalName;
            public bool isBillboard;
        }
        public struct ScatterMaterial
        {
            public Dictionary<string, string> Textures;
            public Dictionary<string, float> Floats;
            public Dictionary<string, Vector3> Vectors;
            public Dictionary<string, Vector2> Scales;
            public Dictionary<string, Color> Colors;

            public Shader shader;
            public Color _MainColor;
            public Color _SubColor;
            public float _ColorNoiseStrength;
        }
        public struct SubdivisionProperties
        {
            public SubdivisionMode mode;
            public float range;
            public int level;
            public int minLevel;
        }
        public enum DistributionNoiseMode
        {
            Persistent,
            NonPersistent,
            VerticalStack,
            FixedAltitude
        }
        public enum SubdivisionMode
        {
            NearestQuads,
            FixedRange
        }
        public struct SubObjectProperties
        {
            public ScatterMaterial material;
            public string model;
            public float _NoiseScale;
            public float _NoiseAmount;
            public float _Density;
        }
        public struct SubObjectMaterial
        {
            public Shader shader;
            public string _MainTex;
            public string _BumpMap;
            public float _Shininess;
            public Color _SpecColor;
        }
        public static class ScatterBodies
        {
            public static Dictionary<string, ScatterBody> scatterBodies = new Dictionary<string, ScatterBody>();
        }
        public class ScatterBody
        {
            public Dictionary<string, LegacyScatter> scatters = new Dictionary<string, LegacyScatter>();
            public string bodyName = "invalidname";
            public int minimumSubdivision = 6;
            public ScatterBody(string name, string minSub)
            {
                bodyName = name;
                bool converted = int.TryParse(minSub, out minimumSubdivision);
                if (!converted) { ScatterLog.SubLog("[Exception] Unable to get the value of minimumSubdivision"); minimumSubdivision = 6; }
            }
        }
        public class LegacyScatter
        {
            public string scatterName = "invalidname";
            public string planetName = "invalidname";
            public string model;
            public float updateFPS = 1;
            public bool alignToTerrainNormal = false;
            public int subObjectCount = 0;
            public Properties properties;
            public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            public bool isVisible = true;
            public bool useSurfacePos = false;
            public float cullingRange = 0;
            public float cullingLimit = -15;
            public bool shared = false;
            public string sharedParent;
            public int maxObjects = 10000;
            public bool collideable = false;
            public bool alwaysCollideable = false;
            public string collisionMesh;

            public LegacyScatter(string name)
            {
                scatterName = name;
            }
            public ConfigNode ToUpdatedConfigNode()
            {
                ConfigNode scatterNode = !shared ? new ConfigNode("Scatter") : new ConfigNode("SharedScatter");
                ConfigNode optimizationNode = scatterNode.AddNode("Optimizations");
                ConfigNode subdivisionSettingsNode = scatterNode.AddNode("SubdivisionSettings");
                ConfigNode distributionNoiseNode = scatterNode.AddNode("DistributionNoise");
                ConfigNode materialNode = scatterNode.AddNode("Material");
                ConfigNode distributionNode = scatterNode.AddNode("Distribution");

                if (shared)
                {
                    PopulateScatterNode(scatterNode);
                    PopulateOptimizationNode(optimizationNode);
                    PopulateMaterialNode(materialNode, default(LOD));
                    PopulateDistributionNode(distributionNode);
                }
                else
                {
                    PopulateScatterNode(scatterNode);
                    PopulateOptimizationNode(optimizationNode);
                    PopulateSubdivisionSettingsNode(subdivisionSettingsNode);
                    PopulateDistributionNoiseNode(distributionNoiseNode);
                    PopulateMaterialNode(materialNode, default(LOD));
                    PopulateDistributionNode(distributionNode);
                }

                return scatterNode;
            }
            void PopulateScatterNode(ConfigNode node)
            {
                node.AddValue("name", scatterName);
                if (model.Contains("Parallax_StockTextures/_Scatters"))
                {
                    model = model.Replace("Parallax_StockTextures/_Scatters", "Parallax_StockScatterTextures");
                }
                node.AddValue("model", model);
                node.AddValue("collisionLevel", CalculateCollisionLevel(scatterName));
                if (shared)
                {
                    node.AddValue("parentName", sharedParent);
                }
            }
            int CalculateCollisionLevel(string scatterName)
            {
                scatterName = scatterName.ToLower();
                if (scatterName.Contains("huge"))
                {
                    return 4;
                }
                else if (scatterName.Contains("large"))
                {
                    return 3;
                }
                else if (scatterName.Contains("med"))
                {
                    return 2;
                }
                else
                {
                    return collideable ? 2 : 1;
                }
            }
            void PopulateOptimizationNode(ConfigNode node)
            {
                node.AddValue("frustumCullingStartRange", cullingRange);
                node.AddValue("frustumCullingScreenMargin", cullingLimit);
                node.AddValue("maxObjects", maxObjects);
            }
            void PopulateSubdivisionSettingsNode(ConfigNode node)
            {
                node.AddValue("subdivisionRangeMode", "noSubdivision");
            }
            void PopulateDistributionNoiseNode(ConfigNode node)
            {
                // Non persistent has changed - quads aren't subdivided anymore
                if (properties.scatterDistribution.noise.noiseMode == DistributionNoiseMode.NonPersistent)
                {
                    node.AddValue("noiseType", "simplexPerlin");
                    node.AddValue("inverted", false);
                    node.AddValue("frequency", 2000.0f);
                    node.AddValue("octaves", 4.0f);
                    node.AddValue("lacunarity", 2.0f);
                    node.AddValue("seed", 0);
                }
                else
                {
                    if (properties.scatterDistribution.noise.useNoiseProfile != null && ScatterBodies.scatterBodies[planetName].scatters.ContainsKey(properties.scatterDistribution.noise.useNoiseProfile))
                    {
                        LegacyScatter noiseProfileScatter = ScatterBodies.scatterBodies[planetName].scatters[properties.scatterDistribution.noise.useNoiseProfile];
                        node.AddValue("noiseType", "simplexPerlin");
                        node.AddValue("inverted", false);
                        node.AddValue("frequency", noiseProfileScatter.properties.scatterDistribution.noise._Frequency);
                        node.AddValue("octaves", noiseProfileScatter.properties.scatterDistribution.noise._Octaves);
                        node.AddValue("lacunarity", noiseProfileScatter.properties.scatterDistribution.noise._Lacunarity);
                        node.AddValue("seed", noiseProfileScatter.properties.scatterDistribution.noise._Seed);
                    }
                    else
                    {
                        node.AddValue("noiseType", "simplexPerlin");
                        node.AddValue("inverted", false);
                        node.AddValue("frequency", properties.scatterDistribution.noise._Frequency);
                        node.AddValue("octaves", properties.scatterDistribution.noise._Octaves);
                        node.AddValue("lacunarity", properties.scatterDistribution.noise._Lacunarity);
                        node.AddValue("seed", properties.scatterDistribution.noise._Seed);
                    }
                }
            }
            void PopulateMaterialNode(ConfigNode node, LOD lod)
            {
                node.AddValue("shader", "Custom/ParallaxInstancedSolid");
                ConfigNode keywordsNode = node.AddNode("Keywords");

                List<string> keywords = GetAllKeywords();
                foreach (string keyword in keywords)
                {
                    keywordsNode.AddValue("name", keyword);
                }

                foreach (string texture in properties.scatterMaterial.Textures.Keys)
                {
                    string modifiedValue = texture;
                    if (texture == "_EdgeBumpMap")
                    {
                        modifiedValue = "_BumpMap";
                    }
                    node.AddValue(modifiedValue, ModifyTexturePath(properties.scatterMaterial.Textures[texture]));
                }
                foreach (string floatValue in properties.scatterMaterial.Floats.Keys)
                {
                    string modifiedValue = floatValue;
                    if (floatValue == "_Metallic")
                    {
                        modifiedValue = "_SpecularIntensity";
                    }
                    if (floatValue == "_Gloss")
                    {
                        modifiedValue = "_SpecularPower";
                    }
                    if (floatValue == "_HeightCutoff" || floatValue == "_HeightFactor" || floatValue == "_WaveAmp" || floatValue == "_WaveSpeed")
                    {
                        continue;
                    }
                    node.AddValue(modifiedValue, properties.scatterMaterial.Floats[floatValue]);
                }
                // Add missing bump scale param
                if (!properties.scatterMaterial.Floats.Keys.Contains("_BumpScale"))
                {
                    node.AddValue("_BumpScale", 1.0f);
                }
                node.AddValue("_EnvironmentMapFactor", 1);
                if (keywords.Contains("WIND"))
                {
                    node.AddValue("_WindScale", 0.080f);
                    node.AddValue("_WindHeightStart", 0.05f);
                    node.AddValue("_WindHeightFactor", 2);
                    node.AddValue("_WindSpeed", 0.0f);
                    node.AddValue("_WindIntensity", 0.0f);
                }
                foreach (string colorValue in properties.scatterMaterial.Colors.Keys)
                {
                    string modifiedValue = colorValue;
                    if (colorValue == "_MainColor" || colorValue == "_SubColor")
                    {
                        continue;
                    }
                    if (colorValue == "_EmissiveColor" || colorValue == "_EmissionColor")
                    {
                        continue;
                    }
                    if (colorValue == "_MetallicTint")
                    {
                        Color color = properties.scatterMaterial.Colors[colorValue];
                        float magnitude = color.grayscale;
                        // Convert specular colour intensity to spec intensity, as spec colour is just the light colour now
                        node.SetValue("_SpecularIntensity", float.Parse(node.GetValue("_SpecularIntensity")) * magnitude);
                        continue;
                    }
                    node.AddValue(modifiedValue, properties.scatterMaterial.Colors[colorValue]);
                }

                foreach (string vectorValue in properties.scatterMaterial.Vectors.Keys)
                {
                    string modifiedValue = vectorValue;
                    if (vectorValue == "_WindSpeed")
                    {
                        continue;
                    }
                    node.AddValue(modifiedValue, properties.scatterMaterial.Vectors[vectorValue]);
                }
                // Two sided geometry must have cull mode set to 0 - no culling
                if (keywords.Contains("TWO_SIDED"))
                {
                    node.AddValue("_CullMode", 0);
                }
                else
                {
                    node.AddValue("_CullMode", 2);
                }
            }
            string ModifyTexturePath(string path)
            {
                if (path.Contains("Parallax_StockTextures/_Scatters"))
                {
                    return path.Replace("Parallax_StockTextures/_Scatters", "Parallax_StockScatterTextures");
                }
                else
                {
                    return path;
                }
            }
            List<string> GetAllKeywords()
            {
                List<string> keywords = new List<string>();
                if (properties.scatterMaterial.shader.name.ToLower().Contains("billboard"))
                {
                    keywords.Add("BILLBOARD");
                }
                if (properties.scatterMaterial.Floats.ContainsKey("_Cutoff") && properties.scatterMaterial.Floats["_Cutoff"] > 0)
                {
                    keywords.Add("ALPHA_CUTOFF");
                    keywords.Add("TWO_SIDED");
                }
                if (properties.scatterMaterial.Textures.ContainsKey("_WindMap"))
                {
                    keywords.Add("WIND");
                }
                return keywords;
            }
            void PopulateDistributionNode(ConfigNode node)
            {
                if (!shared)
                {
                    node.AddValue("seed", properties.scatterDistribution._Seed);
                    node.AddValue("spawnChance", properties.scatterDistribution._SpawnChance);
                    node.AddValue("range", properties.scatterDistribution._Range);
                    node.AddValue("populationMultiplier", (int)properties.scatterDistribution._PopulationMultiplier);
                    float scaleMult = properties.subdivisionSettings.mode == SubdivisionMode.NearestQuads ? 2.0f : 1.0f;
                    node.AddValue("minScale", properties.scatterDistribution._MinScale * scaleMult);
                    node.AddValue("maxScale", properties.scatterDistribution._MaxScale * scaleMult);
                    node.AddValue("scaleRandomness", 0.5f);
                    node.AddValue("cutoffScale", properties.scatterDistribution._CutoffScale);
                    node.AddValue("steepPower", properties.scatterDistribution._SteepPower);
                    node.AddValue("steepContrast", properties.scatterDistribution._SteepContrast);
                    node.AddValue("steepMidpoint", properties.scatterDistribution._SteepMidpoint);
                    node.AddValue("maxNormalDeviance", properties.scatterDistribution._MaxNormalDeviance);
                    node.AddValue("minAltitude", properties.scatterDistribution._MinAltitude);
                    node.AddValue("maxAltitude", properties.scatterDistribution._MaxAltitude);
                    node.AddValue("altitudeFadeRange", properties.scatterDistribution._AltitudeFadeRange * 2.0f);
                    node.AddValue("alignToTerrainNormal", alignToTerrainNormal);
                    node.AddValue("coloredByTerrain", false);
                }

                ConfigNode lodNode = node.AddNode("LODs");

                ConfigNode lod1 = lodNode.AddNode("LOD");
                ConfigNode lod2 = lodNode.AddNode("LOD");
                ProcessLOD(lod1, properties.scatterDistribution.lods.lods[0]);
                ProcessLOD(lod2, properties.scatterDistribution.lods.lods[1]);

                if (properties.scatterDistribution.blacklist.biomes != null)
                {
                    ConfigNode biomeBlacklistNode = node.AddNode("BiomeBlacklist");
                    foreach (string biome in properties.scatterDistribution.blacklist.biomes)
                    {
                        biomeBlacklistNode.AddValue("name", biome);
                    }
                }

                
            }
            void ProcessLOD(ConfigNode lodNode, LOD lod)
            {
                if (lod.modelName.Contains("Parallax_StockTextures/_Scatters"))
                {
                    lod.modelName = lod.modelName.Replace("Parallax_StockTextures/_Scatters", "Parallax_StockScatterTextures");
                }
                lodNode.AddValue("model", lod.modelName);
                lodNode.AddValue("range", lod.range);

                // Process material
                ConfigNode materialNode;
                if (lod.isBillboard)
                {
                    materialNode = lodNode.AddNode("Material");

                    PopulateMaterialNode(materialNode, lod);
                    ConfigNode materialKeywords = materialNode.GetNode("Keywords");
                    if (materialKeywords == null)
                    {
                        materialKeywords = materialNode.AddNode("Keywords");
                    }
                    materialKeywords.AddValue("name", "BILLBOARD");
                    materialKeywords.AddValue("name", "BILLBOARD_USE_MESH_NORMALS");

                    // Process very silly implementation of material override...
                    if (lod.mainTexName != null && lod.mainTexName != "parent")
                    {
                        materialNode.SetValue("_MainTex", lod.mainTexName, true);
                    }
                    if (lod.normalName != null && lod.normalName != "parent")
                    {
                        materialNode.SetValue("_BumpMap", lod.normalName, true);
                    }
                }
                else
                {
                    materialNode = lodNode.AddNode("MaterialOverride");
                    if (lod.mainTexName != "parent" && lod.mainTexName != null)
                    {
                        materialNode.AddValue("_MainTex", lod.mainTexName);
                    }
                    if (lod.normalName != "parent" && lod.normalName != null)
                    {
                        materialNode.AddValue("_BumpMap", lod.normalName);
                    }
                }
            }
        }
        public static class ScatterLog
        {
            public static void Log(string message)
            {
                Debug.Log("[ParallaxScatter] " + message);
            }
            public static void SubLog(string message)
            {
                Debug.Log("[ParallaxScatter] \t\t - " + message);
            }
        }
        [KSPAddon(KSPAddon.Startup.Instantly, true)]
        public class LegacyScatterLoader : MonoBehaviour
        {
            public static UrlDir.UrlConfig[] globalNodes;
            public static LegacyScatterLoader Instance;
            static float subdivisionRangeRestraint = 0;    //Collideables MUST share the same range
            void Awake()
            {
                GameObject.DontDestroyOnLoad(this);
                Instance = this;
            }
            public static void ModuleManagerPostLoad()
            {
                globalNodes = GameDatabase.Instance.GetConfigs("ParallaxScatters");
                LoadScatterNodes();
            }
            public static void LoadScatterNodes()
            {
                for (int i = 0; i < globalNodes.Length; i++)
                {
                    ConfigNode bodyNode = globalNodes[i].config.GetNode("Body");

                    // For future config upgrades, check against the config version itself
                    if (bodyNode != null && bodyNode.GetValue("configVersion") != null)
                    {
                        Debug.Log("SKIPPED");
                        continue;
                    }

                    // If bodyName is null, we've hit the root config node in Parallax Continued
                    string bodyName = globalNodes[i].config.GetValue("body");
                    if (bodyName == null)
                    {
                        continue;
                    }

                    Debug.Log("[Parallax] [LegacyLoader] Parsing Bodyname: " + bodyName);
                    string minSubdiv = globalNodes[i].config.GetValue("minimumSubdivision");
                    
                    
                    ScatterBody body = new ScatterBody(bodyName, minSubdiv);
                    ScatterBodies.scatterBodies.Add(bodyName, body);
                    ScatterLog.Log("Parsing body: " + bodyName);
                    subdivisionRangeRestraint = 0;
                    for (int b = 0; b < globalNodes[i].config.nodes.Count; b++)
                    {
                        ConfigNode rootNode = globalNodes[i].config;
                        ConfigNode scatterNode = rootNode.nodes[b];
                        string parentName = scatterNode.GetValue("parent");
                        if (parentName != null)
                        {
                            ConfigNode materialNode = scatterNode.GetNode("Material");
                            ConfigNode distributionNode = scatterNode.GetNode("Distribution");
                            ParseSharedScatter(parentName, scatterNode, distributionNode, materialNode, bodyName);    //Shares its distribution data with another scatter
                        }
                        else
                        {
                            ConfigNode distributionNode = scatterNode.GetNode("Distribution");
                            ConfigNode materialNode = scatterNode.GetNode("Material");
                            ConfigNode subdivisionSettingsNode = scatterNode.GetNode("SubdivisionSettings");
                            ConfigNode subObjectNode = scatterNode.GetNode("SubObjects");
                            ConfigNode distributionNoiseNode = scatterNode.GetNode("DistributionNoise");

                            ParseNewScatter(scatterNode, distributionNoiseNode, distributionNode, materialNode, subdivisionSettingsNode, subObjectNode, bodyName);
                        }
                    }

                }

                // Now convert them all
                foreach (KeyValuePair<string, ScatterBody> body in ScatterBodies.scatterBodies)
                {
                    ConfigNode baseNode = new ConfigNode("ParallaxScatters-UPGRADED");
                    ConfigNode result = baseNode.AddNode("ParallaxScatters-UPGRADED");
                    ConfigNode bodyNode = result.AddNode("Body");

                    bodyNode.AddValue("name", body.Key, "To activate this config, replace ParallaxScatters-UPGRADED with ParallaxScatters");
                    bodyNode.AddValue("configVersion", 2.0f);

                    foreach (LegacyScatter scatter in body.Value.scatters.Values)
                    {
                        bodyNode.AddNode(scatter.ToUpdatedConfigNode());
                    }
                    baseNode.Save(KSPUtil.ApplicationRootPath + "GameData/ParallaxContinued/Exports/LegacyUpgrades/" + body.Key + ".cfg");
                }
            }
            public static void LoadNewScatter(string type)
            {
                if (!ScatterBodies.scatterBodies.ContainsKey(FlightGlobals.currentMainBody.name))
                {
                    ScatterBodies.scatterBodies.Add(FlightGlobals.currentMainBody.name, new ScatterBody(FlightGlobals.currentMainBody.name, FlightGlobals.currentMainBody.pqsController.maxLevel.ToString()));
                }

                UrlDir.UrlConfig[] uiNode = GameDatabase.Instance.GetConfigs("ParallaxUIDefault");
                for (int b = 0; b < uiNode[0].config.nodes.Count; b++)
                {
                    ConfigNode rootNode = uiNode[0].config;
                    ConfigNode scatterNode = rootNode.nodes[b];
                    string name = scatterNode.GetValue("name");
                    Debug.Log("Parsing: " + name);
                    if (name == type)
                    {
                        ConfigNode distributionNode = scatterNode.GetNode("Distribution");
                        ConfigNode materialNode = scatterNode.GetNode("Material");
                        ConfigNode subdivisionSettingsNode = scatterNode.GetNode("SubdivisionSettings");
                        ConfigNode subObjectNode = scatterNode.GetNode("SubObjects");
                        ConfigNode distributionNoiseNode = scatterNode.GetNode("DistributionNoise");

                        ParseNewScatter(scatterNode, distributionNoiseNode, distributionNode, materialNode, subdivisionSettingsNode, subObjectNode, FlightGlobals.currentMainBody.name);
                    }
                }
            }
            public static void ParseNewScatter(ConfigNode scatterNode, ConfigNode distributionNoiseNode, ConfigNode distributionNode, ConfigNode materialNode, ConfigNode subdivisionSettingsNode, ConfigNode subObjectNode, string bodyName)
            {

                ScatterBody body = ScatterBodies.scatterBodies[bodyName];   //Bodies contain multiple scatters
                string scatterName = scatterNode.GetValue("name");

                string repeatedName = scatterName;
                int repeatedCount = 1;
                while (body.scatters.ContainsKey(repeatedName))  //Just for the UI adding a new scatter to avoid adding duplicate
                {
                    repeatedName = scatterName + repeatedCount.ToString();
                    repeatedCount++;
                }
                scatterName = repeatedName;
                ScatterLog.Log("Parsing scatter: " + scatterName);
                LegacyScatter scatter = new LegacyScatter(scatterName);
                scatter.planetName = bodyName;
                Properties props = new Properties();
                scatter.model = scatterNode.GetValue("model");
                string forcedFull = "";
                bool forcedFullShadows = scatterNode.TryGetValue("shadowMode", ref forcedFull);
                if (forcedFullShadows && forcedFull == "forcedFull") { scatter.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                string alignToNormal = "";
                bool requiresNormal = scatterNode.TryGetValue("alignToTerrainNormal", ref alignToNormal);
                if (requiresNormal) { scatter.alignToTerrainNormal = bool.Parse(alignToNormal); } else { scatter.alignToTerrainNormal = false; }
                bool useSurfacePos = false;
                useSurfacePos = scatterNode.TryGetValue("useSurfacePosition", ref useSurfacePos);
                if (useSurfacePos) { scatter.useSurfacePos = true; } else { scatter.useSurfacePos = false; }
                string cullRange = "";
                bool cullRangeCheck = scatterNode.TryGetValue("cullingRange", ref cullRange);
                if (cullRangeCheck) { scatter.cullingRange = float.Parse(cullRange); }
                string cullLimit = "";
                bool cullLimitCheck = scatterNode.TryGetValue("cullingLimit", ref cullLimit);
                if (cullLimitCheck) { scatter.cullingLimit = float.Parse(cullLimit); }
                string maxObjects = "";
                bool maxObjectsCheck = scatterNode.TryGetValue("maxObjects", ref maxObjects);
                if (maxObjectsCheck) { scatter.maxObjects = int.Parse(maxObjects); }
                string hasCollider = "";
                bool colliderCheck = scatterNode.TryGetValue("collideable", ref hasCollider);
                if (colliderCheck) { scatter.collideable = bool.Parse(hasCollider); }
                
                string hasCollisionMesh = "";
                bool collisionMeshCheck = scatterNode.TryGetValue("collisionMesh", ref hasCollisionMesh);
                if (collisionMeshCheck) { scatter.collisionMesh = hasCollisionMesh; }

                props.scatterDistribution = ParseDistribution(distributionNode);
                props.scatterDistribution.noise = ParseDistributionNoise(distributionNoiseNode, bodyName);
                props.scatterMaterial = ParseMaterial(materialNode, false);
                props.subdivisionSettings = ParseSubdivisionSettings(subdivisionSettingsNode, body);
                props.memoryMultiplier = 100;
                scatter.properties = props;
                scatter.updateFPS = ParseFloat(ParseVar(scatterNode, "updateFPS", "30"));
                body.scatters.Add(scatterName, scatter);

                if (subdivisionRangeRestraint == 0 && scatter.collideable) { subdivisionRangeRestraint = props.subdivisionSettings.range; }


            }
            public static void ParseSharedScatter(string parentName, ConfigNode scatterNode, ConfigNode distributionNode, ConfigNode materialNode, string bodyName)
            {
                ScatterBody body = ScatterBodies.scatterBodies[bodyName];   //Bodies contain multiple scatters
                string scatterName = scatterNode.GetValue("name");
                ScatterLog.Log("Parsing shared scatter: " + scatterName);
                LegacyScatter scatter = new LegacyScatter(scatterName);
                scatter.shared = true;
                scatter.sharedParent = parentName;
                scatter.planetName = bodyName;
                Properties props = new Properties();
                scatter.model = scatterNode.GetValue("model");

                string forcedFull = "";
                bool forcedFullShadows = scatterNode.TryGetValue("shadowMode", ref forcedFull);
                if (forcedFullShadows && forcedFull == "forcedFull") { scatter.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }

                props.scatterMaterial = ParseMaterial(materialNode, false);
                props.memoryMultiplier = 100;
                Distribution distribution = new Distribution();
                ConfigNode lodNode = distributionNode.GetNode("LODs");
                distribution.lods = ParseLODs(lodNode);
                distribution._Range = body.scatters[parentName].properties.scatterDistribution._Range;
                distribution._SqrRange = distribution._Range * distribution._Range;
                props.scatterDistribution = distribution;
                scatter.properties = props;
                body.scatters.Add(scatterName, scatter);
            }
            public static DistributionNoise ParseDistributionNoise(ConfigNode distributionNode, string bodyName)
            {
                DistributionNoise distribution = new DistributionNoise();

                string noiseMode = ParseVar(distributionNode, "mode", "Persistent");
                if (noiseMode.ToLower() == "nonpersistent") { distribution.noiseMode = DistributionNoiseMode.NonPersistent; }
                else if (noiseMode.ToLower() == "verticalstack") { distribution.noiseMode = DistributionNoiseMode.VerticalStack; }
                else if (noiseMode.ToLower() == "fixedaltitude") { distribution.noiseMode = DistributionNoiseMode.FixedAltitude; }
                else { distribution.noiseMode = DistributionNoiseMode.Persistent; }

                distribution.useNoiseProfile = (ParseVar(distributionNode, "useNoiseProfile", null));
                if (distribution.useNoiseProfile != null) { ScatterLog.SubLog("Using noise profile: " + distribution.useNoiseProfile); }
                if (distribution.useNoiseProfile != null && (distribution.noiseMode == DistributionNoiseMode.NonPersistent)) { ScatterLog.SubLog("[Exception] Attempting to use a noise profile for a non-persistent scatter. This only works if you want to share the same noise as another persistent scatter!"); }
                if (distribution.useNoiseProfile != null)
                {
                    distribution._Frequency = 1; distribution._Lacunarity = 1; distribution._Persistence = 1; distribution._Octaves = 1; distribution._Seed = 1; distribution._NoiseType = 1; distribution._NoiseQuality = LibNoise.NoiseQuality.Low; distribution._MaxStacks = 1; distribution._StackSeparation = 1;
                    return distribution;
                }


                distribution._MaxStacks = 1;
                distribution._StackSeparation = 1;

                if (distribution.noiseMode == DistributionNoiseMode.Persistent || distribution.noiseMode == DistributionNoiseMode.VerticalStack || distribution.noiseMode == DistributionNoiseMode.FixedAltitude)
                {
                    distribution._Frequency = ParseFloat(ParseVar(distributionNode, "_Frequency", "100"));
                    distribution._Lacunarity = ParseFloat(ParseVar(distributionNode, "_Lacunarity", "4"));
                    distribution._Persistence = ParseFloat(ParseVar(distributionNode, "_Persistence", "0.5"));
                    distribution._Octaves = ParseFloat(ParseVar(distributionNode, "_Octaves", "4"));
                    distribution._Seed = (int)ParseFloat(ParseVar(distributionNode, "_Seed", "69420")); //Very mature


                    string noiseType = ParseVar(distributionNode, "_NoiseType", "1");
                    switch (noiseType)
                    {
                        default:
                            distribution._NoiseType = 0;
                            break;
                        case "RidgedMultifractal":
                            distribution._NoiseType = 1;
                            break;
                        case "Billow":
                            distribution._NoiseType = 2;
                            break;
                        case "1":
                            distribution._NoiseType = 1;
                            break;
                        case "2":
                            distribution._NoiseType = 2;
                            break;
                    }
                    string noiseQuality = ParseVar(distributionNode, "_NoiseQuality", "Low");
                    switch (noiseQuality)
                    {
                        default:
                            distribution._NoiseQuality = NoiseQuality.Standard;
                            break;
                        case "Low":
                            distribution._NoiseQuality = NoiseQuality.Low;
                            break;
                        case "High":
                            distribution._NoiseQuality = NoiseQuality.High;
                            break;
                    }
                    distribution._SizeNoiseScale = 0;
                    distribution._ColorNoiseScale = 0;
                    distribution._SizeNoiseOffset = 0;
                    if (distribution.noiseMode == DistributionNoiseMode.VerticalStack)
                    {
                        distribution._MaxStacks = (int)ParseFloat(ParseVar(distributionNode, "_MaxStacks", "1"));
                        distribution._StackSeparation = (int)ParseFloat(ParseVar(distributionNode, "_StackSeparation", "10"));
                    }
                    if (distribution.noiseMode == DistributionNoiseMode.FixedAltitude)
                    {
                        distribution._PlacementAltitude = (int)ParseFloat(ParseVar(distributionNode, "_PlacementAltitude", "0"));
                    }
                }
                else
                {
                    distribution._SizeNoiseScale = ParseFloat(ParseVar(distributionNode, "_SizeNoiseScale", "4"));
                    distribution._ColorNoiseScale = ParseFloat(ParseVar(distributionNode, "_ColorNoiseScale", "4"));
                    distribution._SizeNoiseOffset = ParseFloat(ParseVar(distributionNode, "_SizeNoiseOffset", "0"));
                }

                return distribution;
            }
            public static Distribution ParseDistribution(ConfigNode distributionNode)
            {
                Distribution distribution = new Distribution();

                distribution._Range = ParseFloat(ParseVar(distributionNode, "_Range", "1000")) * 1.0f;
                distribution._SqrRange = distribution._Range * distribution._Range;
                distribution._RangePow = ParseFloat(ParseVar(distributionNode, "_RangePow", "1000"));
                distribution._PopulationMultiplier = ParseFloat(ParseVar(distributionNode, "_PopulationMultiplier", "1")) * 1.0f;
                distribution._SizeNoiseStrength = ParseFloat(ParseVar(distributionNode, "_SizeNoiseStrength", "1"));
                distribution._CutoffScale = ParseFloat(ParseVar(distributionNode, "_CutoffScale", "0"));
                distribution._SteepPower = ParseFloat(ParseVar(distributionNode, "_SteepPower", "1"));
                distribution._SteepContrast = ParseFloat(ParseVar(distributionNode, "_SteepContrast", "1"));
                distribution._SteepMidpoint = ParseFloat(ParseVar(distributionNode, "_SteepMidpoint", "0.5"));
                distribution._MaxNormalDeviance = ParseFloat(ParseVar(distributionNode, "_NormalDeviance", "1"));
                distribution._MinScale = ParseVector(ParseVar(distributionNode, "_MinScale", "1,1,1"));
                distribution._MaxScale = ParseVector(ParseVar(distributionNode, "_MaxScale", "1,1,1"));
                distribution._MinAltitude = ParseFloat(ParseVar(distributionNode, "_MinAltitude", "0"));
                distribution._MaxAltitude = ParseFloat(ParseVar(distributionNode, "_MaxAltitude", "1000000"));
                distribution._SpawnChance = ParseFloat(ParseVar(distributionNode, "_SpawnChance", "1"));
                distribution._Seed = ParseFloat(ParseVar(distributionNode, "_Seed", "69"));
                distribution._AltitudeFadeRange = ParseFloat(ParseVar(distributionNode, "_AltitudeFadeRange", "5"));

                string rotCheck = "1.0";
                distributionNode.TryGetValue("_RotationMultiplier", ref rotCheck);
                distribution._RotationMult = float.Parse(rotCheck);

                if ((int)(distribution._PopulationMultiplier) == 0) { distribution._PopulationMultiplier = 1; }
                ConfigNode lodNode = distributionNode.GetNode("LODs");
                ConfigNode biomeBlacklist = null;  //optional
                bool hasBlacklist = distributionNode.TryGetNode("BiomeBlacklist", ref biomeBlacklist);
                distribution.blacklist = ParseBlacklist(biomeBlacklist, hasBlacklist);
                distribution.lods = ParseLODs(lodNode);


                return distribution;
            }
            public static LODs ParseLODs(ConfigNode lodNode)
            {

                LODs lods = new LODs();
                ConfigNode[] lodNodes = lodNode.GetNodes("LOD");
                lods.LODCount = lodNodes.Length;
                lods.lods = new LOD[lods.LODCount];
                for (int i = 0; i < lods.LODCount; i++)
                {
                    LOD lod = new LOD();

                    lod.modelName = ParseVar(lodNodes[i], "model", "[Exception] No model defined in the scatter config");
                    lod.mainTexName = ParseVar(lodNodes[i], "_MainTex", "parent");
                    string normalName = "parent";
                    bool hasNormal = lodNodes[i].TryGetValue("_BumpMap", ref normalName);
                    if (!hasNormal) { normalName = "parent"; }
                    lod.normalName = normalName;
                    if (lodNodes[i].HasValue("billboard") && lodNodes[i].GetValue("billboard").ToLower() == "true") { lod.isBillboard = true; Debug.Log("has billboard"); }
                    else { lod.isBillboard = false; }

                    if (lodNodes[i].HasValue("range")) { lod.range = ParseFloat(ParseVar(lodNodes[i], "range", "5")); }

                    lods.lods[i] = lod;
                    //Parse models on main menu after they have loaded
                }
                return lods;
            }
            public static BiomeBlacklist ParseBlacklist(ConfigNode node, bool hasBlacklist)
            {
                if (!hasBlacklist)
                {
                    BiomeBlacklist emptyList = new BiomeBlacklist();
                    emptyList.fastBiomes = new Dictionary<string, string>();
                    emptyList.biomes = new string[0];
                    return emptyList;
                }
                BiomeBlacklist blacklist = new BiomeBlacklist();
                string[] values = node.GetValues("name");
                blacklist.biomes = values;
                blacklist.fastBiomes = new Dictionary<string, string>();
                for (int i = 0; i < values.Length; i++)
                {
                    blacklist.fastBiomes.Add(values[i], values[i]);
                }
                return blacklist;
            }
            public static ScatterMaterial GetShaderVars(string shaderName, ScatterMaterial material, ConfigNode materialNode)
            {
                UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("ScatterShader");
                for (int i = 0; i < nodes.Length; i++)
                {
                    string configShaderName = nodes[i].config.GetValue("name");
                    if (configShaderName == shaderName)
                    {
                        ConfigNode propertiesNode = nodes[i].config.GetNode("Properties");
                        ConfigNode texturesNode = propertiesNode.GetNode("Textures");
                        ConfigNode floatsNode = propertiesNode.GetNode("Floats");
                        ConfigNode vectorsNode = propertiesNode.GetNode("Vectors");
                        ConfigNode scalesNode = propertiesNode.GetNode("Scales");
                        ConfigNode colorsNode = propertiesNode.GetNode("Colors");
                        material = ParseNodeType(texturesNode, typeof(string), material);
                        material = ParseNodeType(floatsNode, typeof(float), material);
                        material = ParseNodeType(vectorsNode, typeof(Vector3), material);
                        material = ParseNodeType(scalesNode, typeof(Vector2), material);
                        material = ParseNodeType(colorsNode, typeof(Color), material);
                        material = SetShaderValues(materialNode, material);

                    }
                }
                return material;
            }
            public static ScatterMaterial ParseNodeType(ConfigNode node, Type type, ScatterMaterial material)
            {
                string[] values = node.GetValues("name");
                if (type == typeof(string))
                {
                    material.Textures = new Dictionary<string, string>();
                    for (int i = 0; i < values.Length; i++)
                    {
                        material.Textures.Add(values[i], null);
                    }
                }
                else if (type == typeof(float))
                {
                    material.Floats = new Dictionary<string, float>();
                    for (int i = 0; i < values.Length; i++)
                    {
                        material.Floats.Add(values[i], 0);
                    }
                }
                else if (type == typeof(Vector3))
                {
                    material.Vectors = new Dictionary<string, Vector3>();
                    for (int i = 0; i < values.Length; i++)
                    {
                        material.Vectors.Add(values[i], Vector3.zero);
                    }
                }
                else if (type == typeof(Vector2))
                {
                    material.Scales = new Dictionary<string, Vector2>();
                    for (int i = 0; i < values.Length; i++)
                    {
                        material.Scales.Add(values[i], Vector2.zero);
                    }
                }
                else if (type == typeof(Color))
                {
                    material.Colors = new Dictionary<string, Color>();
                    for (int i = 0; i < values.Length; i++)
                    {
                        material.Colors.Add(values[i], Color.magenta);
                    }
                }
                else
                {
                    ScatterLog.SubLog("Unable to determine type");
                }
                return material;
            }
            public static ScatterMaterial SetShaderValues(ConfigNode materialNode, ScatterMaterial material)
            {
                string[] textureKeys = material.Textures.Keys.ToArray();
                ScatterLog.Log("Setting shader values: ");
                for (int i = 0; i < material.Textures.Keys.Count; i++)
                {
                    ScatterLog.SubLog("Parsing " + textureKeys[i] + " as " + materialNode.GetValue(textureKeys[i]));
                    material.Textures[textureKeys[i]] = materialNode.GetValue(textureKeys[i]);
                }
                string[] floatKeys = material.Floats.Keys.ToArray();
                for (int i = 0; i < material.Floats.Keys.Count; i++)
                {
                    ScatterLog.SubLog("Parsing " + floatKeys[i] + " as " + materialNode.GetValue(floatKeys[i]));
                    material.Floats[floatKeys[i]] = float.Parse(materialNode.GetValue(floatKeys[i]));
                }
                string[] vectorKeys = material.Vectors.Keys.ToArray();
                for (int i = 0; i < material.Vectors.Keys.Count; i++)
                {
                    string configValue = materialNode.GetValue(vectorKeys[i]);
                    ScatterLog.SubLog("Parsing " + vectorKeys[i] + " as " + materialNode.GetValue(vectorKeys[i]));
                    material.Vectors[vectorKeys[i]] = ParseVector(configValue);
                }
                string[] scaleKeys = material.Scales.Keys.ToArray();
                for (int i = 0; i < material.Scales.Keys.Count; i++)
                {
                    string configValue = materialNode.GetValue(scaleKeys[i]);
                    ScatterLog.SubLog("Parsing " + scaleKeys[i] + " as " + materialNode.GetValue(scaleKeys[i]));
                    material.Scales[scaleKeys[i]] = ParseVector2D(configValue);
                }
                string[] colorKeys = material.Colors.Keys.ToArray();
                for (int i = 0; i < material.Colors.Keys.Count; i++)
                {
                    string configValue = materialNode.GetValue(colorKeys[i]);
                    ScatterLog.SubLog("Parsing " + colorKeys[i] + " as " + materialNode.GetValue(colorKeys[i]));
                    material.Colors[colorKeys[i]] = ParseColor(configValue);
                }
                return material;
            }
            public static ScatterMaterial ParseMaterial(ConfigNode materialNode, bool isSubObject)
            {
                ScatterMaterial material = new ScatterMaterial();

                material.shader = Shader.Find("Standard");

                material = GetShaderVars(materialNode.GetValue("shader"), material, materialNode);
                if (!isSubObject)
                {
                    material._MainColor = ParseColor(ParseVar(materialNode, "_MainColor", "1,1,1,1"));
                    material._SubColor = ParseColor(ParseVar(materialNode, "_SubColor", "1,1,1,1"));


                    material._ColorNoiseStrength = ParseFloat(ParseVar(materialNode, "_ColorNoiseStrength", "1"));
                }

                return material;
            }
            public static SubdivisionProperties ParseSubdivisionSettings(ConfigNode subdivNode, ScatterBody body)
            {
                SubdivisionProperties props = new SubdivisionProperties();

                string mode = subdivNode.GetValue("subdivisionRangeMode");
                if (mode == "NearestQuads")
                {
                    props.mode = SubdivisionMode.NearestQuads;
                }
                else if (mode == "FixedRange")
                {
                    props.mode = SubdivisionMode.FixedRange;
                }
                else
                {
                    props.mode = SubdivisionMode.FixedRange;
                }

                props.level = (int)ParseFloat(ParseVar(subdivNode, "subdivisionLevel", "1"));
                props.range = ParseFloat(ParseVar(subdivNode, "subdivisionRange", "1000"));

                string minLevel = "";
                bool hasMinLevel = subdivNode.TryGetValue("minimumSubdivision", ref minLevel);
                props.minLevel = hasMinLevel ? (int)ParseFloat(minLevel) : body.minimumSubdivision;

                return props;
            }

            public static string ParseVar(ConfigNode scatter, string valueName, string fallback)
            {
                string data = null;
                bool succeeded = scatter.TryGetValue(valueName, ref data);
                if (!succeeded)
                {
                    ScatterLog.SubLog("[Warning] Unable to get the value of " + valueName + ", it has been set to " + fallback);
                    return fallback;
                }
                else
                {
                    ScatterLog.SubLog("Parsed " + valueName + " as: " + data);
                }
                return data;
            }
            public static Vector3 ParseVector(string data)
            {
                string cleanString = data.Replace(" ", string.Empty);
                string[] components = cleanString.Split(',');
                return new Vector3(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
            }
            public static Vector2 ParseVector2D(string data)
            {
                string cleanString = data.Replace(" ", string.Empty);
                string[] components = cleanString.Split(',');
                return new Vector2(float.Parse(components[0]), float.Parse(components[1]));
            }
            public static float ParseFloat(string data)
            {
                if (data == null)
                {
                    ScatterLog.SubLog("Null value, returning 0");
                    return 0;
                }
                return float.Parse(data);
            }
            public static Color ParseColor(string data)
            {
                if (data == null)
                {
                    ScatterLog.SubLog("Null value, returning 0");
                }
                string cleanString = data.Replace(" ", string.Empty);
                string[] components = cleanString.Split(',');
                return new Color(float.Parse(components[0]), float.Parse(components[1]), float.Parse(components[2]));
            }
        }
    }
}
