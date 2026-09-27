using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class EnemyHudPatch
    {
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        [HarmonyPostfix]
        private static void UpdateHudsPostfix(EnemyHud __instance)
        {
            UI.TamingBarRenderer.Refresh(__instance);
        }
    }
}
