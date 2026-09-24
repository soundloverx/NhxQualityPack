using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class CookingStationSupplyPatch
    {
        // Only fuel is pulled from containers here. Ingredients being cooked are deliberately left to
        // vanilla: OnInteract also collects finished food, so topping up the inventory with a raw item
        // ahead of it handed out a stray raw item every time the food was taken off the station.
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnAddFuelSwitch))]
        [HarmonyPrefix]
        private static bool OnAddFuelSwitchPrefix(CookingStation __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result, ZNetView ___m_nview)
        {
            Player player = user as Player;

            if (item != null || player == null || !__instance.m_useFuel || __instance.m_fuelItem == null)
            {
                return true;
            }

            Inventory inventory = user.GetInventory();
            string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            bool fillAll = InputService.IsHeld(Plugin.FillAllModifierKey);

            if (inventory == null || (InventoryLockService.HaveUnlockedItem(inventory, fuelName) && !fillAll))
            {
                return true;
            }

            // CookingStation.RPC_AddFuel (like Smelter's, unlike Fireplace's) doesn't clamp itself
            // against m_maxFuel, so room is tracked as a raw float and the amounts below always floor,
            // which guarantees the running total can never be pushed past the cap.
            float currentFuel = ___m_nview.GetZDO().GetFloat("fuel", 0f);

            if (currentFuel > __instance.m_maxFuel - 1)
            {
                return true;
            }

            int added = 0;

            if (fillAll && InventoryLockService.HaveUnlockedItem(inventory, fuelName))
            {
                int amount = (int)Mathf.Min(__instance.m_maxFuel - currentFuel, InventoryLockService.CountUnlockedItems(inventory, fuelName));
                inventory.RemoveItem(fuelName, amount);

                for (int i = 0; i < amount; i++)
                {
                    ___m_nview.InvokeRPC("RPC_AddFuel", new object[] { });
                }

                currentFuel += amount;
                added += amount;
            }

            if (currentFuel < __instance.m_maxFuel)
            {
                List<Container> containers = ContainerService.GetNearbyContainers(player);
                int needed = fillAll ? (int)(__instance.m_maxFuel - currentFuel) : 1;
                int fromContainers = ContainerItemService.RemoveItem(containers, fuelName, needed);

                for (int i = 0; i < fromContainers; i++)
                {
                    ___m_nview.InvokeRPC("RPC_AddFuel", new object[] { });
                }

                added += fromContainers;
            }

            if (added <= 0)
            {
                InventoryLockService.ShowNoFuelMessage(user, inventory, fuelName);
                __result = false;

                return false;
            }

            user.Message(MessageHud.MessageType.TopLeft, $"$msg_added {added} {fuelName}");
            __result = true;

            return false;
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnHoverFuelSwitch))]
        [HarmonyPostfix]
        private static void OnHoverFuelSwitchPostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result += Localization.instance.Localize($"\n[<color=yellow><b>{Plugin.FillAllModifierKey}+$KEY_Use</b></color>] fill up");
        }
    }
}
