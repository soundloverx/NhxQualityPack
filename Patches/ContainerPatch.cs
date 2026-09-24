using HarmonyLib;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch(typeof(Container), nameof(Container.RPC_OpenResponse))]
    internal static class ContainerPatch
    {
        private static void Postfix(Container __instance, bool granted)
        {
            if (!granted)
            {
                return;
            }

            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            if (!ContainerService.IsAccessibleContainer(__instance))
            {
                return;
            }

            InventorySortService.SortContainer(__instance);
        }
    }
}
