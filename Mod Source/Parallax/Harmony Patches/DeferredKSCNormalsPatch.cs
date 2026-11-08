using HarmonyLib;
using System;
using System.Reflection;

namespace Parallax.Harmony_Patches
{
    // Deferred recalculates the KSC ground normals and only bends the tarmac back upwards, leaving the skirts tilted into the terrain
    // Parallax shades the KSC ground itself so it needs the stock upward skirt normals
    [HarmonyPatch]
    internal class DeferredKSCNormalsPatch
    {
        static readonly Type normalsFixerType = ModTypes.Find("Deferred", "Deferred.KSCModelNormalsFixer");

        [HarmonyPrepare]
        static bool Prepare()
        {
            return normalsFixerType != null;
        }

        static MethodBase TargetMethod() => AccessTools.Method(normalsFixerType, "FixKSCModels");

        [HarmonyPrefix]
        static bool Prefix()
        {
            // The KSC and island airfield are on the home body, and without Parallax there they keep Deferred's lighting fix
            CelestialBody homeBody = FlightGlobals.GetHomeBody();
            if (homeBody == null || !ConfigLoader.parallaxTerrainBodies.ContainsKey(homeBody.name))
            {
                return true;
            }

            ParallaxDebug.Log("Skipping Deferred's KSC normal adjustments because " + homeBody.name + " has Parallax terrain");
            return false;
        }
    }
}
