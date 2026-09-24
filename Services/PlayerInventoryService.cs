using UnityEngine;

namespace NhxQualityPack
{
    internal static class PlayerInventoryService
    {
        internal static int GetVanillaHeight(Player player)
        {
            if (player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out string value) && int.TryParse(value, out int rows))
            {
                return Mathf.Clamp(rows, 0, 9);
            }

            return 4;
        }
    }
}
