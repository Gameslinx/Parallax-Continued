using HarmonyLib;
using KSP.UI.Screens.DebugToolbar.Screens.Cheats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    /*
     * 
     * The purpose of these stupid patches are to rename instances of 'material' to 'sharedMaterial' when displaying biomes
     * 
     */

    [HarmonyPatch(typeof(DebugToolbar))]
    [HarmonyPatch("SetBiomesVisible", typeof(bool))]
    public class BiomesVisiblePatch
    {
        public static bool Prefix(bool isTrue)
        {
            CheatOptions.BiomesVisible = isTrue;
            for (int i = 0; i < FlightGlobals.fetch.bodies.Count; i++)
            {
                CelestialBody celestialBody = FlightGlobals.fetch.bodies[i];
                GameObject scaledBody = celestialBody.scaledBody;
                if (!(celestialBody == null) && !(scaledBody == null))
                {
                    MeshRenderer component = scaledBody.GetComponent<MeshRenderer>();
                    if (component.sharedMaterial.HasProperty("_ResourceMap"))
                    {
                        Texture2D texture2D = (Texture2D)component.sharedMaterial.GetTexture("_ResourceMap");
                        if (texture2D != null)
                        {
                            UnityEngine.Object.Destroy(texture2D);
                            texture2D = null;
                        }
                        if (isTrue && celestialBody.BiomeMap != null)
                        {
                            texture2D = celestialBody.BiomeMap.CompileToTexture();
                        }
                        component.sharedMaterial.SetTexture("_ResourceMap", texture2D);
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(BiomesVisibleInMap))]
    [HarmonyPatch("SetBiomesVisible", typeof(bool))]
    public class BiomesVisibleInMapPatch
    {
        public static bool Prefix(bool isTrue)
        {
            CheatOptions.BiomesVisible = isTrue;
            for (int i = 0; i < FlightGlobals.fetch.bodies.Count; i++)
            {
                CelestialBody celestialBody = FlightGlobals.fetch.bodies[i];
                GameObject scaledBody = celestialBody.scaledBody;
                if (!(celestialBody == null) && !(scaledBody == null))
                {
                    MeshRenderer component = scaledBody.GetComponent<MeshRenderer>();
                    if (component.sharedMaterial.HasProperty("_ResourceMap"))
                    {
                        Texture2D texture2D = (Texture2D)component.sharedMaterial.GetTexture("_ResourceMap");
                        if (texture2D != null)
                        {
                            UnityEngine.Object.Destroy(texture2D);
                            texture2D = null;
                        }
                        if (isTrue && celestialBody.BiomeMap != null)
                        {
                            texture2D = celestialBody.BiomeMap.CompileToTexture();
                        }
                        component.sharedMaterial.SetTexture("_ResourceMap", texture2D);
                    }
                }
            }
            return false;
        }
    }
}
