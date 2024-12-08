using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    public partial class ParallaxGUI
    {
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
                    GUILayout.Label("Existing config will be backed up to GameData/ParallaxContinued/Exports/Backups");
                }
                else
                {
                    GUILayout.Label("Configs will be exported to GameData/ParallaxContinued/Exports/Configs");
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
                            bool saved = ConfigLoader.SaveConfigNode(currentNode, KSPUtil.ApplicationRootPath + "GameData/ParallaxContinued/Exports/Backups/ParallaxTerrain-" + FlightGlobals.currentMainBody.name + ".cfg");
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
        static void UpdateSubdivision(PQSMod_Parallax pqsMod)
        {
            foreach (TerrainShaderQuadData quadData in PQSMod_Parallax.terrainQuadData.Values)
            {
                quadData.UpdateSubdivision(pqsMod.subdivisionLevel, pqsMod.subdivisionRadius);
            }
        }
    }
}
