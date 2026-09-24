using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    // Vanilla SetupRequirement only ever displays the required amount (e.g. "4"), never what the
    // player actually has. This shows "have/required" instead, with "have" being inventory + nearby
    // containers combined, so a requirement never looks unmet just because it's covered by storage.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class InventoryGuiRequirementPatch
    {
        private static readonly Color SuppliedColor = new Color(1f, 0.85f, 0.3f);

        private static void Postfix(bool __result, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier)
        {
            if (!__result || req == null || req.m_resItem == null || player == null || elementRoot == null)
            {
                return;
            }

            Inventory playerInventory = player.GetInventory();

            if (playerInventory == null)
            {
                return;
            }

            string itemName = req.m_resItem.m_itemData.m_shared.m_name;
            int required = req.GetAmount(quality) * craftMultiplier;

            if (required <= 0)
            {
                return;
            }

            int invAmount = playerInventory.CountItems(itemName);
            List<Container> containers = ContainerService.GetNearbyContainers(player);
            int combined = invAmount + ContainerItemService.CountItem(containers, itemName);

            Transform amountTransform = elementRoot.Find("res_amount");
            TMP_Text text = amountTransform != null ? amountTransform.GetComponent<TMP_Text>() : null;

            if (text == null)
            {
                return;
            }

            text.text = $"{combined}/{required}";

            bool costEnforced = craft
                ? !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)
                : !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBuildCost);

            if (combined < required && costEnforced)
            {
                // still short even counting storage - keep vanilla's flashing red warning
                text.color = Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.white;
            }
            else if (invAmount < required)
            {
                // only met because of nearby containers - call that out
                text.color = SuppliedColor;
            }
            else
            {
                text.color = Color.white;
            }
        }
    }
}
