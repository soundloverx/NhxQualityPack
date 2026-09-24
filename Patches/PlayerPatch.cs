using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class PlayerPatch
    {
        private static void Postfix(Player __instance)
        {
            LockService.MigrateLegacyLocksIfNeeded(__instance);
        }
    }
}
