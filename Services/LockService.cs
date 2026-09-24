using System.Collections.Generic;
using System.IO;

namespace NhxQualityPack
{
    internal static class LockService
    {
        // These keys still carry the mod's previous name (NSimpleDeposit) on purpose: they're persisted in each
        // character's save data, so renaming them would silently drop every existing lock.
        private const string LockedKeyPrefix = "NSimpleDeposit.Locked:";
        private const string LegacyMigratedKey = "NSimpleDeposit.LegacyLocksMigrated";
        private const string LegacyLockedFileName = "NSimpleDeposit.locked.txt";

        internal static bool IsLocked(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null)
            {
                return false;
            }

            return IsLocked(item.m_shared.m_name);
        }

        internal static bool IsLocked(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return false;
            }

            Player player = Player.m_localPlayer;

            if (player == null || player.m_customData == null)
            {
                return false;
            }

            return player.m_customData.ContainsKey(LockedKeyPrefix + itemName);
        }

        internal static void ToggleLocked(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null || item.m_shared == null || player.m_customData == null)
            {
                return;
            }

            if (!IsVanillaInventoryItem(player, item))
            {
                return;
            }

            string key = LockedKeyPrefix + item.m_shared.m_name;

            if (!player.m_customData.Remove(key))
            {
                player.m_customData[key] = "1";
            }
        }

        internal static bool IsVanillaInventoryItem(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null)
            {
                return false;
            }

            int vanillaHeight = PlayerInventoryService.GetVanillaHeight(player);

            return item.m_gridPos.y >= 0 && item.m_gridPos.y < vanillaHeight;
        }

        internal static void MigrateLegacyLocksIfNeeded(Player player)
        {
            if (player == null || player.m_customData == null)
            {
                return;
            }

            if (player.m_customData.ContainsKey(LegacyMigratedKey))
            {
                return;
            }

            string legacyPath = Path.Combine(BepInEx.Paths.ConfigPath, LegacyLockedFileName);

            if (File.Exists(legacyPath))
            {
                foreach (string line in File.ReadAllLines(legacyPath))
                {
                    string itemName = line.Trim();

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        player.m_customData[LockedKeyPrefix + itemName] = "1";
                    }
                }
            }

            player.m_customData[LegacyMigratedKey] = "1";
        }
    }
}
