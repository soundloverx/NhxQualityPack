using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class SmelterSupplyPatch
    {
        // Smelter.GetQueueSize()/GetFuel() are private in the real game assembly. The local
        // "publicized" reference copy this project compiles against marks every member public for
        // compile-time convenience, so a direct call compiles fine but throws MethodAccessException
        // at runtime once bound against the real assembly the game actually loads - reflection is required.
        private static readonly MethodInfo GetQueueSizeMethod = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo GetFuelMethod = AccessTools.Method(typeof(Smelter), "GetFuel");

        private static int GetQueueSize(Smelter smelter)
        {
            return (int)GetQueueSizeMethod.Invoke(smelter, null);
        }

        private static float GetFuel(Smelter smelter)
        {
            return (float)GetFuelMethod.Invoke(smelter, null);
        }

        // Also covers the battering ram: its engine is a kiln-style Smelter with no fuel switch, where the
        // wood types it burns (wood, fine wood, core wood, blackwood) are fed in as ore.
        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
        [HarmonyPrefix]
        private static bool OnAddOrePrefix(Smelter __instance, Humanoid user, ref ItemDrop.ItemData item, ZNetView ___m_nview)
        {
            Player player = user as Player;

            if (item != null || player == null)
            {
                return true;
            }

            int queueSize = GetQueueSize(__instance);

            if (queueSize >= __instance.m_maxOre)
            {
                return true;
            }

            Inventory inventory = user.GetInventory();

            if (inventory == null)
            {
                return true;
            }

            bool fillAll = InputService.IsHeld(Plugin.FillAllModifierKey);

            if (!fillAll)
            {
                return TryAddSingleOre(__instance, user, ___m_nview, player, inventory, ref item);
            }

            return TryFillAllOre(__instance, user, ___m_nview, player, inventory, queueSize);
        }

        private static bool TryAddSingleOre(Smelter __instance, Humanoid user, ZNetView nview, Player player, Inventory inventory, ref ItemDrop.ItemData item)
        {
            // Pick the inventory item for vanilla instead of letting it search: its own lookup takes the first
            // accepted type found in the inventory, so a locked type listed ahead of an unlocked one got spent.
            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
            {
                string itemName = conversion.m_from.m_itemData.m_shared.m_name;

                if (InventoryLockService.HaveUnlockedItem(inventory, itemName))
                {
                    item = inventory.GetItem(itemName);

                    return true;
                }
            }

            List<Container> containers = ContainerService.GetNearbyContainers(player);

            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
            {
                ItemDrop.ItemData taken = ContainerItemService.FindAndTakeOne(containers, conversion.m_from.m_itemData.m_shared.m_name);

                if (taken == null || taken.m_dropPrefab == null)
                {
                    continue;
                }

                nview.InvokeRPC("RPC_AddOre", new object[] { taken.m_dropPrefab.name, false });
                user.Message(MessageHud.MessageType.TopLeft, $"$msg_added 1 {taken.m_shared.m_name}");

                return false;
            }

            return DeferToVanillaUnlessLocked(__instance, user, inventory);
        }

        private static bool TryFillAllOre(Smelter __instance, Humanoid user, ZNetView nview, Player player, Inventory inventory, int queueSize)
        {
            Dictionary<string, int> added = new Dictionary<string, int>();
            List<Container> containers = null;

            // Pass 1: everything the player is carrying, across all accepted ore types. This must finish
            // before any chest is touched, otherwise an earlier conversion's chest ore can fill the queue
            // while a later conversion's ore is still sitting in the inventory.
            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
            {
                if (queueSize >= __instance.m_maxOre)
                {
                    break;
                }

                string itemName = conversion.m_from.m_itemData.m_shared.m_name;

                if (!InventoryLockService.HaveUnlockedItem(inventory, itemName))
                {
                    continue;
                }

                ItemDrop.ItemData invItem = inventory.GetItem(itemName);

                if (invItem == null || invItem.m_dropPrefab == null)
                {
                    continue;
                }

                int amount = Mathf.Min(__instance.m_maxOre - queueSize, InventoryLockService.CountUnlockedItems(inventory, itemName));
                inventory.RemoveItem(itemName, amount);

                for (int i = 0; i < amount; i++)
                {
                    nview.InvokeRPC("RPC_AddOre", new object[] { invItem.m_dropPrefab.name, false });
                }

                queueSize += amount;
                AddCount(added, itemName, amount);
            }

            // Pass 2: top up whatever room is left from nearby chests.
            foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
            {
                if (queueSize >= __instance.m_maxOre)
                {
                    break;
                }

                string itemName = conversion.m_from.m_itemData.m_shared.m_name;

                if (containers == null)
                {
                    containers = ContainerService.GetNearbyContainers(player);
                }

                while (queueSize < __instance.m_maxOre)
                {
                    ItemDrop.ItemData taken = ContainerItemService.FindAndTakeOne(containers, itemName);

                    if (taken == null || taken.m_dropPrefab == null)
                    {
                        break;
                    }

                    nview.InvokeRPC("RPC_AddOre", new object[] { taken.m_dropPrefab.name, false });
                    queueSize++;
                    AddCount(added, itemName, 1);
                }
            }

            if (added.Count == 0)
            {
                return DeferToVanillaUnlessLocked(__instance, user, inventory);
            }

            List<string> messages = new List<string>();

            foreach (KeyValuePair<string, int> entry in added)
            {
                messages.Add($"$msg_added {entry.Value} {entry.Key}");
            }

            user.Message(MessageHud.MessageType.Center, string.Join("\n", messages));

            return false;
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
        [HarmonyPrefix]
        private static bool OnAddFuelPrefix(Smelter __instance, ref bool __result, ZNetView ___m_nview, Humanoid user, ItemDrop.ItemData item)
        {
            Player player = user as Player;

            if (item != null || player == null || __instance.m_fuelItem == null)
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

            float currentFuel = GetFuel(__instance);

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

        // Belt-and-suspenders: RPC_AddOre/QueueOre don't cap themselves against m_maxOre, so if our own
        // fill-all loop (or interaction with another mod) ever invokes the RPC more times than there's
        // room for, this stops it from writing past the smelter's queue slots.
        [HarmonyPatch(typeof(Smelter), nameof(Smelter.QueueOre))]
        [HarmonyPrefix]
        private static bool QueueOrePrefix(Smelter __instance)
        {
            return GetQueueSize(__instance) < __instance.m_maxOre;
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddOre))]
        [HarmonyPostfix]
        private static void OnHoverAddOrePostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result += Localization.instance.Localize($"\n[<color=yellow><b>{Plugin.FillAllModifierKey}+$KEY_Use</b></color>] fill up");
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddFuel))]
        [HarmonyPostfix]
        private static void OnHoverAddFuelPostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result += Localization.instance.Localize($"\n[<color=yellow><b>{Plugin.FillAllModifierKey}+$KEY_Use</b></color>] fill up");
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

        // Used once nothing could be added from unlocked inventory stacks or nearby chests. Vanilla's own fallback
        // doesn't know about locks and would spend a locked stack we skipped, so it only gets to run (and show its
        // own "nothing to add" message) when no accepted ore type is sitting locked in the inventory.
        private static bool DeferToVanillaUnlessLocked(Smelter smelter, Humanoid user, Inventory inventory)
        {
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (InventoryLockService.HaveLockedItem(inventory, conversion.m_from.m_itemData.m_shared.m_name))
                {
                    InventoryLockService.ShowLockedItemsMessage(user);

                    return false;
                }
            }

            return true;
        }
    }
}
