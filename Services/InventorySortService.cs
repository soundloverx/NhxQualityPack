using System;
using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class InventorySortService
    {
        internal static void SortPlayerInventory()
        {
            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            Inventory inventory = player.GetInventory();

            if (inventory == null)
            {
                return;
            }

            int vanillaHeight = PlayerInventoryService.GetVanillaHeight(player);

            Sort(inventory, vanillaHeight, true, true);
        }

        internal static void SortContainer(Container container)
        {
            if (container == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            if (!ContainerService.IsAccessibleContainer(container))
            {
                return;
            }

            Inventory inventory = container.GetInventory();

            if (inventory == null)
            {
                return;
            }

            if (!ContainerService.EnsureOwnership(container))
            {
                return;
            }

            Sort(inventory, inventory.GetHeight(), false, false);
        }

        private static void Sort(Inventory inventory, int validHeight, bool preserveHotbar, bool respectLocks)
        {
            List<ItemDrop.ItemData> allItems = inventory.GetAllItems();

            if (allItems == null || allItems.Count <= 1)
            {
                return;
            }

            int width = inventory.GetWidth();

            if (width <= 0 || validHeight <= 0)
            {
                return;
            }

            List<ItemDrop.ItemData> itemsToSort = new List<ItemDrop.ItemData>(allItems.Count);
            HashSet<(int x, int y)> reservedSlots = new HashSet<(int x, int y)>();

            foreach (ItemDrop.ItemData item in allItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (!IsValidSlot(item.m_gridPos, width, validHeight))
                {
                    continue;
                }

                if (item.m_equipped)
                {
                    reservedSlots.Add((item.m_gridPos.x, item.m_gridPos.y));
                    continue;
                }

                if (preserveHotbar && item.m_gridPos.y == 0)
                {
                    reservedSlots.Add((item.m_gridPos.x, item.m_gridPos.y));
                    continue;
                }

                if (respectLocks && LockService.IsLocked(item))
                {
                    reservedSlots.Add((item.m_gridPos.x, item.m_gridPos.y));
                    continue;
                }

                itemsToSort.Add(item);
            }

            MergeStacks(inventory, itemsToSort);

            itemsToSort.Sort(CompareItems);

            int itemIndex = 0;

            for (int y = preserveHotbar ? 1 : 0; y < validHeight && itemIndex < itemsToSort.Count; y++)
            {
                for (int x = 0; x < width && itemIndex < itemsToSort.Count; x++)
                {
                    if (reservedSlots.Contains((x, y)))
                    {
                        continue;
                    }

                    ItemDrop.ItemData item = itemsToSort[itemIndex];
                    item.m_gridPos = new Vector2i(x, y);
                    itemIndex++;
                }
            }

            ContainerService.NotifyInventoryChanged(inventory);
        }

        private static void MergeStacks(Inventory inventory, List<ItemDrop.ItemData> itemsToSort)
        {
            if (itemsToSort.Count <= 1)
            {
                return;
            }

            Dictionary<(string name, int quality), List<ItemDrop.ItemData>> groups = new Dictionary<(string, int), List<ItemDrop.ItemData>>();

            foreach (ItemDrop.ItemData item in itemsToSort)
            {
                if (item.m_shared.m_maxStackSize <= 1)
                {
                    continue;
                }

                (string name, int quality) key = (item.m_shared.m_name, item.m_quality);

                if (!groups.TryGetValue(key, out List<ItemDrop.ItemData> group))
                {
                    group = new List<ItemDrop.ItemData>();
                    groups[key] = group;
                }

                group.Add(item);
            }

            foreach (List<ItemDrop.ItemData> group in groups.Values)
            {
                if (group.Count <= 1)
                {
                    continue;
                }

                int maxStackSize = group[0].m_shared.m_maxStackSize;
                int targetIndex = 0;

                for (int sourceIndex = 1; sourceIndex < group.Count; sourceIndex++)
                {
                    ItemDrop.ItemData source = group[sourceIndex];

                    while (source.m_stack > 0 && targetIndex < sourceIndex)
                    {
                        ItemDrop.ItemData target = group[targetIndex];
                        int room = maxStackSize - target.m_stack;

                        if (room <= 0)
                        {
                            targetIndex++;
                            continue;
                        }

                        int amount = Mathf.Min(room, source.m_stack);
                        target.m_stack += amount;
                        source.m_stack -= amount;
                    }
                }
            }

            for (int i = itemsToSort.Count - 1; i >= 0; i--)
            {
                if (itemsToSort[i].m_stack <= 0)
                {
                    inventory.RemoveItem(itemsToSort[i]);
                    itemsToSort.RemoveAt(i);
                }
            }
        }

        private static bool IsValidSlot(Vector2i position, int width, int height)
        {
            return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
        }

        private static int CompareItems(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            int result = a.m_shared.m_itemType.CompareTo(b.m_shared.m_itemType);

            if (result != 0)
            {
                return result;
            }

            result = string.Compare(a.m_shared.m_name, b.m_shared.m_name, StringComparison.Ordinal);

            if (result != 0)
            {
                return result;
            }

            result = b.m_quality.CompareTo(a.m_quality);

            if (result != 0)
            {
                return result;
            }

            return b.m_stack.CompareTo(a.m_stack);
        }
    }
}
