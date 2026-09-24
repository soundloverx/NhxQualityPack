using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class ContainerItemService
    {
        internal static int CountItem(List<Container> containers, string itemName)
        {
            int total = 0;

            foreach (Container container in containers)
            {
                Inventory inventory = container?.GetInventory();

                if (inventory != null)
                {
                    total += inventory.CountItems(itemName);
                }
            }

            return total;
        }

        // Destroys up to `amount` of itemName across the containers (used when consuming build/craft
        // requirements or feeding a fireplace, where the item is spent rather than handed to the player).
        internal static int RemoveItem(List<Container> containers, string itemName, int amount)
        {
            int removed = 0;

            foreach (Container container in containers)
            {
                if (removed >= amount)
                {
                    break;
                }

                Inventory inventory = container?.GetInventory();

                if (inventory == null)
                {
                    continue;
                }

                int available = inventory.CountItems(itemName);

                if (available <= 0)
                {
                    continue;
                }

                if (!ContainerService.EnsureOwnership(container))
                {
                    continue;
                }

                int take = Mathf.Min(available, amount - removed);
                inventory.RemoveItem(itemName, take);
                removed += take;

                ContainerService.NotifyInventoryChanged(inventory);
            }

            return removed;
        }

        // Removes exactly one unit of itemName and returns a clone of it (so callers can read fields
        // like m_dropPrefab that a plain name/count removal wouldn't expose), or null if none was found.
        internal static ItemDrop.ItemData FindAndTakeOne(List<Container> containers, string itemName)
        {
            foreach (Container container in containers)
            {
                Inventory inventory = container?.GetInventory();

                if (inventory == null)
                {
                    continue;
                }

                ItemDrop.ItemData item = inventory.GetItem(itemName);

                if (item == null)
                {
                    continue;
                }

                if (!ContainerService.EnsureOwnership(container))
                {
                    continue;
                }

                ItemDrop.ItemData taken = item.Clone();
                taken.m_stack = 1;

                inventory.RemoveItem(item, 1);
                ContainerService.NotifyInventoryChanged(inventory);

                return taken;
            }

            return null;
        }

        // Moves up to `amount` of itemName into targetInventory (used for turret ammo, where the item
        // needs to actually end up in the player's inventory rather than being destroyed in place).
        internal static int TransferItem(List<Container> containers, Inventory targetInventory, string itemName, int amount)
        {
            int transferred = 0;

            foreach (Container container in containers)
            {
                if (transferred >= amount)
                {
                    break;
                }

                Inventory inventory = container?.GetInventory();

                if (inventory == null)
                {
                    continue;
                }

                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inventory.GetAllItems());

                foreach (ItemDrop.ItemData item in items)
                {
                    if (transferred >= amount)
                    {
                        break;
                    }

                    if (item == null || item.m_shared == null || item.m_shared.m_name != itemName)
                    {
                        continue;
                    }

                    int take = Mathf.Min(item.m_stack, amount - transferred);

                    if (!targetInventory.CanAddItem(item, take))
                    {
                        continue;
                    }

                    if (!ContainerService.EnsureOwnership(container))
                    {
                        break;
                    }

                    ItemDrop.ItemData clone = item.Clone();
                    clone.m_stack = take;
                    targetInventory.AddItem(clone);
                    inventory.RemoveItem(item, take);

                    ContainerService.NotifyInventoryChanged(inventory);

                    transferred += take;
                }
            }

            return transferred;
        }
    }
}
