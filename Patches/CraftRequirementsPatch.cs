using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems))]
    internal static class CraftRequirementsPatch
    {
        private static void Postfix(Player __instance, ref bool __result, Recipe piece, bool discover, int qualityLevel, int amount)
        {
            if (__result || discover || __instance == null || piece == null)
            {
                return;
            }

            __result = BuildCraftService.HaveCraftRequirements(__instance, piece, qualityLevel, amount);
        }
    }
}
