using Kopernicus.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Parallax.Legacy.LegacyScatterConfigLoader;

namespace Parallax.Legacy
{
    public class LegacyTerrainConfigLoader
    {
        public class ParallaxBodies
        {
            public static Dictionary<string, ParallaxBody> parallaxBodies = new Dictionary<string, ParallaxBody>();
            //public static List<ParallaxAsteroidBody> parallaxAsteroidBodies = new List<ParallaxAsteroidBody>();
        }

        [KSPAddon(KSPAddon.Startup.MainMenu, true)]
        public class LegacyUpgradeNotifier : MonoBehaviour
        {
            void Start()
            {
                if (ParallaxBodies.parallaxBodies.Count > 0 || ScatterBodies.scatterBodies.Count > 0)
                {
                    PopupDialog dialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Parallax", "Parallax",
                                      "Old Parallax configs detected! They have been upgraded and exported to GameData/Parallax/Exports/LegacyUpgrades. They won't work until manually activated.", "Understood", true, HighLogic.UISkin);
                }
            }
        }

        [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
        public class Loader : MonoBehaviour
        {
            public static UrlDir.UrlConfig[] globalNodes;
            public static UrlDir.UrlConfig[] globalPlanetManagerNodes;
            public static void ModuleManagerPostLoad()
            {
                globalPlanetManagerNodes = FetchGlobalNodes("ParallaxBodyManager");
                globalNodes = FetchGlobalNodes("Parallax");
                DetermineParallaxType();
                LoadAllNodes();

                ConfigNode baseNode = new ConfigNode("ParallaxTerrain-UPGRADED");
                ConfigNode result = baseNode.AddNode("ParallaxTerrain-UPGRADED");

                // Now convert these and upgrade the configs
                foreach (KeyValuePair<string, ParallaxBody> body in ParallaxBodies.parallaxBodies)
                {
                    ConfigNode bodyNode = result.AddNode("Body");

                    bodyNode.AddValue("name", body.Key, "To activate this config, replace ParallaxTerrain-UPGRADED with ParallaxTerrain");
                    bodyNode.AddValue("emissive", body.Value.hasEmission);

                    // Creates 'ShaderProperties' node
                    bodyNode.AddNode(body.Value.ToUpgradedTerrainNode());
                    
                }
                baseNode.Save(KSPUtil.ApplicationRootPath + "GameData/Parallax/Exports/LegacyUpgrades/_Terrain.cfg");
            }
            public static void DetermineParallaxType()
            {
                int terrainShaderQuality = GameSettings.TERRAIN_SHADER_QUALITY;
                for (int i = 0; i < globalPlanetManagerNodes.Length; i++)   //ParallaxBodyManager
                {
                    ConfigNode[] nodes = globalPlanetManagerNodes[i].config.GetNodes("Planet");
                    for (int b = 0; b < nodes.Length; b++)  //For every planet, determine quality and assign appropriate variables
                    {
                        string name = nodes[b].GetValue("name");
                        ParallaxBody body = new ParallaxBody(name, terrainShaderQuality);
                        //body.shaderVars = Parallax.DetermineVersion(false, false, GameSettings.TERRAIN_SHADER_QUALITY);
                        ParallaxBodies.parallaxBodies.Add(name, body);
                    }
                }
            }
            public static UrlDir.UrlConfig[] FetchGlobalNodes(string configName)
            {
                return GameDatabase.Instance.GetConfigs(configName);
            }
            public static void LoadAllNodes()
            {
                for (int i = 0; i < globalNodes.Length; i++)
                {
                    for (int b = 0; b < globalNodes[i].config.nodes.Count; b++)
                    {
                        ConfigNode rootNode = globalNodes[i].config;
                        string bodyName = rootNode.nodes[b].GetValue("name");
                        ParallaxLog.Log("Parsing " + bodyName + " on core");
                        ParallaxBody body = new ParallaxBody(bodyName, GameSettings.TERRAIN_SHADER_QUALITY);
                        ParallaxBodies.parallaxBodies.Add(bodyName, body);
                        ConfigNode bodyNode = rootNode.nodes[b].GetNode("Textures");
                        bool emission = bool.Parse(rootNode.nodes[b].GetValue("emissive"));
                        body.hasEmission = emission;
                        ParseNewBody(bodyNode, bodyName);
                    }
                }
            }
            public static void ParseNewBody(ConfigNode node, string name)
            {
                ParallaxBody body = ParallaxBodies.parallaxBodies[name];           //This can be a standard, scaled or part body

                //Iterate through all materials

                foreach (PropertyInfo property in body.GetType().GetProperties()) //get all shader properties
                {
                    ParallaxLog.Log("[Loader] " + body.bodyName + " - Parsing: " + property.Name);
                    if (property.PropertyType != typeof(Parallax))
                    {
                        //Now start setting the variables
                        object materialValue = ParseType(property.Name, node.GetValue(property.Name));
                        PropertyInfo materialType = body.GetType().GetProperty(property.Name);
                        ConvertAndSetType(materialType, materialValue, body);
                    }
                }


            }
            public static void ConvertAndSetType(PropertyInfo property, object value, ParallaxBody body)
            {
                if (property.PropertyType == typeof(Vector2))
                {
                    body.GetType().GetProperty(property.Name).SetValue(body, (Vector2)value);
                }
                if (property.PropertyType == typeof(float))
                {
                    body.GetType().GetProperty(property.Name).SetValue(body, (float)value);
                }
                if (property.PropertyType == typeof(Color))
                {
                    body.GetType().GetProperty(property.Name).SetValue(body, (Color)value);
                }
                if (property.PropertyType == typeof(string))
                {
                    body.GetType().GetProperty(property.Name).SetValue(body, (string)value);
                }
            }
            public static object ParseType(string name, string value)
            {
                //Vector2, string, float, color
                //if (name.Contains("TextureScale"))
                //{
                //    string[] vectorComponents = value.Replace(" ", string.Empty).Split(',');
                //    return new Vector2(float.Parse(vectorComponents[0]), float.Parse(vectorComponents[1]));
                //}
                if (value.Contains("/"))
                {
                    return value;
                }
                if (name == "_MetallicTint" || name == "_EmissionColor" || name == "_FresnelColor")
                {
                    string[] vectorComponents = value.Replace(" ", string.Empty).Split(',');
                    return new Color(float.Parse(vectorComponents[0]), float.Parse(vectorComponents[1]), float.Parse(vectorComponents[2]));
                }
                return float.Parse(value);
            }
        }
    }
}
