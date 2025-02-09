using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class debugger : MonoBehaviour
    {
        //void Update()
        //{
        //    CelestialBody kerbin = FlightGlobals.GetBodyByName("Kerbin");
        //
        //    if (kerbin != null)
        //    {
        //        Debug.Log("Kerbin scaled material: " + kerbin.scaledBody.GetComponent<MeshRenderer>().sharedMaterial.name);
        //        Debug.Log("Kerbin scaled material shader: " + kerbin.scaledBody.GetComponent<MeshRenderer>().sharedMaterial.shader);
        //    }
        //
        //    CelestialBody mun = FlightGlobals.GetBodyByName("Mun");
        //    if (mun != null)
        //    {
        //        Debug.Log("mun scaled material: " + mun.scaledBody.GetComponent<MeshRenderer>().sharedMaterial.name);
        //        Debug.Log("mun scaled material shader: " + mun.scaledBody.GetComponent<MeshRenderer>().sharedMaterial.shader);
        //    }
        //}
    }
    [HarmonyPatch]
    internal class PatchScanSat
    {
        public enum mapSource
        {
            Data = 0,
            BigMap = 1,
            ZoomMap = 2,
            RPM = 3,
            Overlay = 4,
        }

        internal static readonly System.Type _type = AccessTools.TypeByName("SCANsat.SCANcontroller");
        internal static readonly System.Type settingsType = AccessTools.TypeByName("SCANsat.SCAN_Settings_Config");
        internal static readonly System.Type mapSourceType = AccessTools.TypeByName("SCANsat.SCAN_Map.mapSource");
        internal static MethodBase TargetMethod() => AccessTools.Method(_type, "LoadVisualMapTexture", new Type[] { typeof(CelestialBody), mapSourceType });

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return _type != null;
        }

        // Patch scansat reading from .material instead of .sharedmaterial, and might as well point it to the parallax scaled textures too while we're at it
        [HarmonyPrefix]
        internal static bool Prefix_LoadVisualMapTexture(object __instance, CelestialBody b, mapSource s)
        {
            if (b == null || b.scaledBody == null)
                return false;

            Type scanControllerRuntimeType = __instance.GetType();

            // Keeping these here in case they're useful later
            var instanceProp = AccessTools.Property(settingsType, "Instance");
            var visibleMapsActiveField = AccessTools.Property(settingsType, "VisibleMapsActive");

            var readableScaledSpaceMapsField = AccessTools.Field(_type, "readableScaledSpaceMaps");
            var readableScaledSpaceNormalMapsField = AccessTools.Field(_type, "readableScaledSpaceNormalMaps");
            var readableTextureMethod = AccessTools.Method(_type, "readableTexture");
            var bigMapBodyField = AccessTools.Field(_type, "bigMapBodyScaledSpace");
            var zoomMapBodyField = AccessTools.Field(_type, "zoomMapBodyScaledSpace");

            //object settingsInstance = instanceProp.GetValue(null);

            Debug.Log("Scansat requesting a texture load for " + b.name);

            // Skip checking visible maps active, it kept returning null. Should be fine, we're just forcing a load instead

            MeshRenderer scaledMesh = b.scaledBody.GetComponent<MeshRenderer>();
            if (scaledMesh == null)
                return false;

            var readableScaledSpaceMaps = (IDictionary)readableScaledSpaceMapsField.GetValue(__instance);
            var readableScaledSpaceNormalMaps = (IDictionary)readableScaledSpaceNormalMapsField.GetValue(__instance);

            bool isScaled = false;
            ParallaxScaledBody body = null;
            if (scaledMesh.sharedMaterial.shader.name.Contains("ParallaxScaled"))
            {
                Debug.Log("Loading scaled planet: " + b.name);
                isScaled = true;
                body = ConfigLoader.parallaxScaledBodies[b.name];
                body.Load();
            }

            if (!readableScaledSpaceMaps.Contains(b) || readableScaledSpaceMaps[b] == null)
            {
                string shaderName = scaledMesh.sharedMaterial.shader.name;
                Texture mainTex = null;

                if (shaderName == "Terrain/Gas Giant" && scaledMesh.sharedMaterial.HasProperty("_DetailCloudPatternTexture"))
                {
                    mainTex = scaledMesh.sharedMaterial.GetTexture("_DetailCloudPatternTexture");
                }
                else
                {
                    if (isScaled)
                    {
                        Debug.Log("Is scaled, getting color map");
                        mainTex = body.scaledMaterial.GetTexture("_ColorMap");
                    }
                    else
                    {
                        mainTex = scaledMesh.sharedMaterial.GetTexture("_MainTex");
                    }
                    
                }

                if (mainTex != null)
                {
                    var readableTexture = readableTextureMethod.Invoke(__instance, new object[] { mainTex, scaledMesh.sharedMaterial, false });
                    readableScaledSpaceMaps[b] = readableTexture;
                }
            }

            if (!readableScaledSpaceNormalMaps.Contains(b) || readableScaledSpaceNormalMaps[b] == null)
            {
                string shaderName = scaledMesh.sharedMaterial.shader.name;
                Texture normalTex = null;

                if (shaderName == "Terrain/Gas Giant" && scaledMesh.sharedMaterial.HasProperty("_NormalMap"))
                {
                    normalTex = scaledMesh.sharedMaterial.GetTexture("_NormalMap");
                }
                else if (scaledMesh.sharedMaterial.HasProperty("_BumpMap"))
                {
                    if (isScaled)
                    {
                        Debug.Log("Is scaled, getting bump map");
                        normalTex = body.scaledMaterial.GetTexture("_BumpMap");
                    }
                    else
                    {
                        normalTex = scaledMesh.sharedMaterial.GetTexture("_BumpMap");
                    }
                }

                if (normalTex != null)
                {
                    var readableTexture = readableTextureMethod.Invoke(__instance, new object[] { normalTex, scaledMesh.sharedMaterial, false });
                    readableScaledSpaceNormalMaps[b] = readableTexture;
                }
            }

            if (s == mapSource.BigMap)
            {
                bigMapBodyField.SetValue(__instance, b);
            }
            else if (s == mapSource.ZoomMap)
            {
                zoomMapBodyField.SetValue(__instance, b);
            }

            return false;
        }
    }
}
