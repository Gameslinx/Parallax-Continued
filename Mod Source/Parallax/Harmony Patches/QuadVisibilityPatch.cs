using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    [HarmonyPatch(typeof(PQ))]
    [HarmonyPatch("SetVisible")]
    public class QuadSetVisiblePatch
    {
        /// <summary>
        /// Called when the quad is about to become the most subdivided it can be, and can be rendered.
        /// NOT limited to being inside the view frustum
        /// </summary>
        public delegate void OnQuadVisibleBuilt(PQ quad);
        public delegate void OnQuadVisibleAlreadyBuilt(PQ quad);

        public static event OnQuadVisibleBuilt onQuadVisibleBuilt;
        public static event OnQuadVisibleAlreadyBuilt onQuadVisibleAlreadyBuilt;

        public ScatterComponent scatterComponent;

        static bool Prefix(PQ __instance)
        {
            if (!__instance.isVisible)
            {
                __instance.isVisible = true;
                
                if (__instance.isBuilt)
                {
                    // Quad is already built but now visible - For example going back down a subdivision level
                    ScatterComponent.OnQuadVisible(__instance);
                }
                if (!__instance.isBuilt)
                {
                    __instance.Build();
                    // The quad wasn't previously built so we need to get the array of directions from center
                    ScatterComponent.OnQuadVisibleBuilt(__instance);
                }
                if (__instance.isForcedInvisible)
                {
                    __instance.meshRenderer.enabled = false;
                }
                else
                {
                    __instance.meshRenderer.enabled = true;
                }
                if (__instance.onVisible != null)
                {
                    __instance.onVisible(__instance);
                }
            }
            return false;

        }
    }

    //
    //  Quad Invisible Patch
    //

    [HarmonyPatch(typeof(PQ))]
    [HarmonyPatch("SetInvisible")]
    public class QuadSetInvisiblePatch
    {
        public delegate void OnQuadInvisible(PQ quad);

        static bool Prefix(PQ __instance)
        {
            if (__instance.isVisible)
            {
                __instance.isVisible = false;
                // Quad now invisible
                ScatterComponent.OnQuadInvisible(__instance);
                __instance.meshRenderer.enabled = false;
                if (__instance.onInvisible != null)
                {
                    __instance.onInvisible(__instance);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PQS))]
    [HarmonyPatch("DestroyQuad")]
    public class QuadDestroyPatch
    {
        public delegate void OnQuadDestroyed(PQ quad);
        static bool Prefix(PQ __instance, PQ quad)
        {
            if (quad.isBuilt)
            {
                ScatterComponent.OnQuadDestroyed(quad);
            }
            return true;
        }
    }
}
