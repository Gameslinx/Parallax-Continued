using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ParallaxGUI : MonoBehaviour
    {
        // Update is called once per frame
        private static Rect window = new Rect(100, 100, 450, 300);
        private static Rect windowDefault = new Rect(100, 100, 450, 145);

        static bool showGUI = false;
        static bool showDistribution = false;
        static bool showLOD1 = false;
        static bool showLOD2 = false;

        static bool showMaterial = false;
        static bool showKeywords = false;

        static int currentScatterIndex = 0;

        public static Scatter[] scatters;
        bool currentBodyHasScatters = false;

 

        void Start()
        {
            window = new Rect(0, 0, 450, 100);
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
                scatters = ConfigLoader.parallaxScatterBodies[bodyName].fastScatters;
                currentBodyHasScatters = true;
                currentScatterIndex = 0;
            }
            else
            {
                currentBodyHasScatters = false;
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
            if (!currentBodyHasScatters) 
            {
                ScreenMessages.PostScreenMessage("This body is not configured for Parallax Scatters");
            }
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

            GUIStyle labelSkin = HighLogic.Skin.label;
            labelSkin.alignment = TextAnchor.MiddleCenter;

            // Reset window size
            if (!showDistribution && !showMaterial)
            {
                window.height = windowDefault.height;
            }

            // Show current scatter
            Scatter scatter = GetScatter();
            GUILayout.Label("Currently displaying scatter: " + scatter.scatterName, labelSkin);

            // Align correctly
            labelSkin.alignment = TextAnchor.MiddleLeft;

            ProcessDistributionParams(scatter);
            ProcessMaterialParams(scatter.materialParams, ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].instancedMaterialLOD0, ScatterManager.Instance.fastScatterRenderers[scatter.scatterName].SetLOD0MaterialParams);

            ///////////////////////////
            GUILayout.EndVertical();

            // Must be last or buttons wont work
            UnityEngine.GUI.DragWindow();
        }
        static Scatter GetScatter()
        {
            // Advance scatter
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Scatter", HighLogic.Skin.button))
            {
                currentScatterIndex = DecrementScatterIndex();
            }
            if (GUILayout.Button("Next Scatter", HighLogic.Skin.button))
            {
                currentScatterIndex = IncrementScatterIndex();
            }
            GUILayout.EndHorizontal();
            return scatters[currentScatterIndex];
        }
        static void ProcessDistributionParams(Scatter scatter)
        {
            if (GUILayout.Button("Distribution Params", HighLogic.Skin.button))
            {
                showDistribution = !showDistribution;
            }
            if (showDistribution)
            {
                // Callback method that regenerates scatters when these values are changed
                ParamCreator.ChangeMethod callback = scatter.ReinitializeDistribution;
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

                ParamCreator.CreateParam("Align To Terrain Normal", ref scatter.distributionParams.alignToTerrainNormal, GUIHelperFunctions.IntField, callback);
            
                if (GUILayout.Button("LOD 1 Params", HighLogic.Skin.button))
                {
                    showLOD1 = !showLOD1;
                }
                if (showLOD1)
                {
                    GUILayout.Label("LOD 1 Params: ", HighLogic.Skin.label);
                    ParamCreator.CreateParam("Range", ref scatter.distributionParams.lod1.range, GUIHelperFunctions.FloatField, callback);
                    // Implement material
                }

                if (GUILayout.Button("LOD 2 Params", HighLogic.Skin.button))
                {
                    showLOD2 = !showLOD2;
                }
                if (showLOD2)
                {
                    GUILayout.Label("LOD 2 Params: ", HighLogic.Skin.label);
                    ParamCreator.CreateParam("Range", ref scatter.distributionParams.lod2.range, GUIHelperFunctions.FloatField, callback);
                    // Implement material
                }
            }
        }
        static void ProcessMaterialParams(in MaterialParams materialParams, Material material, ParamCreator.ChangeMethod callback)
        {
            if (GUILayout.Button("Material Params", HighLogic.Skin.button))
            {
                showMaterial = !showMaterial;
            }
            if (showMaterial)
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
                if (GUILayout.Button("Show Keywords", HighLogic.Skin.button))
                {
                    showKeywords = !showKeywords;
                }
                if (showKeywords)
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
                            }
                            if (enabled)
                            {
                                materialParams.shaderKeywords.Add(keywordName);
                                // Now we need to initialize defaults for the keywords
                                ConfigLoader.InitializeTemplateConfig(node, materialParams.shaderProperties);
                            }
                            callback();
                        }
                    }
                }
                
            }
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
