using HarmonyLib;
using Kopernicus;
using Parallax.LateCompatibility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parallax.Harmony_Patches
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
        public delegate void PQSRestart(string bodyName);

        /// <summary>
        /// Called when the planet currently loading is just about to start building terrain. Use this for functions you need to run before the quads are built.
        /// NOT called if a non-parallax body was loading/unloading. NOT called if the body did not change, but the scene did (quicksave, quickload for ex)
        /// </summary>
        public static event PQSStart onPQSStart;
        public static event PQSUnload onPQSUnload;
        public static event PQSRestart onPQSRestart;
        static bool Prefix(PQS __instance)
        {
            Debug.Log("Update Quads Init: " + __instance.name);
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(__instance.name))
            {
                Debug.Log(" - Invoking events for: " + __instance.name);
                if (currentLoadedBody == __instance.name)
                {
                    onPQSRestart?.Invoke(__instance.name);
                    return true;
                }
                onPQSUnload?.Invoke(currentLoadedBody);
                onPQSStart?.Invoke(__instance.name);
                currentLoadedBody = __instance.name;
                UrlDir.UrlConfig sigDimConfigEntry = ConfigLoader.GetConfigByName("SigmaDimensions");

                if (sigDimConfigEntry == null)
                {
                    return true;
                }
                else
                {
                    int pqsRaiseAmountInteger = 0;
                    //validate that zero with SigDim is correct, correct value if we ran too early
                    CelestialBody cb = FlightGlobals.GetBodyByName(__instance.name);
                    float resizeValue = (float)cb.Get<double>("resize");
                    float pqsRaiseAmountFloat = 0;

                    //If its null we must build it.  This should only ever happen once.
                    if (SigmaDimensions.defaultPresetMaxLevelDictionary == null)
                    {
                        SigmaDimensions.defaultPresetMaxLevelDictionary = new Dictionary<string, int>();
                        foreach (PQSCache.PQSPreset rawPresets in PQSCache.PresetList.presets)
                        {
                            foreach (PQSCache.PQSSpherePreset preset in rawPresets.spherePresets)
                            {
                                if (!SigmaDimensions.defaultPresetMaxLevelDictionary.ContainsKey(preset.name))
                                {
                                    SigmaDimensions.defaultPresetMaxLevelDictionary.Add(preset.name, preset.maxSubdivision);
                                }
                                else if (preset.maxSubdivision > SigmaDimensions.defaultPresetMaxLevelDictionary[preset.name])
                                {
                                    SigmaDimensions.defaultPresetMaxLevelDictionary[preset.name] = preset.maxSubdivision;
                                }
                            }
                        }
                    }
                    if (SigmaDimensions.defaultPresetMaxLevelDictionary != null)
                    {
                        if (resizeValue >= 1)
                        {
                            pqsRaiseAmountFloat = Mathf.Max(Mathf.Log(resizeValue, 2f), 1);
                            pqsRaiseAmountInteger = (int)Mathf.Round(pqsRaiseAmountFloat);
                        }
                        else if (resizeValue < 1)
                        {
                            float resizeValueInverted = (1f / resizeValue);
                            pqsRaiseAmountFloat = Mathf.Log(resizeValueInverted, 2f) * (-1f);
                            pqsRaiseAmountInteger = (int)Mathf.Round(pqsRaiseAmountFloat);
                        }
                        foreach (PQSCache.PQSPreset rawPresets in PQSCache.PresetList.presets)
                        {
                            if (rawPresets.spherePresets[0].maxSubdivision != SigmaDimensions.defaultPresetMaxLevelDictionary[rawPresets.spherePresets[0].name] + pqsRaiseAmountInteger)
                            {
                                foreach (PQSCache.PQSSpherePreset preset in rawPresets.spherePresets)
                                {
                                    preset.maxSubdivision = Math.Max(SigmaDimensions.defaultPresetMaxLevelDictionary[preset.name] + pqsRaiseAmountInteger, 1);
                                }
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
