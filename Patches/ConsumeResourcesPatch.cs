using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class ConsumeResourcesPatch
    {
        // Harmony still runs every prefix after one returns false, so if another mod that pulls from
        // nearby containers already paid for this build/craft and skipped vanilla, paying again here
        // would consume the materials twice. Running late (low priority) keeps us behind such mods.
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(Player __instance, bool __runOriginal, Piece.Requirement[] requirements, int qualityLevel, int multiplier)
        {
            if (!__runOriginal)
            {
                return false;
            }

            BuildCraftService.ConsumeRequirements(__instance, requirements, qualityLevel, multiplier);

            return false;
        }
    }
}
