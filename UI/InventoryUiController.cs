using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NhxQualityPack
{
    internal static class InventoryUiController
    {
        private const string SortButtonName = "NSDSortButton";
        private const string QuickStackButtonName = "NSDQuickStackButton";
        private const float ButtonSize = 36f;
        private const float ButtonSpacing = 4f;

        private static readonly FieldInfo CurrentContainerField = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        private static InventoryGui _inventoryGui;

        internal static void Initialize(InventoryGui inventoryGui)
        {
            if (inventoryGui == null)
            {
                return;
            }

            _inventoryGui = inventoryGui;

            CreateButtons();
        }

        private static void CreateButtons()
        {
            if (_inventoryGui == null || _inventoryGui.m_player == null || _inventoryGui.m_takeAllButton == null)
            {
                return;
            }

            Button nativeButton = _inventoryGui.m_takeAllButton;

            CreateActionButton(nativeButton, SortButtonName, "S", 0, OnSortClicked);
            CreateActionButton(nativeButton, QuickStackButtonName, "Q", 1, OnQuickStackClicked);
        }

        private static Button CreateActionButton(Button template, string buttonName, string text, int index, UnityEngine.Events.UnityAction onClick)
        {
            Transform existing = _inventoryGui.m_player.transform.Find(buttonName);

            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            GameObject buttonObject = UnityEngine.Object.Instantiate(template.gameObject, _inventoryGui.m_player.transform);
            buttonObject.name = buttonName;

            Button button = buttonObject.GetComponent<Button>();

            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                return null;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);

            SetButtonText(buttonObject, text);
            StyleActionButton(buttonObject);
            PositionActionButton(buttonObject, index);

            buttonObject.SetActive(true);

            return button;
        }

        private static void SetButtonText(GameObject buttonObject, string text)
        {
            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = text;
                label.alignment = TextAlignmentOptions.Center;
                return;
            }

            Text legacyLabel = buttonObject.GetComponentInChildren<Text>(true);

            if (legacyLabel != null)
            {
                legacyLabel.text = text;
                legacyLabel.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void StyleActionButton(GameObject buttonObject)
        {
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();

            if (rectTransform == null)
            {
                return;
            }

            rectTransform.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            Image image = buttonObject.GetComponent<Image>();

            if (image != null)
            {
                image.type = Image.Type.Sliced;
            }
        }

        private static void PositionActionButton(GameObject buttonObject, int index)
        {
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            RectTransform playerRectTransform = _inventoryGui.m_player.GetComponent<RectTransform>();

            if (rectTransform == null || playerRectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);

            float x = playerRectTransform.rect.width + 8f + (index * (ButtonSize + ButtonSpacing));
            float y = -playerRectTransform.rect.height + ButtonSize;

            rectTransform.anchoredPosition = new Vector2(x, y);
        }

        private static void OnSortClicked()
        {
            InventorySortService.SortPlayerInventory();

            if (_inventoryGui == null)
            {
                return;
            }

            Container container = CurrentContainerField?.GetValue(_inventoryGui) as Container;

            if (container != null)
            {
                InventorySortService.SortContainer(container);
            }
        }

        private static void OnQuickStackClicked()
        {
            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            QuickStackService.QuickStack(player);
        }
    }
}
