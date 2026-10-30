using HarmonyLib;
using Parallax.Structures;

namespace Parallax.Harmony_Patches
{
    // Highlighting a KSC facility replaces the whole property block on its renderers, which removes the structure ground properties
    [HarmonyPatch(typeof(SpaceCenterBuilding), nameof(SpaceCenterBuilding.HighLightBuilding))]
    internal class SpaceCenterBuildingHighlightPatch
    {
        [HarmonyPostfix]
        static void Postfix(SpaceCenterBuilding __instance)
        {
            if (__instance.buildingRenderers != null)
            {
                StructureGroundTerrain.ReapplyRendererProperties(__instance.buildingRenderers);
            }
        }
    }
}
