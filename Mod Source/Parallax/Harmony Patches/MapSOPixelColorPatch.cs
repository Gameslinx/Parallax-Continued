using HarmonyLib;
using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    //[HarmonyPatch(typeof(MapSO))]
    //[HarmonyPatch("GetPixelColor", typeof(int), typeof(int))]
    //public class MapSOPixelColorPatch
    //{
    //    static bool Prefix(MapSO __instance, ref Color __result, object[] __args)
    //    {
    //        if (!TextureExporter.exportRequested)
    //        {
    //            return true;
    //        }
    //
    //        int index = __instance.PixelIndex((int)__args[0], (int)__args[1]);
    //        if (__instance.BitsPerPixel == 3)
    //        {
    //            __result = new Color(MapSO.Byte2Float * (float)__instance._data[index], MapSO.Byte2Float * (float)__instance._data[index + 1], MapSO.Byte2Float * (float)__instance._data[index + 2], 1f);
    //            return false;
    //        }
    //        if (__instance.BitsPerPixel == 4)
    //        {
    //            __result = new Color(MapSO.Byte2Float * (float)__instance._data[index], MapSO.Byte2Float * (float)__instance._data[index + 1], MapSO.Byte2Float * (float)__instance._data[index + 2], MapSO.Byte2Float * (float)__instance._data[index + 3]);
    //            return false;
    //        }
    //        float retVal = 0;
    //        if (__instance.BitsPerPixel == 2)
    //        {
    //            retVal = MapSO.Byte2Float * (float)__instance._data[index];
    //            __result = new Color(retVal, retVal, retVal, MapSO.Byte2Float * (float)__instance._data[index + 1]);
    //            return false;
    //        }
    //        retVal = MapSO.Byte2Float * (float)__instance._data[index];
    //        __result = new Color(retVal, retVal, retVal, 1f);
    //
    //        return false;
    //    }
    //}
    //
    //
    //
    //[HarmonyPatch(typeof(MapSO))]
    //[HarmonyPatch("GetPixelColor", typeof(double), typeof(double))]
    //public class MapSOPixelColorPatchDouble
    //{
    //    public static double[] ConstructBilinearCoords(MapSO map, double x, double y)
    //    {
    //        x = Math.Abs(x - Math.Floor(x));
    //        y = Math.Abs(y - Math.Floor(y));
    //        double centerXD = x * (double)map.Width;
    //
    //        int minX = (int)Math.Floor(centerXD);
    //        int maxX = (int)Math.Ceiling(centerXD);
    //        float midX = (float)(centerXD - (double)minX);
    //        if (maxX == map.Width)
    //        {
    //            maxX = 0;
    //        }
    //        double centerYD = y * (double)map.Height;
    //        int minY = (int)Math.Floor(centerYD);
    //        int maxY = (int)Math.Ceiling(centerYD);
    //        float midY = (float)(centerYD - (double)minY);
    //        if (maxY == map.Height)
    //        {
    //            maxY = 0;
    //        }
    //        return new double[] { minX, maxX, minY, maxY, midX, midY };
    //    }
    //    static bool Prefix(MapSO __instance, ref Color __result, object[] __args)
    //    {
    //        double[] coords = ConstructBilinearCoords(__instance, (double)__args[0], (double)__args[1]);
    //
    //        int minX = (int)coords[0];
    //        int maxX = (int)coords[1];
    //        int minY = (int)coords[2];
    //        int maxY = (int)coords[3];
    //        float midX = (float)coords[4];
    //        float midY = (float)coords[5];
    //
    //        __result = Color.Lerp(Color.Lerp(__instance.GetPixelColor(minX, minY), __instance.GetPixelColor(maxX, minY), midX), Color.Lerp(__instance.GetPixelColor(minX, maxY), __instance.GetPixelColor(maxX, maxY), midX), midY);
    //        return false;
    //    }
    //}
}
