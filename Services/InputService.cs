using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace NhxQualityPack
{
    internal static class InputService
    {
        private static readonly KeyCode[] StrictModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.LeftCommand, KeyCode.RightCommand,
            KeyCode.LeftWindows, KeyCode.RightWindows,
            KeyCode.AltGr
        };

        // BepInEx's own KeyboardShortcut.IsPressed()/IsDown() is meant to be polled once per Update
        // frame and can behave unreliably when checked from inside a Harmony patch instead, so this
        // checks the raw key state directly via the game's own input wrapper (same approach other
        // Valheim mods use for this reason; the game reads input through ZInput, not UnityEngine.Input).
        internal static bool IsHeld(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None
                && ZInput.GetKey(shortcut.MainKey, false)
                && shortcut.Modifiers.All(key => ZInput.GetKey(key, false));
        }

        // BepInEx's KeyboardShortcut.IsDown() fails whenever any key outside the shortcut is held,
        // so it never fires while the player is moving/running (WASD, Shift). This ignores ordinary
        // keys, but still rejects extra Ctrl/Alt/Windows modifiers so e.g. a bare "P" doesn't also
        // fire on another mod's Ctrl+P. Shift is deliberately not rejected since it's the run key.
        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None
                || !ZInput.GetKeyDown(shortcut.MainKey, false)
                || !shortcut.Modifiers.All(key => ZInput.GetKey(key, false)))
            {
                return false;
            }

            return !StrictModifierKeys.Any(key => key != shortcut.MainKey
                && !shortcut.Modifiers.Contains(key)
                && ZInput.GetKey(key, false));
        }
    }
}
