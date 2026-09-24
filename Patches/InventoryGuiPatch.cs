using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class InventoryGuiPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            InventoryUiController.Initialize(__instance);
        }
    }
}
