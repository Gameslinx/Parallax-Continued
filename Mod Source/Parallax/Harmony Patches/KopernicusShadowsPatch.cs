using HarmonyLib;
using Kopernicus.ShadowMan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parallax.Harmony_Patches
{
    // Temporarily disabled - Will come back to this later

    //[HarmonyPatch(typeof(ShadowRemoveFadeCommandBuffer))]
    //[HarmonyPatch("Awake")]
    //public class KopernicusShadowsPatch
    //{
    //    static bool Prefix(ShadowRemoveFadeCommandBuffer __instance)
    //    {
    //        ParallaxDebug.Log("Patching Kopernicus shadows to enable the shadow fade...");
    //        __instance.m_Buffer = new CommandBuffer();
    //        __instance.m_Buffer.name = "ShadowManShadowRemoveFade";
    //        //__instance.m_Buffer.SetGlobalVector(Kopernicus.ShadowMan.ShaderProperties.unity_ShadowFadeCenterAndType_PROPERTY, new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, -1f));
    //        __instance.m_Camera = __instance.GetComponent<Camera>();
    //        __instance.m_Camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, __instance.m_Buffer);
    //        return false;
    //    }
    //}
}
