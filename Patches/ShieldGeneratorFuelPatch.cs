using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class ShieldGeneratorFuelPatch
    {
        // ShieldGenerator.GetFuel() (and the OnAddFuel/OnHoverAddFuel switch callbacks patched below) are
        // private in the real game assembly - see SmelterSupplyPatch for why a direct call isn't safe here.
        private static readonly MethodInfo GetFuelMethod = AccessTools.Method(typeof(ShieldGenerator), "GetFuel");

        private static float GetFuel(ShieldGenerator shield)
        {
            return (float)GetFuelMethod.Invoke(shield, null);
        }

        // Unlike the other stations, this one is always handled here rather than deferring single adds to
        // vanilla: the shield accepts several fuel types (all the bone types) and vanilla just grabs the first
        // one the player carries, locked or not.
        [HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")]
        [HarmonyPrefix]
        private static bool OnAddFuelPrefix(ShieldGenerator __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result, ZNetView ___m_nview)
        {
            Player player = user as Player;

            if (item != null || player == null || ___m_nview == null || !___m_nview.IsValid() || __instance.m_fuelItems.Count == 0)
            {
                return true;
            }

            Inventory inventory = user.GetInventory();

            if (inventory == null)
            {
                return true;
            }

            float currentFuel = GetFuel(__instance);

            if (currentFuel > __instance.m_maxFuel - 1)
            {
                return true;
            }

            bool fillAll = InputService.IsHeld(Plugin.FillAllModifierKey);
            int room = fillAll ? (int)(__instance.m_maxFuel - currentFuel) : 1;
            Dictionary<string, int> added = new Dictionary<string, int>();

            // Pass 1: everything the player is carrying, across all accepted fuel types, before any chest is touched.
            foreach (ItemDrop fuelItem in __instance.m_fuelItems)
            {
                if (room <= 0)
                {
                    break;
                }

                string fuelName = fuelItem.m_itemData.m_shared.m_name;
                int amount = Mathf.Min(room, InventoryLockService.CountUnlockedItems(inventory, fuelName));

                if (amount <= 0)
                {
                    continue;
                }

                inventory.RemoveItem(fuelName, amount);
                AddFuel(___m_nview, amount);
                room -= amount;
                AddCount(added, fuelName, amount);
            }

            // Pass 2: top up whatever room is left from nearby chests.
            if (room > 0)
            {
                List<Container> containers = ContainerService.GetNearbyContainers(player);

                foreach (ItemDrop fuelItem in __instance.m_fuelItems)
                {
                    if (room <= 0)
                    {
                        break;
                    }

                    string fuelName = fuelItem.m_itemData.m_shared.m_name;
                    int amount = ContainerItemService.RemoveItem(containers, fuelName, room);

                    AddFuel(___m_nview, amount);
                    room -= amount;
                    AddCount(added, fuelName, amount);
                }
            }

            if (added.Count == 0)
            {
                InventoryLockService.ShowNoFuelMessage(user, inventory, __instance.m_fuelItems.ConvertAll(fuelItem => fuelItem.m_itemData.m_shared.m_name).ToArray());
                __result = false;

                return false;
            }

            List<string> messages = new List<string>();

            foreach (KeyValuePair<string, int> entry in added)
            {
                messages.Add($"$msg_added {entry.Value} {entry.Key}");
            }

            user.Message(MessageHud.MessageType.Center, string.Join("\n", messages));
            __result = true;

            return false;
        }

        [HarmonyPatch(typeof(ShieldGenerator), "OnHoverAddFuel")]
        [HarmonyPostfix]
        private static void OnHoverAddFuelPostfix(ShieldGenerator __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || __instance.m_fuelItems.Count == 0)
            {
                return;
            }

            __result += Localization.instance.Localize($"\n[<color=yellow><b>{Plugin.FillAllModifierKey}+$KEY_Use</b></color>] fill up");
        }

        private static void AddFuel(ZNetView nview, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                nview.InvokeRPC("RPC_AddFuel", new object[] { });
            }
        }

        private static void AddCount(Dictionary<string, int> counts, string key, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            counts.TryGetValue(key, out int existing);
            counts[key] = existing + amount;
        }
    }
}
