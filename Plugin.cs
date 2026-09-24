using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NhxQualityPack
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "NhxQualityPack";
        private const string PluginName = "NhxQualityPack";
        private const string PluginVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private ConfigEntry<int> _searchRadius;
        private ConfigEntry<KeyboardShortcut> _quickStackShortcut;
        private ConfigEntry<KeyboardShortcut> _fillAllModifierKey;
        private ConfigEntry<KeyboardShortcut> _hotbarSwapShortcut;
        private ConfigEntry<bool> _worldChat;
        private Harmony _harmonyInstance;

        internal static int SearchRadius => Instance?._searchRadius != null ? Instance._searchRadius.Value : 25;
        internal static KeyboardShortcut QuickStackShortcut => Instance?._quickStackShortcut != null ? Instance._quickStackShortcut.Value : new KeyboardShortcut(KeyCode.P);
        internal static KeyboardShortcut FillAllModifierKey => Instance?._fillAllModifierKey != null ? Instance._fillAllModifierKey.Value : new KeyboardShortcut(KeyCode.LeftShift);

        internal static KeyboardShortcut HotbarSwapShortcut => Instance?._hotbarSwapShortcut != null ? Instance._hotbarSwapShortcut.Value : new KeyboardShortcut(KeyCode.BackQuote);

        internal static bool WorldChatEnabled => Instance?._worldChat != null ? Instance._worldChat.Value : true;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            _searchRadius = Config.Bind(
                "General",
                "Search Radius",
                25,
                new ConfigDescription(
                    "The radius in which to search for nearby containers when quick stacking.",
                    new AcceptableValueRange<int>(5, 150)
                )
            );

            _quickStackShortcut = Config.Bind(
                "General",
                "Quick Stack Shortcut",
                new KeyboardShortcut(KeyCode.P),
                "Keyboard shortcut used to quick stack nearby containers."
            );

            _fillAllModifierKey = Config.Bind(
                "General",
                "Fill All Modifier Key",
                new KeyboardShortcut(KeyCode.LeftShift),
                "Hold this modifier while using a fireplace/light or a smelter/kiln to fill it to capacity (fuel or ore) from your inventory and nearby containers in one interaction, instead of adding one unit at a time."
            );

            _hotbarSwapShortcut = Config.Bind(
                "General",
                "Hotbar Swap Shortcut",
                new KeyboardShortcut(KeyCode.BackQuote),
                "Keyboard shortcut used to swap the hotbar (first inventory row) with the second inventory row."
            );

            _worldChat = Config.Bind(
                "Chat",
                "World Chat",
                true,
                "Send normal chat messages (and /s shouts) to every player in the world instead of only nearby players. They're shown like normal chat, without a marker on the map, and the sender's name is added when they're far away or out of view. Chat messages also get a [HH:mm] timestamp in the chat window. Turning this off leaves chat exactly as in the unmodded game."
            );

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmonyInstance = new Harmony(PluginGuid);
            _harmonyInstance.PatchAll(assembly);

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void Update()
        {
            HandleHotbarSwap();
            HandleQuickStack();
        }

        private static void HandleHotbarSwap()
        {
            if (!InputService.IsDown(HotbarSwapShortcut) || IsTypingInInputField())
            {
                return;
            }

            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            if (HotbarSwapService.SwapHotbarRows(player))
            {
                player.Message(MessageHud.MessageType.Center, "Hotbar swapped");
            }
        }

        private static void HandleQuickStack()
        {
            if (!InputService.IsDown(QuickStackShortcut))
            {
                return;
            }

            if (IsTypingInInputField())
            {
                Log.LogInfo("Quick stack hotkey ignored: an input field is focused.");
                return;
            }

            Player player = Player.m_localPlayer;

            if (player == null)
            {
                return;
            }

            QuickStackResult result = QuickStackService.QuickStack(player);
            Log.LogInfo($"Quick stack: {result.Outcome} ({result.Deposited}/{result.TotalEligible}).");
            ShowQuickStackResultMessage(player, result);
        }

        private static void ShowQuickStackResultMessage(Player player, QuickStackResult result)
        {
            switch (result.Outcome)
            {
                case QuickStackOutcome.NoContainersFound:
                    player.Message(MessageHud.MessageType.Center, "No available containers");
                    break;

                case QuickStackOutcome.NothingToDeposit:
                    break;

                case QuickStackOutcome.NoMatchingItems:
                    player.Message(MessageHud.MessageType.Center, "No matching containers");
                    break;

                case QuickStackOutcome.NoRoomForItems:
                    player.Message(MessageHud.MessageType.Center, "No room for items");
                    break;

                case QuickStackOutcome.ItemsDeposited:
                    if (result.Deposited >= result.TotalEligible)
                    {
                        player.Message(MessageHud.MessageType.Center, "All items deposited");
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.Center, $"{result.Deposited}/{result.TotalEligible} items deposited");
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            _harmonyInstance?.UnpatchSelf();

            Log?.LogInfo($"{PluginName} v{PluginVersion} unloaded.");

            Instance = null;
        }

        private static bool IsTypingInInputField()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }

            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

            // The EventSystem keeps pointing at an input field after its panel is closed (e.g. the portal/sign
            // text dialog only deactivates its panel), so being selected isn't enough - it has to be focused.
            InputField legacyField = selectedObject.GetComponent<InputField>();
            TMP_InputField tmpField = selectedObject.GetComponent<TMP_InputField>();

            return (legacyField != null && legacyField.isFocused) || (tmpField != null && tmpField.isFocused);
        }
    }
}
