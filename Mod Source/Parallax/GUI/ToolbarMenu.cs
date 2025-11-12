using KSP.UI;
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
        private static Rect hoverWindow = new Rect(0, 0, 350, 80);
        private static GUIStyle activeButton;
        private static ApplicationLauncherButton button;

        static string maxTessellationTooltip = "The close-up terrain detail - Makes terrain 3D. Higher is better quality, max 64. Moderate GPU performance impact";
        static string tessellationEdgeLengthTooltip = "The tessellation level of detail falloff with camera distance. Lower is higher quality. Moderate GPU performance impact. Values below 1 not recommended";
        static string maxTessellationRangeTooltip = "The range within which the terrain tessellates. Low GPU performance impact. You can set this very high, but the performance impact will vary depending on your tessellation edge length setting";
        static string advancedTextureBlendingTooltip = "Switches between simple texture blending and more realistic heightmap-based texture blending. Very low GPU performance impact";
        static string ambientOcclusionTooltip = "Toggles texture baked ambient occlusion. Very low GPU performance impact";

        static string densityMultiplierTooltip = "Multiplies the number of scatters generated. Setting this to 2 will double the number of objects you see, while 0.5 will halve them. High GPU performance impact";
        static string rangeMultiplierTooltip = "Multiplies the scatter render distance. Setting this to 2 will double the render distance (which is 4x the number of objects rendered). High GPU performance impact";
        static string fadeOutStartRangeTooltip = "The percentage of the scatters' render distances at which scatters will start to despawn. This helps prevent a 'hard edge' where scatters suddenly despawn. Low GPU performance impact";
        static string collisionLevelTooltip = "Controls which objects are collideable.\u000A-1 = off.\u000A0 = absolutely everything, including foliage.\u000A1 = everything reasonable including small rocks.\u000A2 = most objects (default).\u000A3 = only large objects.\u000A4 = only huge objects.\u000AModerate CPU performance impact";
        static string colliderLookaheadTimeTooltip = "Controls how many seconds into the future to predict colliders - only use this if you use a pathfinding rover mod, or need the distance to far away scatters when moving at speed. Moderate CPU performance impact";
        static string showCollidersTooltip = "Toggle collider visibility. If you're unsure if you can hit an object or not, toggle this. Collideable objects are green, while non collideable objects are red";

        static string lightShadowsTooltip = "Toggle craft light shadows. Moderate GPU performance impact when lights are on depending on light shadows quality";
        static string lightShadowsQualityTooltip = "Controls the craft light shadow resolution. Moderate GPU performance impact when lights are on";

        static string scaledSpaceShadowsTooltip = "Toggles raymarched planet shadows from orbit. Low GPU performance impact";
        static string smoothScaledSpaceShadowsTooltip = "Toggles smooth planet shadows. No performance impact. Disable or increase the shadow step count if you see shadow flickering";
        static string scaledRaymarchedShadowStepCountTooltip = "Controls how many raymarch steps to use for the orbital shadows. Higher is higher quality. Moderate GPU performance impact";
        static string loadTexturesImmediatelyTooltip = "Toggles loading planet orbit textures immediately or spreads it out over a few frames. No performance impact";

        bool showGUI = false;

        bool initialized = false;

        static bool showCollideables = false;
        static string tooltip = "";

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

                if (!string.IsNullOrEmpty(tooltip))
                {
                    hoverWindow.x = Event.current.mousePosition.x + 22;
                    hoverWindow.y = Event.current.mousePosition.y - hoverWindow.height / 2;
                    hoverWindow = GUILayout.Window(GetInstanceID() + 1, hoverWindow, ShowTooltip, "Tooltip", HighLogic.Skin.window);
                }
            }
        }

        static void ShowTooltip(int windowID)
        {
            GUILayout.BeginVertical();

            GUIStyle skin = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };

            // Measure content size dynamically
            GUIContent content = new GUIContent(tooltip);
            Vector2 size = skin.CalcSize(content);
            float minHeight = skin.CalcHeight(content, size.x); // Ensure multi-line height is considered

            // Adjust window size based on content
            hoverWindow.height = Mathf.Max(minHeight + 10, 30); // Minimum height to avoid excessive shrinking

            GUILayout.Label(tooltip, skin);
            GUILayout.EndVertical();
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

            ParamCreator.CreateParam("Max Tessellation",         ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation,             GUIHelperFunctions.FloatField, terrainCallback, maxTessellationTooltip);
            ParamCreator.CreateParam("Tessellation Edge Length", ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength,      GUIHelperFunctions.FloatField, terrainCallback, tessellationEdgeLengthTooltip);
            ParamCreator.CreateParam("Tessellation Range",       ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange,        GUIHelperFunctions.FloatField, terrainCallback, maxTessellationRangeTooltip);
            ParamCreator.CreateParam("Use Advanced Blending",    ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending,     GUIHelperFunctions.BoolField, terrainKeywordCallback, advancedTextureBlendingTooltip);
            ParamCreator.CreateParam("Ambient Occlusion",        ref ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion,            GUIHelperFunctions.BoolField, terrainKeywordCallback, ambientOcclusionTooltip);

            GUILayout.Space(15);
            // Scatter system settings
            GUILayout.Label("Scatter System Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod scatterCallback = UpdateScatterSettings;
            ParamCreator.ChangeMethod colliderCallback = UpdateColliderSettings;

            GUILayout.Label("Note: The game will pause for a few seconds when changing these settings");
            GUILayout.Label("Important: You should restart the game after changing and saving these");
            GUILayout.Space(10);
            ParamCreator.CreateParam("Density Multiplier",                  ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.densityMultiplier,        GUIHelperFunctions.FloatField, scatterCallback, densityMultiplierTooltip);
            ParamCreator.CreateParam("Range Multiplier",                    ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.rangeMultiplier,          GUIHelperFunctions.FloatField, scatterCallback, rangeMultiplierTooltip);
            ParamCreator.CreateParam("Fade Out Start Range",                ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange,        GUIHelperFunctions.FloatField, scatterCallback, fadeOutStartRangeTooltip);
            ParamCreator.CreateParam("Collision Level (Restart Required)",  ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.collisionLevel,           GUIHelperFunctions.IntField,   scatterCallback, collisionLevelTooltip);
            ParamCreator.CreateParam("Collider Lookahead Time",             ref ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.colliderLookaheadTime,    GUIHelperFunctions.FloatField, colliderCallback, colliderLookaheadTimeTooltip);

            GUILayout.Space(15);
            // Light settings
            GUILayout.Label("Lighting Settings", HighLogic.Skin.label);
            ParamCreator.ChangeMethod lightingCallback = UpdateLightingSettings;

            ParamCreator.CreateParam("Light Shadows",         ref ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadows,          GUIHelperFunctions.BoolField,   lightingCallback, lightShadowsTooltip);
            ParamCreator.CreateParam("Light Shadows Quality", ref ConfigLoader.parallaxGlobalSettings.lightingGlobalSettings.lightShadowsQuality,   EnumField,                      lightingCallback, lightShadowsQualityTooltip);

            GUILayout.Label("Visualisations", HighLogic.Skin.label);
            ParamCreator.CreateParam("Highlight Collideable Objects", ref showCollideables, GUIHelperFunctions.BoolField, ShowCollideableScatters, showCollidersTooltip);

            GUILayout.Space(15);
            // Scaled settings
            GUILayout.Label("Scaled System Settings", HighLogic.Skin.label);
            GUILayout.Label("Tip: You can press Alt + 7 in the tracking station to display the shadow map");
            ParamCreator.ChangeMethod scaledShadowCallback = UpdateScaledShadowSettings;
            ParamCreator.ChangeMethod scaledTextureCallback = UpdateScaledTextureSettings;
            ParamCreator.ChangeMethod scaledShadowMaterialCallback = UpdateScaledShadowMaterialSettings;

            ParamCreator.CreateParam("Scaled Planet Self Shadows",              ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows, GUIHelperFunctions.BoolField, scaledShadowCallback, scaledSpaceShadowsTooltip);
            ParamCreator.CreateParam("Smooth Scaled Planet Shadows",            ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows, GUIHelperFunctions.BoolField, scaledShadowMaterialCallback, smoothScaledSpaceShadowsTooltip);
            ParamCreator.CreateParam("Raymarched Scaled Shadows Step Count",    ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount, GUIHelperFunctions.IntField, scaledShadowMaterialCallback, scaledRaymarchedShadowStepCountTooltip);
            ParamCreator.CreateParam("Load Scaled Textures Immediately",        ref ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.loadTexturesImmediately, GUIHelperFunctions.BoolField, scaledTextureCallback, loadTexturesImmediatelyTooltip);

            // Prevent some stupid settings
            if (ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount > 256)
            {
                ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount = 256;
            }

            GUILayout.Space(15);
            // Save button
            if (GUILayout.Button("Save Changes", activeButton))
            {
                ConfigLoader.parallaxGlobalSettings.SaveSettings();
            }

            ///////////////////////////
            GUILayout.EndVertical();

            // Store it so we can set show it in the tooltip window
            tooltip = GUI.tooltip;

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

            // check current pause state
            bool isGamePaused = FlightDriver.paused;
            
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

            // Restore previous pause state
            FlightDriver.SetPause(isGamePaused);
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

        static void UpdateScaledShadowMaterialSettings()
        {
            // Toggle smooth shadows
            foreach (ParallaxScaledBody scaledBody in ConfigLoader.parallaxScaledBodies.Values)
            {
                if (scaledBody.shadowCasterMaterial != null)
                {
                    if (!scaledBody.shadowCasterMaterial.IsKeywordEnabled("BLUE_NOISE") && ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows)
                    {
                        scaledBody.shadowCasterMaterial.EnableKeyword("BLUE_NOISE");
                    }
                    else if (scaledBody.shadowCasterMaterial.IsKeywordEnabled("BLUE_NOISE") && !ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows)
                    {
                        scaledBody.shadowCasterMaterial.DisableKeyword("BLUE_NOISE");
                    }
                }
            }

            Shader.SetGlobalInt("_ParallaxScaledShadowStepSize", ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowStepCount);
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
