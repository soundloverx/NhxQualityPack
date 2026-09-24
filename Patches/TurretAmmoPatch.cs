using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
    internal static class TurretAmmoPatch
    {
        private static void Prefix(Turret __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = user as Player;

            if (item != null || player == null)
            {
                return;
            }

            Inventory inventory = user.GetInventory();

            if (inventory == null)
            {
                return;
            }

            string currentAmmoType = __instance.GetAmmo() > 0 ? __instance.GetAmmoType() : null;
            item = inventory.GetAmmoItem(__instance.m_ammoType, currentAmmoType);

            if (item != null)
            {
                return;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(__instance.GetAmmoType());
            ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;

            if (itemDrop == null)
            {
                return;
            }

            string ammoName = itemDrop.m_itemData.m_shared.m_name;
            List<Container> containers = ContainerService.GetNearbyContainers(player);

            if (ContainerItemService.TransferItem(containers, inventory, ammoName, 1) <= 0)
            {
                return;
            }

            item = inventory.GetAmmoItem(__instance.m_ammoType, currentAmmoType);
        }
    }
}
