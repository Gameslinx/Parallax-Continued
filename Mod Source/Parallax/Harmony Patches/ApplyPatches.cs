using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ParallaxHarmonyPatcher : MonoBehaviour
    {
        public void Start()
        {
            ParallaxDebug.Log("Starting Harmony patching...");
            var harmony = new Harmony("Parallax");
            harmony.PatchAll();
            ParallaxDebug.Log("Harmony patching complete");
        }
    }
}
