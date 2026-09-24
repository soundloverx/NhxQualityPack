namespace NhxQualityPack
{
    // Station auto-supply features (smelter ore/fuel, fireplace fuel, cooking station fuel) should never
    // spend a locked item type out of the player's own inventory, but a locked type must still be pullable
    // from nearby containers - a chest full of the same material isn't what the player locked. Building
    // and manual crafting are deliberately exempt: those are expected to use locked inventory items.
    internal static class InventoryLockService
    {
        internal static bool HaveUnlockedItem(Inventory inventory, string itemName)
        {
            return !LockService.IsLocked(itemName) && inventory.HaveItem(itemName);
        }

        internal static int CountUnlockedItems(Inventory inventory, string itemName)
        {
            return LockService.IsLocked(itemName) ? 0 : inventory.CountItems(itemName);
        }

        // Shown by the station fuel patches when nothing could be added, in place of vanilla's own fallback
        // (which doesn't know about locks and would spend a locked stack).
        internal static void ShowNoFuelMessage(Humanoid user, Inventory inventory, params string[] fuelNames)
        {
            foreach (string fuelName in fuelNames)
            {
                if (LockService.IsLocked(fuelName) && inventory.HaveItem(fuelName))
                {
                    user.Message(MessageHud.MessageType.Center, "Inventory items locked");
                    return;
                }
            }

            user.Message(MessageHud.MessageType.Center, "Unable to find fuel");
        }
    }
}
