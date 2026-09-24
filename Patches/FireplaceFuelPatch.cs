using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class FireplaceFuelPatch
    {
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        [HarmonyPrefix]
        private static bool InteractPrefix(Fireplace __instance, Humanoid user, bool hold, bool alt, ref bool __result, ZNetView ___m_nview)
        {
            Player player = user as Player;

            if (hold || player == null || !__instance.m_canRefill || __instance.m_infiniteFuel || __instance.m_fuelItem == null)
            {
                return true;
            }

            float rawFuel = ___m_nview.GetZDO().GetFloat("fuel", 0f);

            if (__instance.m_canTurnOff && !alt && rawFuel > 0f)
            {
                // a plain interact on a lit, toggleable fire turns it off in vanilla - don't hijack that into a refuel
                return true;
            }

            Inventory inventory = user.GetInventory();

            if (inventory == null)
            {
                return true;
            }

            string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            bool fillAll = InputService.IsHeld(Plugin.FillAllModifierKey);

            if (InventoryLockService.HaveUnlockedItem(inventory, fuelName) && !fillAll)
            {
                return true;
            }

            int fuel = Mathf.CeilToInt(rawFuel);

            if (fuel >= __instance.m_maxFuel)
            {
                return true;
            }

            if (!___m_nview.HasOwner())
            {
                ___m_nview.ClaimOwnership();
            }

            int added = 0;

            if (fillAll && InventoryLockService.HaveUnlockedItem(inventory, fuelName))
            {
                int amount = (int)Mathf.Min(__instance.m_maxFuel - fuel, InventoryLockService.CountUnlockedItems(inventory, fuelName));
                inventory.RemoveItem(fuelName, amount);

                for (int i = 0; i < amount; i++)
                {
                    ___m_nview.InvokeRPC("RPC_AddFuel", new object[] { });
                }

                fuel += amount;
                added += amount;
            }

            if (fuel < __instance.m_maxFuel)
            {
                List<Container> containers = ContainerService.GetNearbyContainers(player);
                int needed = fillAll ? (int)(__instance.m_maxFuel - fuel) : 1;
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

            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", new[] { fuelName }));

            __result = false;

            return false;
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
        [HarmonyPostfix]
        private static void GetHoverTextPostfix(Fireplace __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || !__instance.m_canRefill || __instance.m_infiniteFuel || __instance.m_fuelItem == null)
            {
                return;
            }

            __result += Localization.instance.Localize($"\n[<color=yellow><b>{Plugin.FillAllModifierKey}+$KEY_Use</b></color>] fill up");
        }
    }
}
