using KSP.UI.Screens;
using Parallax.Scaled_System;
using SoftMasking.Samples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parallax
{
    //
    // Toolbar Menu
    // This is what most players will use to configure Parallax - This directly interfaces with the ParallaxGlobalSettings 
    //
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ToolbarMenu : MonoBehaviour
    {
        private static Rect window = new Rect(100, 100, 450, 600);
        private static Rect hoverWindow = new Rect(0, 0, 100, 25);
        private static GUIStyle activeButton;
        private static ApplicationLauncherButton button;

        bool showGUI = false;

        bool initialized = false;

        static bool showCollideables = false;

        static int bingusCount = 0;
        void Start()
        {
            if (!initialized)
            {
                window = new Rect(Screen.width / 2 - 450 / 2, Screen.height / 2 - 50, 450, 100);

                // Create on button hover style
                activeButton = new GUIStyle(HighLogic.Skin.button);
                activeButton.normal.textColor = HighLogic.Skin.label.normal.textColor;
                activeButton.hover.textColor = HighLogic.Skin.label.normal.textColor * 1.25f;

                // Create toolbar button
                Texture buttonTexture = GameDatabase.Instance.GetTexture("ParallaxContinued/Textures/button", false);
                button = ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, IncrementBingus, Nothing, Nothing, Nothing, ApplicationLauncher.AppScenes.ALWAYS, buttonTexture);

                initialized = true;
            }
        }
        public void ShowToolbarGUI()
        {
            showGUI = true;
        }
        public void HideToolbarGUI()
        {
            showGUI = false;
        }
        
        void OnGUI()
        {
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
            labelSkin.fontStyle = FontStyle.Bold;

            GUILayout.Label("Parallax Global Settings", labelSkin);
            GUILayout.Space(15);

            // Terrain shader settings
            GUILayout.Label("Terrain Shader Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod terrainCallback = UpdateTerrainMaterials;
            ParamCreator.ChangeMethod terrainKeywordCallback = UpdateTerrainKeywords;

            ParamCreator.CreateParam("Max Tessellation",         ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation,             GUIHelperFunctions.FloatField, terrainCallback);
            ParamCreator.CreateParam("Tessellation Edge Length", ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength,      GUIHelperFunctions.FloatField, terrainCallback);
            ParamCreator.CreateParam("Tessellation Range",       ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange,        GUIHelperFunctions.FloatField, terrainCallback);
            ParamCreator.CreateParam("Use Advanced Blending",    ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending,     GUIHelperFunctions.BoolField, terrainKeywordCallback);
            ParamCreator.CreateParam("Ambient Occlusion",        ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion,            GUIHelperFunctions.BoolField, terrainKeywordCallback);

            GUILayout.Space(15);
            // Scatter system settings
            GUILayout.Label("Scatter System Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod scatterCallback = UpdateScatterSettings;
            ParamCreator.ChangeMethod colliderCallback = UpdateColliderSettings;

            GUILayout.Label("Note: The game will pause for a few seconds when changing these settings");
            GUILayout.Space(10);
            ParamCreator.CreateParam("Density Multiplier",                  ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier,        GUIHelperFunctions.FloatField, scatterCallback);
            ParamCreator.CreateParam("Range Multiplier",                    ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.rangeMultiplier,          GUIHelperFunctions.FloatField, scatterCallback);
            ParamCreator.CreateParam("Fade Out Start Range",                ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange,        GUIHelperFunctions.FloatField, scatterCallback);
            ParamCreator.CreateParam("Collision Level (Restart Required)",  ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.collisionLevel,           GUIHelperFunctions.IntField,   scatterCallback);
            ParamCreator.CreateParam("Collider Lookahead Time",             ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.colliderLookaheadTime,    GUIHelperFunctions.FloatField, colliderCallback);

            GUILayout.Space(15);
            // Light settings
            GUILayout.Label("Lighting Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod lightingCallback = UpdateLightingSettings;

            ParamCreator.CreateParam("Light Shadows",         ref ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows,          GUIHelperFunctions.BoolField,   lightingCallback);
            ParamCreator.CreateParam("Light Shadows Quality", ref ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadowsQuality,   EnumField,                      lightingCallback);

            GUILayout.Label("Visualisations", HighLogic.Skin.label);
            ParamCreator.CreateParam("Highlight Collideable Objects", ref showCollideables, GUIHelperFunctions.BoolField, ShowCollideableScatters);

            GUILayout.Space(15);
            // Scaled settings
            GUILayout.Label("Scaled System Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod scaledShadowCallback = UpdateScaledShadowSettings;
            ParamCreator.ChangeMethod scaledTextureCallback = UpdateScaledTextureSettings;

            ParamCreator.CreateParam("Scaled Planet Self Shadows", ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows, GUIHelperFunctions.BoolField, scaledShadowCallback);
            ParamCreator.CreateParam("Load Scaled Textures Immediately", ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.loadTexturesImmediately, GUIHelperFunctions.BoolField, scaledTextureCallback);

            GUILayout.Space(15);
            // Save button
            if (GUILayout.Button("Save Changes", activeButton))
            {
                ConfigLoader.parallaxGlobalSettings.SaveSettings();
            }

            ///////////////////////////
            GUILayout.EndVertical();

            // Must be last or buttons wont work
            UnityEngine.GUI.DragWindow();
        }

        //
        //  Callbacks
        //

        static void UpdateTerrainMaterials()
        {
            foreach (ParallaxTerrainBody body in ConfigLoader.parallaxTerrainBodies.Values)
            {
                body.parallaxMaterials.SetAll("_MaxTessellation", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation);
                body.parallaxMaterials.SetAll("_TessellationEdgeLength", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength);
                body.parallaxMaterials.SetAll("_MaxTessellationRange", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange);
            }
        }
        static void UpdateTerrainKeywords()
        {
            foreach (TerrainShaderQuadData data in PQSMod_Parallax.terrainQuadData.Values)
            {
                // Advanced blending
                if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending)
                {
                    data.quadMaterial.EnableKeyword("ADVANCED_BLENDING");
                }
                else
                {
                    data.quadMaterial.DisableKeyword("ADVANCED_BLENDING");
                }

                // Ambient Occlusion
                if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion)
                {
                    data.quadMaterial.EnableKeyword("AMBIENT_OCCLUSION");
                }
                else
                {
                    data.quadMaterial.DisableKeyword("AMBIENT_OCCLUSION");
                }
            }
        }

        static void UpdateScatterSettings()
        {
            ParallaxDebug.Log("Updating scatter system settings from GUI...");
            // Pause to prevent collider issues
            FlightDriver.SetPause(true);

            // Reverse normalisations - we're modifying the range
            foreach (ParallaxScatterBody body in ConfigLoader.parallaxScatterBodies.Values)
            {
                foreach (Scatter scatter in body.fastScatters)
                {
                    ConfigLoader.ReverseNormalisationConversions(scatter);
                }
            }

            // Reperform normalisations - we've modified the range and pop mult, but want to keep lod distances the same and avoid int float conversion inaccuracy
            foreach (ParallaxScatterBody body in ConfigLoader.parallaxScatterBodies.Values)
            {
                foreach (Scatter scatter in body.fastScatters)
                {
                    ConfigLoader.PerformNormalisationConversions(scatter);
                }
            }

            FlightGlobals.currentMainBody.pqsController.RebuildSphere();

            FlightDriver.SetPause(false);
        }

        static void UpdateColliderSettings()
        {
            // Nothing needed here for now
            // All that updates is the collider lookahead but that auto updates next frame anyway
        }

        static void UpdateScaledTextureSettings()
        {
            // Nothing needed here for now
        }


        static void UpdateScaledShadowSettings()
        {
            // Shadows just turned off
            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                RaymarchedShadowsRenderer.Instance.Disable();
            }
            // Shadows just turned on
            else
            {
                RaymarchedShadowsRenderer.Instance.Enable();
            }
            
            // As for instant texture loading, that'll update whenever a planet is next loaded
        }

        static void ShowCollideableScatters()
        {
            ParallaxGUI.ShowCollideableScatters(showCollideables);
        }
        static void UpdateLightingSettings()
        {
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v.parts.Count > 0)
                {
                    if (v.parts[0].isKerbalEVA())
                    {
                        KerbalEVA headlamp = v.parts[0].Modules.GetModule<KerbalEVA>();
                        if (headlamp != null)
                        {
                            Light headLight = headlamp.headLamp.GetComponent<Light>();
                            if (headLight != null)
                            {
                                headLight.shadows = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows ? LightShadows.Soft : LightShadows.None;
                                headLight.shadowResolution = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadowsQuality;
                                headLight.lightShadowCasterMode = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows ? LightShadowCasterMode.Everything : LightShadowCasterMode.Default;
                            }
                        }
                    }
                }
                foreach (Part p in v.Parts)
                {
                    PartModule lightModule = p.Modules.GetModule("ModuleLight");
                    if (lightModule != null)
                    {
                        ModuleLight moduleLight = lightModule as ModuleLight;
                        foreach (Light light in moduleLight.lights)
                        {
                            light.shadows = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows ? LightShadows.Soft : LightShadows.None;
                            light.shadowResolution = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadowsQuality;
                            light.lightShadowCasterMode = ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows ? LightShadowCasterMode.Everything : LightShadowCasterMode.Default;

                            light.shadowBias = 0.05f;
                            light.shadowNormalBias = 0.4f;
                        }
                    }
                }
            }
        }

        static void IncrementBingus()
        {
            bingusCount++;
            if (bingusCount % 10 == 0 && bingusCount > 0)
            {
                button.SetTexture(GameDatabase.Instance.GetTexture("ParallaxContinued/Textures/bingus", false));
            }
            else
            {
                button.SetTexture(GameDatabase.Instance.GetTexture("ParallaxContinued/Textures/button", false));
            }
        }

        void Nothing()
        {

        }
        void OnDestroy()
        {
            if (button)
            {
                ApplicationLauncher.Instance.RemoveModApplication(button);
            }
        }

        //
        // Helper Function
        //

        static LightShadowResolution activeEnumFieldLastValue = LightShadowResolution.Medium;
        static int activeEnumField = -1;
        static string activeEnumFieldString = "";

        public static LightShadowResolution EnumField(LightShadowResolution value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this enum field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(140) });
            int enumFieldID = GUIUtility.GetControlID("EnumField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (enumFieldID == 0)
                return value;

            bool recorded = activeEnumField == enumFieldID;
            bool active = enumFieldID == GUIUtility.keyboardControl;

            if (active && recorded && !activeEnumFieldLastValue.Equals(value))
            { // Value has been modified externally
                activeEnumFieldLastValue = value;
                activeEnumFieldString = value.ToString();
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeEnumFieldString : value.ToString();

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeEnumFieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
            bool parsed = true;
            if (strValue != value.ToString())
            {
                LightShadowResolution newValue;
                parsed = TryParseEnum(strValue, out newValue);
                if (parsed)
                {
                    value = activeEnumFieldLastValue = newValue;
                    valueWasChanged = true;
                }
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeEnumField = enumFieldID;
                activeEnumFieldString = strValue;
                activeEnumFieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeEnumField = -1;
                if (!parsed)
                    value = TryParseEnum(strValue, out LightShadowResolution forcedValue) ? forcedValue : value;
            }

            return value;
        }
        private static bool TryParseEnum(string str, out LightShadowResolution result)
        {
            return Enum.TryParse(str, true, out result) && Enum.IsDefined(typeof(LightShadowResolution), result);
        }
    }
}
