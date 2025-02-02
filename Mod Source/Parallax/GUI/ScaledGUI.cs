using Kopernicus.Configuration;
using Parallax.Scaled_System;
using Parallax.Tools;
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
        static bool showExporter = false;

        static float currentMinAltitude = -100000;
        static float currentMaxAltitude = -100000;
        static string currentMinMaxBody = "";
        static void ScaledMenu(ParallaxScaledBody body)
        {
            GUILayout.Label("Scaled Shader Properties ( " + body.planetName + "):", HighLogic.Skin.label);
            GUILayout.Space(15);
            ProcessBaseScaledMaterial(body);
        }
        static void ProcessBaseScaledMaterial(ParallaxScaledBody body)
        {
            ParamCreator.ChangeMethod callback = body.UpdateBaseMaterialParamsFromGUI;
            ProcessGenericMaterialParams(body.scaledMaterialParams, callback, true, body.scaledMaterial, "ParallaxScaledShaderProperties");

            if (GUILayout.Button("Reload"))
            {
                body.Unload();
                body.Load();
            }
        }
        static void TextureExporterMenu()
        {
            if (PlanetariumCamera.fetch.target.type != MapObject.ObjectType.CelestialBody)
            {
                return;
            }

            string planetName = PlanetariumCamera.fetch.target.gameObject.name;
            CelestialBody body = FlightGlobals.GetBodyByName(planetName);

            if (GUILayout.Button("Texture Exporter", GetButtonColor(showExporter)))
            {
                showExporter = !showExporter;
            }
            if (showExporter)
            {
                // Options
                GUILayout.Label("Planet Texture Exporter Options:", HighLogic.Skin.label);

                ParamCreator.CreateParam("Resolution", ref exportOptions.horizontalResolution, GUIHelperFunctions.IntField, null);
                GUILayout.Space(10);
                ParamCreator.CreateParam("Export Height", ref exportOptions.exportHeight, GUIHelperFunctions.BoolField, null);
                ParamCreator.CreateParam("Export Color", ref exportOptions.exportColor, GUIHelperFunctions.BoolField, null);
                ParamCreator.CreateParam("Export Normal", ref exportOptions.exportNormal, GUIHelperFunctions.BoolField, null);
                GUILayout.Space(10);
                ParamCreator.CreateParam("Multithreaded Export", ref exportOptions.multithread, GUIHelperFunctions.BoolField, null);
                GUILayout.Space(10);
                if (currentMinMaxBody != planetName)
                {
                    GUILayout.Label("To see min/max altitudes to use in configs, export the planet textures first");
                }
                else
                {
                    GUILayout.Label("Min Altitude: " + currentMinAltitude.ToString("F2"));
                    GUILayout.Label("Max Altitude: " + currentMaxAltitude.ToString("F2"));
                }
                
                GUILayout.Space(10);
                if (GUILayout.Button("Export", HighLogic.Skin.button))
                {
                    Coroutine co = ScaledManager.Instance.StartCoroutine(TextureExporter.GenerateTextures(exportOptions, body));
                }
                if (GUILayout.Button("Export Entire System", HighLogic.Skin.button))
                {
                    GenerateEntireSystem();
                }
            }
        }
        public static void SetMinMaxAltitudeLabels(string body, float min, float max)
        {
            currentMinMaxBody = body;
            currentMinAltitude = min;
            currentMaxAltitude = max;
        }
        public static void GenerateEntireSystem()
        {
            Coroutine co = ScaledManager.Instance.StartCoroutine(TextureExporter.GenerateTextures(exportOptions, FlightGlobals.Bodies.ToArray()));
        }
    }
}
