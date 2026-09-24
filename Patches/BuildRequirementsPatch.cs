using HarmonyLib;
using System.Collections.Generic;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class BuildRequirementsPatch
    {
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateKnownRecipesList))]
        [HarmonyPrefix]
        private static void UpdateKnownRecipesListPrefix()
        {
            BuildCraftService.RebuildingRecipeList = true;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateKnownRecipesList))]
        [HarmonyPostfix]
        private static void UpdateKnownRecipesListPostfix()
        {
            BuildCraftService.RebuildingRecipeList = false;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        [HarmonyPostfix]
        private static void HaveRequirementsPostfix(Player __instance, ref bool __result, Piece piece, Player.RequirementMode mode, HashSet<string> ___m_knownMaterial, Dictionary<string, int> ___m_knownStations)
        {
            if (__result || BuildCraftService.RebuildingRecipeList || __instance == null || piece == null)
            {
                return;
            }

            __result = BuildCraftService.HaveBuildRequirements(__instance, piece, mode, ___m_knownMaterial, ___m_knownStations);
        }
    }
}
