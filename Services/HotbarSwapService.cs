using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class HotbarSwapService
    {
        internal static bool SwapHotbarRows(Player player)
        {
            if (player == null)
            {
                return false;
            }

            Inventory inventory = player.GetInventory();

            if (inventory == null || inventory.GetHeight() < 2)
            {
                return false;
            }

            List<ItemDrop.ItemData> allItems = inventory.GetAllItems();

            if (allItems == null)
            {
                return false;
            }

            bool moved = false;

            foreach (ItemDrop.ItemData item in allItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.m_gridPos.y == 0)
                {
                    item.m_gridPos = new Vector2i(item.m_gridPos.x, 1);
                    moved = true;
                }
                else if (item.m_gridPos.y == 1)
                {
                    item.m_gridPos = new Vector2i(item.m_gridPos.x, 0);
                    moved = true;
                }
            }

            if (moved)
            {
                ContainerService.NotifyInventoryChanged(inventory);
            }

            return moved;
        }
    }
}
