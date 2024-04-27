using System;
using System.Collections.Generic;
using System.Linq;
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
            InitializeTemplateConfigs(GetConfigByName("ParallaxShaderProperties"));
            LoadConfigs(GetConfigsByName("Parallax"));

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
        // Template configs tell Parallax what variable names and type are supported by the shader
        public static void InitializeTemplateConfigs(UrlDir.UrlConfig config)
        {
            ConfigNode.ConfigNodeList nodes = config.config.nodes;
            ConfigNode texturesNode = nodes.GetNode("Textures");
            ConfigNode floatsNode = nodes.GetNode("Floats");
            ConfigNode vectorsNode = nodes.GetNode("Vectors");
            ConfigNode colorsNode = nodes.GetNode("Colors");
            
            string[] texturesNames = texturesNode.GetValues("name");
            string[] floatsNames = floatsNode.GetValues("name");
            string[] vectorsNames = vectorsNode.GetValues("name");
            string[] colorsNames = colorsNode.GetValues("name");

            // Add template names
            foreach (string value in texturesNames)
            {
                shaderPropertiesTemplate.shaderTextures.Add(value, "");
            }
            foreach (string value in floatsNames)
            {
                shaderPropertiesTemplate.shaderFloats.Add(value, 0);
            }
            foreach (string value in vectorsNames)
            {
                shaderPropertiesTemplate.shaderVectors.Add(value, Vector3.zero);
            }
            foreach (string value in colorsNames)
            {
                shaderPropertiesTemplate.shaderColors.Add(value, Color.black);
            }
        }
        public static void LoadConfigs(UrlDir.UrlConfig[] allRootNodes)
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

            // For debugging
            parallaxScatterBodies.Add("Kerbin", new ParallaxScatterBody("Kerbin"));
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
        }
        void OnDestroy()
        {

        }
    }
}
