using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Parallax.Tools.TextureExporter;
using static SystemInformation;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public partial class ParallaxGUI : MonoBehaviour
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
        static bool currentBodyHasScaled = false;

        static ParallaxScaledBody currentScaledBody;

        public static GUIEditorMode editorMode = GUIEditorMode.Terrain;
        private static List<GUIEditorMode> possibleEditorModes = new List<GUIEditorMode>();
        private static int currentEditorModeIndex = 0;

        private static TextureExporterOptions exportOptions;

        static GUIStyle activeButton;
        public enum GUIEditorMode
        {
            Scatter,
            Terrain,
            Scaled
        }
        void Awake()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.TRACKSTATION)
            {
                Destroy(this);
            }
        }
        void Start()
        {
            window = new Rect(Screen.width / 2 - 450 / 2, Screen.height / 2 - 50, 450, 100);

            activeButton = new GUIStyle(HighLogic.Skin.button);
            activeButton.normal.textColor = HighLogic.Skin.label.normal.textColor;
            activeButton.hover.textColor = HighLogic.Skin.label.normal.textColor * 1.25f;

            exportOptions = new TextureExporterOptions()
            {
                horizontalResolution = 4096,
                exportColor = true,
                exportHeight = true,
                exportNormal = true,
                multithread = true
            };

            // Register events
            PQSStartPatch.onPQSStart += OnBodyChanged;
            GameEvents.onPlanetariumTargetChanged.Add(OnScaledBodyChanged);

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                OnBodyChanged(FlightGlobals.currentMainBody.name);
            }
            else
            {
                editorMode = GUIEditorMode.Scaled;
            }
        }
        void OnDisable()
        {
            PQSStartPatch.onPQSStart -= OnBodyChanged;
            GameEvents.onPlanetariumTargetChanged.Remove(OnScaledBodyChanged);
        }
        // Update GUI on body change, reset scatter counter and get new scatters
        void OnBodyChanged(string bodyName)
        {
            possibleEditorModes.Clear();
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(bodyName))
            {
                // Includes shared scatters
                scatters = ConfigLoader.parallaxScatterBodies[bodyName].scatters.Values.ToArray();
                currentBodyHasScatters = true;
                currentScatterIndex = 0;
                possibleEditorModes.Add(GUIEditorMode.Scatter);
            }
            else
            {
                currentBodyHasScatters = false;
            }

            if (ConfigLoader.parallaxTerrainBodies.ContainsKey(bodyName))
            {
                currentBodyHasTerrain = true;
                possibleEditorModes.Add(GUIEditorMode.Terrain);
            }
            else
            {
                currentBodyHasTerrain = false;
            }

            if (ConfigLoader.parallaxScaledBodies.ContainsKey(bodyName))
            {
                currentBodyHasScaled = true;
                possibleEditorModes.Add(GUIEditorMode.Scaled);
            }
            else
            {
                currentBodyHasScaled = false;
            }
        }
        // Test if this body has parallax scaled
        void OnScaledBodyChanged(MapObject body)
        {
            if (body.celestialBody == null)
            {
                // Probably focusing on a vessel. Or at least, not a planet
                return;
            }
            if (ConfigLoader.parallaxScaledBodies.ContainsKey(body.celestialBody.name))
            {
                currentScaledBody = ConfigLoader.parallaxScaledBodies[body.celestialBody.name];
                if (!possibleEditorModes.Contains(GUIEditorMode.Scaled)) 
                {
                    possibleEditorModes.Add(GUIEditorMode.Scaled);
                    currentBodyHasScaled = true;
                }
            }
            else
            {
                possibleEditorModes.Remove(GUIEditorMode.Scaled);
                currentBodyHasScaled = false;
            }
        }
        void Update()
        {
            // Determine whether the GUI should be shown or not
            bool toggleDisplayGUI = (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.P));
            if (toggleDisplayGUI)
            {
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
        static void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            ///////////////////////////
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Edit Mode", HighLogic.Skin.button))
            {
                editorMode = WrapEditorMode(currentEditorModeIndex - 1);
                Debug.Log("Editor mode changed: " + editorMode.ToString());
            }
            //GUILayout.Space(350);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Editing " + editorMode.ToString(), HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Next Edit Mode", HighLogic.Skin.button))
            {
                editorMode = WrapEditorMode(currentEditorModeIndex + 1);
                Debug.Log("Editor mode changed: " + editorMode.ToString());
            }
            GUILayout.EndHorizontal();

            if (editorMode == GUIEditorMode.Terrain)
            {
                TerrainMenu();
            }
            else if (editorMode == GUIEditorMode.Scatter)
            {
                ScatterMenu();
            }
            else if (editorMode == GUIEditorMode.Scaled)
            {
                if (currentBodyHasScaled)
                {
                    ScaledMenu(currentScaledBody);
                    TextureExporterMenu();
                }
                else
                {
                    TextureExporterMenu();
                }
            }
            else
            {
                GUILayout.Label("Current planet is not configured for Parallax", HighLogic.Skin.label);
            }

            ///////////////////////////
            GUILayout.EndVertical();

            // Must be last or buttons wont work
            UnityEngine.GUI.DragWindow();
        }
        static GUIEditorMode WrapEditorMode(int value)
        {
            int max = possibleEditorModes.Count;
            int min = 0;
            if (value > max - 1)
            {
                currentEditorModeIndex = (value - max);
                return possibleEditorModes[currentEditorModeIndex];
            }
            if (value < min)
            {
                currentEditorModeIndex = (max - 1);
                return possibleEditorModes[currentEditorModeIndex];
            }
            currentEditorModeIndex = value;
            return possibleEditorModes[currentEditorModeIndex];
        }
        static GUIEditorMode GetEditorMode(bool terrain, bool scatters, bool scaled)
        {
            // Only one configured
            if (!scatters && !scaled && terrain && editorMode != GUIEditorMode.Terrain)
            {
                return GUIEditorMode.Terrain;
            }
            if (!scaled && !terrain && scatters && editorMode != GUIEditorMode.Scatter)
            {
                return GUIEditorMode.Scatter;
            }
            if (!terrain && !scatters && scaled && editorMode != GUIEditorMode.Scaled)
            {
                return GUIEditorMode.Scaled;
            }
        
            // Two configured but we're on the wrong one
            if (terrain && scatters && !scaled && editorMode == GUIEditorMode.Scaled)
            {
                
            }
            return GUIEditorMode.Terrain;
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
                    GUILayout.Label("Existing config will be backed up to GameData/ParallaxContinued/Exports/Backups");
                }
                else
                {
                    GUILayout.Label("Configs will be exported to GameData/ParallaxContinued/Exports/Configs");
                    GUILayout.Label("To apply them, you must copy the contents of the .cfg to the active .cfg");
                }
                ParamCreator.CreateParam("Overwrite Existing Configs", ref overwriteOnExport, GUIHelperFunctions.BoolField);
                if (overwriteOnExport && GUILayout.Button("Save Current Scatter", HighLogic.Skin.button))
                {
                    ConfigNode currentNode = scatter.isShared ? new ConfigNode("SharedScatter") : new ConfigNode("Scatter");
                    ConfigLoader.GetScatterConfigNode(FlightGlobals.currentMainBody.name, scatter.scatterName, scatter.isShared).CopyTo(currentNode);
                    currentNode.name = "Scatter-Backup";
                    // Removes body name from the start of the scatter's name
                    bool saved = ConfigLoader.SaveConfigNode(currentNode, KSPUtil.ApplicationRootPath + "GameData/ParallaxContinued/Exports/Backups/" + scatter.scatterName.Split('-')[1] + ".cfg");

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
                    string directory = "GameData/ParallaxContinued/Exports/Configs/" + scatter.scatterName.Split('-')[0] + "/";
                    string fileName = scatter.scatterName.Split('-')[1] + "-Exported.cfg";

                    // Create directory and save to it
                    Directory.CreateDirectory(KSPUtil.ApplicationRootPath + directory);
                    ConfigLoader.SaveConfigNode(node, directory + fileName);
                }
            }
        }
        public static void ProcessGenericMaterialParams(in MaterialParams materialParams, ParamCreator.ChangeMethod callback, bool includeKeywords = false, Material material = null, string configName = null)
        {
            // Process ints
            GUILayout.Label("Integers: ", HighLogic.Skin.label);
            List<string> intKeys = new List<string>(materialParams.shaderProperties.shaderInts.Keys);
            ShaderProperties properties = materialParams.shaderProperties;
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

            if (!includeKeywords)
            {
                return;
            }

            string shaderName = materialParams.shader;
            ConfigNode config = GameDatabase.Instance.GetConfigs(configName).FirstOrDefault().config.GetNodes("ParallaxShader").Where(x => x.GetValue("name") == shaderName).FirstOrDefault();
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
                    }
                    if (enabled)
                    {
                        materialParams.shaderKeywords.Add(keywordName);
                        material.EnableKeyword(keywordName);

                        // Now we need to initialize defaults for the keywords
                        ConfigLoader.InitializeTemplateConfig(node, materialParams.shaderProperties);
                    }
                    callback();
                }
            }
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

            if (valueWasChanged && callback != null)
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
