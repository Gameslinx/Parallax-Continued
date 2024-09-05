using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Parallax.Legacy.LegacyScatterConfigLoader;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ParallaxGUI : MonoBehaviour
    {
        // Update is called once per frame
        private static Rect window = new Rect(100, 100, 450, 600);
        private static Rect windowDefault = new Rect(100, 100, 450, 350);

        static bool showGUI = false;
        static bool showDistribution = false;
        static bool showLOD1 = false;
        static bool showLOD2 = false;
        static bool showLOD1Material = false;
        static bool showLOD2Material = false;

        static bool showDistributionNoise = false;

        static bool showMaterial = false;

        static bool showLOD0Keywords = false;
        static bool showLOD1Keywords = false;
        static bool showLOD2Keywords = false;

        static bool showScatterExporter = false;
        static bool showTerrainExporter = false;
        static bool overwriteOnExport = false;

        static bool showDebug = false;
        static bool debugShowFaceOrientation = false;
        public static bool debugShowCollideables = false;

        static int currentScatterIndex = 0;

        public static Scatter[] scatters;
        static bool currentBodyHasScatters = false;
        static bool currentBodyHasTerrain = false;

        static GUIStyle activeButton;

        void Start()
        {
            window = new Rect(Screen.width / 2 - 450 / 2, Screen.height / 2 - 50, 450, 100);

            activeButton = new GUIStyle(HighLogic.Skin.button);
            activeButton.normal.textColor = HighLogic.Skin.label.normal.textColor;
            activeButton.hover.textColor = HighLogic.Skin.label.normal.textColor * 1.25f;

            PQSStartPatch.onPQSStart += OnBodyChanged;
            OnBodyChanged(FlightGlobals.currentMainBody.name);
        }
        void OnDisable()
        {
            PQSStartPatch.onPQSStart -= OnBodyChanged;
        }
        // Update GUI on body change, reset scatter counter and get new scatters
        void OnBodyChanged(string bodyName)
        {
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                // Includes shared scatters
                scatters = ConfigLoader.parallaxScatterBodies[bodyName].scatters.Values.ToArray();
                currentBodyHasScatters = true;
                currentScatterIndex = 0;
            }
            else
            {
                currentBodyHasScatters = false;
            }

            if (ConfigLoader.parallaxTerrainBodies.ContainsKey(bodyName))
            {
                currentBodyHasTerrain = true;
            }
            else
            {
                currentBodyHasTerrain = false;
            }
        }
        void Update()
        {
            // Determine whether the GUI should be shown or not
            bool toggleDisplayGUI = (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.P));
            if (toggleDisplayGUI)
            {
                if (!currentBodyHasScatters && !currentBodyHasTerrain)
                {
                    ScreenMessages.PostScreenMessage("This body is not configured for Parallax");
                    showGUI = false;
                    return;
                }
                showGUI = !showGUI;
            }
        }
        void OnGUI()
        {
            // Show GUI if toggle enabled
            if (showGUI)
            {
                window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Parallax GUI", HighLogic.Skin.window);
            }
        }
        static bool editorIsScatter = true;
        static void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            ///////////////////////////
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Terrain Edit Mode", HighLogic.Skin.button))
            {
                editorIsScatter = false;
            }
            //GUILayout.Space(350);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Editing " + (editorIsScatter ? "Scatters" : "Terrain"), HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Scatter Edit Mode", HighLogic.Skin.button))
            {
                editorIsScatter = true;
            }
            GUILayout.EndHorizontal();

            // Force an editor if only one is configured
            if (currentBodyHasTerrain && !currentBodyHasScatters)
            {
                editorIsScatter = false;
            }
            if (currentBodyHasScatters && !currentBodyHasTerrain)
            {
                editorIsScatter = true;
            }

            if (!editorIsScatter)
            {
                TerrainMenu();
            }
            else
            {
                ScatterMenu();
            }

            ///////////////////////////
            GUILayout.EndVertical();

            // Must be last or buttons wont work
            UnityEngine.GUI.DragWindow();
        }
        static void TerrainMenu()
        {
            ParallaxTerrainBody body = ConfigLoader.parallaxTerrainBodies[FlightGlobals.currentMainBody.name];
            ShaderProperties props = body.terrainShaderProperties;
            ParamCreator.ChangeMethod callback = body.SetMaterialValues;
            ParamCreator.ChangeMethod texCallback = body.Reload;
            // Parse shader properties

            GUILayout.Label("Terrain Shader Properties: ", HighLogic.Skin.label);
            GUILayout.Space(15);
            // Process floats
            GUILayout.Label("Floats: ", HighLogic.Skin.label);
            List<string> floatKeys = new List<string>(props.shaderFloats.Keys);
            foreach (string key in floatKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                float value = props.shaderFloats[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.FloatField);
                if (valueChanged)
                {
                    props.shaderFloats[key] = value;
                    callback();
                }
            }

            // Process vectors
            GUILayout.Label("Vectors: ", HighLogic.Skin.label);
            List<string> vectorKeys = new List<string>(props.shaderVectors.Keys);
            foreach (string key in vectorKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                Vector3 value = props.shaderVectors[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.Vector3Field);
                if (valueChanged)
                {
                    props.shaderVectors[key] = value;
                    callback();
                }
            }

            // Process colors
            GUILayout.Label("Colors: ", HighLogic.Skin.label);
            List<string> colorKeys = new List<string>(props.shaderColors.Keys);
            foreach (string key in colorKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                Color value = props.shaderColors[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.ColorField);
                if (valueChanged)
                {
                    props.shaderColors[key] = value;
                    callback();
                }
            }

            GUILayout.Space(15);
            GUILayout.Label("Planet Subdivision Properties: ", HighLogic.Skin.label);
            GUILayout.Label("Subdivision settings are not saved, you must update the config values manually");
            PQSMod_Parallax pqsMod = FlightGlobals.currentMainBody.pqsController.GetComponentsInChildren<PQSMod>().Where(x => x.GetType() == typeof(PQSMod_Parallax)).FirstOrDefault() as PQSMod_Parallax;
            bool subdivisionUpdated = false;
            subdivisionUpdated |= ParamCreator.CreateParam("Subdivision Radius", ref pqsMod.subdivisionRadius, GUIHelperFunctions.FloatField);
            subdivisionUpdated |= ParamCreator.CreateParam("Subdivision Level", ref pqsMod.subdivisionLevel, GUIHelperFunctions.IntField);

            if (subdivisionUpdated)
            {
                UpdateSubdivision(pqsMod);
            }

            GUILayout.Space(15);
            GUILayout.Label("Exporter Options", HighLogic.Skin.label);
            if (GUILayout.Button("Exporter", HighLogic.Skin.button))
            {
                showTerrainExporter = !showTerrainExporter;
            }
            if (showTerrainExporter)
            {
                if (overwriteOnExport)
                {
                    GUILayout.Label("Existing config will be backed up to GameData/Parallax/Exports/Backups");
                }
                else
                {
                    GUILayout.Label("Configs will be exported to GameData/Parallax/Exports/Configs");
                    GUILayout.Label("To apply them, you must copy the contents of the .cfg to the active .cfg");
                }
                ParamCreator.CreateParam("Overwrite Existing Configs", ref overwriteOnExport, GUIHelperFunctions.BoolField);
                if (GUILayout.Button("Save Current Terrain", HighLogic.Skin.button))
                {
                    if (overwriteOnExport)
                    {
                        // Backup current planet
                        ConfigNode currentNode = ConfigLoader.GetPlanetTerrainNode(FlightGlobals.currentMainBody.name);
                        if (currentNode != null)
                        {
                            bool saved = ConfigLoader.SaveConfigNode(currentNode, KSPUtil.ApplicationRootPath + "GameData/Parallax/Exports/Backups/ParallaxTerrain-" + FlightGlobals.currentMainBody.name + ".cfg");
                            // Backup created, now overwrite
                            if (saved)
                            {
                                // Used just for file path, really
                                UrlDir.UrlConfig rootPlanetNode = ConfigLoader.GetBaseParallaxNode(FlightGlobals.currentMainBody.name);
                                ConfigNode rootPlanetNodeConfig = rootPlanetNode.config;

                                ConfigNode originalTerrainNode = ConfigLoader.GetPlanetTerrainNode(FlightGlobals.currentMainBody.name);

                                List<ConfigNode> precedingNodes = new List<ConfigNode>();
                                List<ConfigNode> trailingNodes = new List<ConfigNode>();

                                // Get our new config node
                                ConfigNode newTerrainNode = ConfigLoader.parallaxTerrainBodies[FlightGlobals.currentMainBody.name].ToConfigNode();

                                // Populates lists preceding and trailing so we can preserve the terrain node position in the config instead of forcing it to the bottom
                                ConfigLoader.DeterminePrecedingAndTrailingNodes(rootPlanetNodeConfig, originalTerrainNode, precedingNodes, trailingNodes);

                                // Remove original node, add preceding nodes, add new node, add trailing nodes
                                ConfigLoader.OverwriteConfigNode(rootPlanetNodeConfig, newTerrainNode, precedingNodes, trailingNodes, "Body");

                                string path = "GameData/" + rootPlanetNode.url.Replace("/ParallaxTerrain", string.Empty) + ".cfg";
                                ConfigLoader.SaveConfigNode(rootPlanetNodeConfig, path);
                            }
                        }
                    }
                    else
                    {
                        ScreenMessages.PostScreenMessage("Non-overwriting terrain saving not implemented yet");
                    }
                }
            }
        }
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

            // Process ints
            GUILayout.Label("Integers: ", HighLogic.Skin.label);
            List<string> intKeys = new List<string>(materialParams.shaderProperties.shaderInts.Keys);
            foreach (string key in intKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                int value = properties.shaderInts[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.IntField);
                if (valueChanged)
                {
                    properties.shaderInts[key] = value;
                    callback();
                }
            }

            // Process floats
            GUILayout.Label("Floats: ", HighLogic.Skin.label);
            List<string> floatKeys = new List<string>(materialParams.shaderProperties.shaderFloats.Keys);
            foreach (string key in floatKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                float value = properties.shaderFloats[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.FloatField);
                if (valueChanged)
                {
                    properties.shaderFloats[key] = value;
                    callback();
                }
            }

            // Process vectors
            GUILayout.Label("Vectors: ", HighLogic.Skin.label);
            List<string> vectorKeys = new List<string>(materialParams.shaderProperties.shaderVectors.Keys);
            foreach (string key in vectorKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                Vector3 value = properties.shaderVectors[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.Vector3Field);
                if (valueChanged)
                {
                    properties.shaderVectors[key] = value;
                    callback();
                }
            }

            // Process colors
            GUILayout.Label("Colors: ", HighLogic.Skin.label);
            List<string> colorKeys = new List<string>(materialParams.shaderProperties.shaderColors.Keys);
            foreach (string key in colorKeys)
            {
                // Can't pass dictionary value by reference - create temporary variable, update it, then run the callback
                Color value = properties.shaderColors[key];
                bool valueChanged = ParamCreator.CreateParam(key, ref value, GUIHelperFunctions.ColorField);
                if (valueChanged)
                {
                    properties.shaderColors[key] = value;
                    callback();
                }
            }

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
        static void ProcessSaveButton(Scatter scatter)
        {
            GUILayout.Space(15);
            GUILayout.Label("Exporter Options", HighLogic.Skin.label);
            if (GUILayout.Button("Exporter", GetButtonColor(showScatterExporter)))
            {
                showScatterExporter = !showScatterExporter;
            }
            if (showScatterExporter)
            {
                if (overwriteOnExport)
                {
                    GUILayout.Label("Existing config will be backed up to GameData/Parallax/Exports/Backups");
                }
                else
                {
                    GUILayout.Label("Configs will be exported to GameData/Parallax/Exports/Configs");
                    GUILayout.Label("To apply them, you must copy the contents of the .cfg to the active .cfg");
                }
                ParamCreator.CreateParam("Overwrite Existing Configs", ref overwriteOnExport, GUIHelperFunctions.BoolField);
                if (overwriteOnExport && GUILayout.Button("Save Current Scatter", HighLogic.Skin.button))
                {
                    ConfigNode currentNode = scatter.isShared ? new ConfigNode("SharedScatter") : new ConfigNode("Scatter");
                    ConfigLoader.GetScatterConfigNode(FlightGlobals.currentMainBody.name, scatter.scatterName, scatter.isShared).CopyTo(currentNode);
                    currentNode.name = "Scatter-Backup";
                    // Removes body name from the start of the scatter's name
                    bool saved = ConfigLoader.SaveConfigNode(currentNode, KSPUtil.ApplicationRootPath + "GameData/Parallax/Exports/Backups/" + scatter.scatterName.Split('-')[1] + ".cfg");

                    // Backup created, now overwrite
                    if (saved)
                    {
                        // Used just for file path, really
                        // "ParallaxScatters"
                        UrlDir.UrlConfig rootScatterNode = ConfigLoader.GetRootScatterNode(FlightGlobals.currentMainBody.name);

                        // "Body"
                        ConfigNode rootPlanetNodeConfig = ConfigLoader.GetPlanetScatterNode(FlightGlobals.currentMainBody.name);

                        ConfigNode originalScatterNode = ConfigLoader.GetScatterConfigNode(FlightGlobals.currentMainBody.name, scatter.scatterName, rootPlanetNodeConfig, scatter.isShared);

                        List<ConfigNode> precedingNodes = new List<ConfigNode>();
                        List<ConfigNode> trailingNodes = new List<ConfigNode>();

                        // Get our new config node
                        ConfigNode newScatterNode = scatter.isShared ? (scatter as SharedScatter).ToConfigNode() : scatter.ToConfigNode();

                        // Populates lists preceding and trailing so we can preserve the scatter node position in the config instead of forcing it to the bottom
                        ConfigLoader.DeterminePrecedingAndTrailingNodes(rootPlanetNodeConfig, originalScatterNode, precedingNodes, trailingNodes);

                        // Remove original node, add preceding nodes, add new node, add trailing nodes
                        ConfigLoader.OverwriteConfigNode(rootPlanetNodeConfig, newScatterNode, precedingNodes, trailingNodes, "Scatter", "SharedScatter");

                        Debug.Log("URL: " + rootScatterNode.url);

                        string path = "GameData/" + rootScatterNode.url.Replace("/ParallaxScatters", string.Empty) + ".cfg";
                        ConfigLoader.SaveConfigNode(rootScatterNode.config, path);
                    }
                }
                if (!overwriteOnExport && GUILayout.Button("Save Current Scatter", HighLogic.Skin.button))
                {
                    ConfigNode node = scatter.ToConfigNode();
                    
                    // Prevent Parallax from trying to load anything that was exported with overwrite disabled
                    node.name = node.name + "-EXPORTED";

                    // Export to Exports/Configs/PlanetName/ScatterName.cfg
                    string directory = "GameData/Parallax/Exports/Configs/" + scatter.scatterName.Split('-')[0] + "/";
                    string fileName = scatter.scatterName.Split('-')[1] + "-Exported.cfg";

                    // Create directory and save to it
                    Directory.CreateDirectory(KSPUtil.ApplicationRootPath + directory);
                    ConfigLoader.SaveConfigNode(node, directory + fileName);
                }

                //if (GUILayout.Button("Save Current Planet", HighLogic.Skin.button))
                //{
                //
                //}
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
        static void UpdateSubdivision(PQSMod_Parallax pqsMod)
        {
            foreach (TerrainShaderQuadData quadData in PQSMod_Parallax.terrainQuadData.Values)
            {
                quadData.UpdateSubdivision(pqsMod.subdivisionLevel, pqsMod.subdivisionRadius);
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
        static GUIStyle GetButtonColor(bool isActive)
        {
            if (isActive)
            {
                return activeButton;
            }
            else
            {
                return HighLogic.Skin.button;
            }
        }
    }
    /// <summary>
    /// Helps create parameters for the in-game parallax config gui.
    /// </summary>
    public static class ParamCreator
    {
        public delegate T ParamTypeMethod<T>(T value, out bool valueChanged);
        public delegate void ChangeMethod();
        /// <summary>
        /// Create a GUI parameter: Left aligned label, right aligned input
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="existingValue"></param>
        /// <param name="fieldMethod"></param>
        public static void CreateParam<T>(string name, ref T existingValue, ParamTypeMethod<T> fieldMethod, ChangeMethod callback)
        {
            // Create a left aligned label and right aligned text box
            GUILayout.BeginHorizontal();

            GUILayout.Label(name);
            existingValue = fieldMethod(existingValue, out bool valueWasChanged);

            if (valueWasChanged)
            {
                callback();
            }

            GUILayout.EndHorizontal();
        }
        /// <summary>
        /// Create a GUI parameter: Left aligned label, right aligned input. No callback.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="existingValue"></param>
        /// <param name="fieldMethod"></param>
        /// <returns></returns>
        public static bool CreateParam<T>(string name, ref T existingValue, ParamTypeMethod<T> fieldMethod)
        {
            // Create a left aligned label and right aligned text box
            GUILayout.BeginHorizontal();

            GUILayout.Label(name);
            existingValue = fieldMethod(existingValue, out bool valueWasChanged);

            GUILayout.EndHorizontal();
            return valueWasChanged;
        }
    }
}
