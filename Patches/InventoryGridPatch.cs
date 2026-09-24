using HarmonyLib;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class InventoryGridPatch
    {
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem))]
        [HarmonyPrefix]
        private static bool OnRightClickItemPrefix(InventoryGrid grid, ItemDrop.ItemData item)
        {
            Player player = Player.m_localPlayer;

            if (player == null || grid == null || item == null)
            {
                return true;
            }

            if (grid.GetInventory() != player.GetInventory())
            {
                return true;
            }

            if (!ZInput.GetKey(KeyCode.LeftAlt, true) && !ZInput.GetKey(KeyCode.RightAlt, true))
            {
                return true;
            }

            if (!LockService.IsVanillaInventoryItem(player, item))
            {
                return true;
            }

            LockService.ToggleLocked(player, item);
            UI.LockBorderRenderer.Refresh(grid);

            return false;
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        [HarmonyPostfix]
        private static void UpdateGuiPostfix(InventoryGrid __instance)
        {
            UI.LockBorderRenderer.Refresh(__instance);
        }
    }
}
