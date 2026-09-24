using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class ContainerService
    {
        // Container.CheckAccess(long) is private in the actual game assembly loaded at runtime
        private static readonly MethodInfo CheckAccessMethod = AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) });

        // Inventory.Changed(bool, bool) is private; shared here so container/inventory mutations
        // done through the low-level RemoveItem/AddItem APIs get saved and synced like the vanilla
        // container UI would (a plain field/stack mutation otherwise leaves the ZDO stale).
        private static readonly MethodInfo InventoryChangedMethod = AccessTools.Method(typeof(Inventory), "Changed", new[] { typeof(bool), typeof(bool) });
        private static readonly object[] InventoryChangedArguments = { false, false };

        // Building/crafting requirement checks and the requirement-panel display can all call this
        // several times in the same frame (once per resource, per recipe row); cache per frame so
        // that only triggers one Physics.OverlapSphere instead of one per call site.
        private static int _cachedFrame = -1;
        private static Player _cachedPlayer;
        private static List<Container> _cachedContainers = new List<Container>();

        internal static List<Container> GetNearbyContainers(Player player)
        {
            if (player == null)
            {
                return new List<Container>();
            }

            int frame = Time.frameCount;

            if (frame == _cachedFrame && _cachedPlayer == player)
            {
                return _cachedContainers;
            }

            List<Container> containers = new List<Container>();
            Collider[] colliders = Physics.OverlapSphere(player.transform.position, Plugin.SearchRadius);
            HashSet<Container> foundContainers = new HashSet<Container>();

            foreach (Collider collider in colliders)
            {
                Container container = collider.GetComponentInParent<Container>();

                if (container == null)
                {
                    continue;
                }

                if (!foundContainers.Add(container))
                {
                    continue;
                }

                if (!IsAccessibleContainer(container))
                {
                    continue;
                }

                containers.Add(container);
            }

            _cachedFrame = frame;
            _cachedPlayer = player;
            _cachedContainers = containers;

            return containers;
        }

        internal static void NotifyInventoryChanged(Inventory inventory)
        {
            InventoryChangedMethod?.Invoke(inventory, InventoryChangedArguments);
        }

        internal static bool IsAccessibleContainer(Container container)
        {
            if (container == null)
            {
                return false;
            }

            Piece piece = container.GetComponentInParent<Piece>();

            if (piece == null)
            {
                return false;
            }

            // A build placement ghost is a real clone of the piece prefab - Container component
            // included - but it was never registered on the network, so its ZNetView has no ZDO yet.
            // Container.CheckAccess assumes a fully-initialized container and throws on one of these.
            ZNetView nview = container.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
            {
                return false;
            }

            if (CheckAccessMethod == null)
            {
                return false;
            }

            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();

            return (bool)CheckAccessMethod.Invoke(container, new object[] { playerId });
        }

        internal static bool EnsureOwnership(Container container)
        {
            if (container == null)
            {
                return false;
            }

            ZNetView nview = container.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            if (nview.IsOwner())
            {
                return true;
            }

            ZDO zdo = nview.GetZDO();

            if (zdo.GetInt(ZDOVars.s_inUse) == 1)
            {
                return false;
            }

            long previousOwner = zdo.GetOwner();

            nview.ClaimOwnership();

            if (previousOwner != 0L && ZDOMan.instance != null)
            {
                ZDOMan.instance.ForceSendZDO(previousOwner, zdo.m_uid);
            }

            return true;
        }
    }
}
