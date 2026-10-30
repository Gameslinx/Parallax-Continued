using HarmonyLib;
using Parallax.Structures;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    // Kerbal Konstructs swaps its statics' grass onto its own shaders when each static first activates
    // This hands the static to the structure ground replacement after
    [HarmonyPatch]
    internal class KerbalKonstructsGrasColorPatch
    {
        static readonly Type grasColorType = AccessTools.TypeByName("KerbalKonstructs.GrasColor");

        [HarmonyPrepare]
        static bool Prepare()
        {
            return grasColorType != null;
        }

        static MethodBase TargetMethod() => AccessTools.Method(grasColorType, "FindModelGrasMaterials");

        [HarmonyPostfix]
        static void Postfix(Component __instance)
        {
            StructureGroundTerrain.QueueRenderers(__instance);
        }
    }

    [HarmonyPatch]
    internal class KerbalKonstructsGrassColor2Patch
    {
        static readonly Type grassColor2Type = AccessTools.TypeByName("KerbalKonstructs.GrassColor2");

        [HarmonyPrepare]
        static bool Prepare()
        {
            return grassColor2Type != null;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(grassColor2Type, "FindModelGrasMaterials");
            yield return AccessTools.Method(grassColor2Type, "FindSquadGrasMaterial");
        }

        [HarmonyPostfix]
        static void Postfix(Component __instance)
        {
            StructureGroundTerrain.QueueRenderers(__instance);
        }
    }
}
