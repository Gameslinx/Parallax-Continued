using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Parallax.PQSStartPatch;

namespace Parallax
{
    // PQS StartSphere() is called for every planet at the start of every scene. The planet that actually builds is called with force = true (and can be multiple times!)
    // We need a patch here to add an event that fires when the planet we are about to build is the planet we are actually on
    // This condition is met when force is true, and force is true on the dominant body

    // This avoids loading everything for every planet at the main menu, which defeats the purpose of on demand
    [HarmonyPatch(typeof(PQS))]
    [HarmonyPatch("UpdateQuadsInit")]
    public class PQSStartPatch
    {
        public static string currentLoadedBody = "ParallaxFirstRunDoNotCallAPlanetThis";

        public delegate void PQSStart(string bodyName);
        public delegate void PQSUnload(string bodyName);

        /// <summary>
        /// Called when the planet currently loading is just about to start building terrain. Use this for functions you need to run before the quads are built.
        /// NOT called if a non-parallax body was loading/unloading. NOT called if the body did not change, but the scene did (quicksave, quickload for ex)
        /// </summary>
        public static event PQSStart onPQSStart;
        public static event PQSUnload onPQSUnload;
        static bool Prefix(PQS __instance)
        {
            Debug.Log("Update Quads Init: " + __instance.name);
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(__instance.name))
            {
                Debug.Log(" - Invoking events for: " + __instance.name);
                if (currentLoadedBody == __instance.name)
                {
                    return true;
                }
                onPQSUnload?.Invoke(currentLoadedBody);
                onPQSStart?.Invoke(__instance.name);
                currentLoadedBody = __instance.name;
            }
            return true;
        }
    }
}
