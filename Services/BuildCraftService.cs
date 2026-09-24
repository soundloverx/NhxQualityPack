using System.Collections.Generic;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class BuildCraftService
    {
        // Set while Player.UpdateKnownRecipesList is rebuilding its list (which calls HaveRequirements
        // once per known piece) so that pass isn't turned into a container scan per piece.
        internal static bool RebuildingRecipeList;

        internal static bool HaveBuildRequirements(Player player, Piece piece, Player.RequirementMode mode, HashSet<string> knownMaterial, Dictionary<string, int> knownStations)
        {
            if (piece.m_craftingStation != null)
            {
                if (mode == Player.RequirementMode.IsKnown || mode == Player.RequirementMode.CanAlmostBuild)
                {
                    if (knownStations == null || !knownStations.ContainsKey(piece.m_craftingStation.m_name))
                    {
                        return false;
                    }
                }
                else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, player.transform.position)
                    && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench))
                {
                    return false;
                }
            }

            if (piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
            {
                return false;
            }

            Inventory playerInventory = player.GetInventory();

            if (playerInventory == null)
            {
                return false;
            }

            List<Container> containers = null;

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null || requirement.m_amount <= 0)
                {
                    continue;
                }

                string itemName = requirement.m_resItem.m_itemData.m_shared.m_name;

                switch (mode)
                {
                    case Player.RequirementMode.IsKnown:
                        if (knownMaterial == null || !knownMaterial.Contains(itemName))
                        {
                            return false;
                        }
                        break;

                    case Player.RequirementMode.CanAlmostBuild:
                        if (!playerInventory.HaveItem(itemName))
                        {
                            if (containers == null)
                            {
                                containers = ContainerService.GetNearbyContainers(player);
                            }

                            if (ContainerItemService.CountItem(containers, itemName) <= 0)
                            {
                                return false;
                            }
                        }
                        break;

                    case Player.RequirementMode.CanBuild:
                        int have = playerInventory.CountItems(itemName);

                        if (have < requirement.m_amount)
                        {
                            if (containers == null)
                            {
                                containers = ContainerService.GetNearbyContainers(player);
                            }

                            have += ContainerItemService.CountItem(containers, itemName);

                            if (have < requirement.m_amount)
                            {
                                return false;
                            }
                        }
                        break;
                }
            }

            return true;
        }

        internal static bool HaveCraftRequirements(Player player, Recipe recipe, int qualityLevel, int multiplier)
        {
            Inventory playerInventory = player.GetInventory();

            if (playerInventory == null)
            {
                return false;
            }

            CraftingStation currentCraftingStation = player.GetCurrentCraftingStation();
            List<Container> containers = null;

            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement.m_resItem == null || !MatchesCraftingStation(currentCraftingStation, requirement))
                {
                    continue;
                }

                int required = requirement.GetAmount(qualityLevel) * multiplier;
                string itemName = requirement.m_resItem.m_itemData.m_shared.m_name;
                int have = playerInventory.CountItems(itemName);

                if (have < required)
                {
                    if (containers == null)
                    {
                        containers = ContainerService.GetNearbyContainers(player);
                    }

                    have += ContainerItemService.CountItem(containers, itemName);
                }

                // Recipes with m_requireOnlyOneIngredient accept any single listed ingredient (an
                // "or" list, e.g. alternative meats for a stew) rather than requiring every one of them.
                if (recipe.m_requireOnlyOneIngredient)
                {
                    if (have >= required)
                    {
                        return true;
                    }
                }
                else if (have < required)
                {
                    return false;
                }
            }

            return !recipe.m_requireOnlyOneIngredient;
        }

        internal static void ConsumeRequirements(Player player, Piece.Requirement[] requirements, int qualityLevel, int multiplier)
        {
            Inventory playerInventory = player.GetInventory();

            if (playerInventory == null || requirements == null)
            {
                return;
            }

            CraftingStation currentCraftingStation = player.GetCurrentCraftingStation();
            List<Container> containers = null;

            foreach (Piece.Requirement requirement in requirements)
            {
                if (requirement.m_resItem == null || !MatchesCraftingStation(currentCraftingStation, requirement))
                {
                    continue;
                }

                int required = requirement.GetAmount(qualityLevel) * multiplier;

                if (required <= 0)
                {
                    continue;
                }

                string itemName = requirement.m_resItem.m_itemData.m_shared.m_name;
                int fromInventory = Mathf.Min(playerInventory.CountItems(itemName), required);

                if (fromInventory > 0)
                {
                    playerInventory.RemoveItem(itemName, fromInventory);
                }

                int remaining = required - fromInventory;
                int fromContainers = 0;

                if (remaining > 0)
                {
                    if (containers == null)
                    {
                        containers = ContainerService.GetNearbyContainers(player);
                    }

                    fromContainers = ContainerItemService.RemoveItem(containers, itemName, remaining);
                }

                Plugin.Log.LogInfo($"Consumed {itemName}: required {required} (quality {qualityLevel}, multiplier {multiplier}), inventory {fromInventory}, containers {fromContainers}, frame {Time.frameCount}.");
            }
        }

        // Mirrors the crafting-station upgrade filter in the vanilla resource-requirement methods: a
        // requirement flagged as upgrader-only only counts while crafting at a matching upgrade station.
        private static bool MatchesCraftingStation(CraftingStation currentCraftingStation, Piece.Requirement requirement)
        {
            if (currentCraftingStation != null)
            {
                return currentCraftingStation.m_upgrader == requirement.m_upgraderResource;
            }

            return !requirement.m_upgraderResource;
        }
    }
}
