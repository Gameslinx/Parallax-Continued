using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    // Patch the compute buffer dispose method to throw a proper exception and not just a warning
    // Compute buffer 'name' does not have a getter and its backing field 'namek__BackingField' doesn't exist either, so we can't log the name of the buffer
    [HarmonyPatch(typeof(ComputeBuffer))]
    [HarmonyPatch("Dispose", typeof(bool))]
    public class ComputeBufferPatch
    {
        static bool Prefix(ComputeBuffer __instance, bool disposing)
        {
            // This means it's being GCd and was not disposed properly
            // Do not attempt to actually dispose it. I imagine there's a good reason why Unity doesn't
            if (!disposing)
            {
                Exception e = new Exception("[Parallax Severe Exception] A Compute Buffer was NOT disposed correctly, leading to a potential VRAM leak. A crash is imminent if this continues. PLEASE REPORT THIS");
                Debug.LogException(e);
            }
            // Run original method
            return true;
        }
    }
}
