using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NhxQualityPack.UI
{
    internal static class LockBorderRenderer
    {
        private const string BorderName = "NSDLockBorder";
        private const float BorderThickness = 2f;
        private const float BorderInset = 3f;

        private static readonly Color BorderColor = new Color(0.3f, 0.6f, 1f, 1f);

        private static readonly FieldInfo ElementsField = AccessTools.Field(typeof(InventoryGrid), "m_elements");

        internal static void Refresh(InventoryGrid grid)
        {
            if (grid == null || grid.GetInventory() == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;

            if (player == null || grid.GetInventory() != player.GetInventory())
            {
                return;
            }

            List<InventoryElement> elements = ElementsField?.GetValue(grid) as List<InventoryElement>;

            if (elements == null)
            {
                return;
            }

            int width = grid.GetInventory().GetWidth();

            foreach (InventoryElement element in elements)
            {
                if (element == null)
                {
                    continue;
                }

                SetBorderVisible(element, false);
            }

            foreach (ItemDrop.ItemData item in grid.GetInventory().GetAllItems())
            {
                if (item == null || !LockService.IsLocked(item))
                {
                    continue;
                }

                int index = item.m_gridPos.y * width + item.m_gridPos.x;

                if (index < 0 || index >= elements.Count)
                {
                    continue;
                }

                InventoryElement element = elements[index];

                if (element != null)
                {
                    SetBorderVisible(element, true);
                }
            }
        }

        private static void SetBorderVisible(InventoryElement element, bool visible)
        {
            Transform existing = element.transform.Find(BorderName);
            GameObject border = existing != null ? existing.gameObject : null;

            if (border == null && visible)
            {
                border = CreateBorder(element.transform);
            }

            if (border != null)
            {
                border.SetActive(visible);
            }
        }

        private static GameObject CreateBorder(Transform parent)
        {
            GameObject border = new GameObject(BorderName, typeof(RectTransform));
            RectTransform borderRect = border.GetComponent<RectTransform>();

            borderRect.SetParent(parent, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(BorderInset, BorderInset);
            borderRect.offsetMax = new Vector2(-BorderInset, -BorderInset);

            CreateEdge(border.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -BorderThickness), Vector2.zero);
            CreateEdge(border.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BorderThickness));
            CreateEdge(border.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BorderThickness, 0f));
            CreateEdge(border.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-BorderThickness, 0f), Vector2.zero);

            borderRect.SetAsLastSibling();

            return border;
        }

        private static void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = edge.GetComponent<Image>();
            image.color = BorderColor;
            image.raycastTarget = false;
        }
    }
}
