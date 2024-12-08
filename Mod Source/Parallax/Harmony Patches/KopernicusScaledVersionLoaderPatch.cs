using HarmonyLib;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration;
using Kopernicus.Configuration.Enumerations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    /// <summary>
    /// Prevent KittopiaTech from breaking when trying to open the GUI with Parallax Scaled installed
    /// </summary>
    [HarmonyPatch(typeof(ScaledVersionLoader))]
    [HarmonyPatch("get_Type")]
    public class KopernicusScaledVersionLoaderPatch
    {
        static bool Prefix(ScaledVersionLoader __instance, ref EnumParser<ScaledMaterialType> __result)
        {
            Material material = __instance.Value.scaledBody.GetComponent<Renderer>().sharedMaterial;
            if (material.shader.name.Contains("Parallax"))
            {
                __result = ScaledMaterialType.Vacuum;
                return false;
            }
            return true;
        }
    }
}
