using HarmonyLib;
using Splatform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace NhxQualityPack.Patches
{
    [HarmonyPatch]
    internal static class ChatPatch
    {
        private const int MaxHeldMessages = 50;

        // Talker.m_normalDistance: how far vanilla normal chat reaches. Within it, on-screen text looks exactly like
        // vanilla chat; beyond it (or off-screen) the speaker's name is added so it's clear who said it.
        private const float NormalChatDistance = 15f;

        private static readonly List<HeldMessage> HeldMessages = new List<HeldMessage>();
        private static readonly List<KeyValuePair<Chat.WorldTextInstance, Vector3>> SavedPositions = new List<KeyValuePair<Chat.WorldTextInstance, Vector3>>();

        // Chat.m_hideTimer is private. AccessTools.Field returns null rather than throwing if an update renames it, so
        // the worst case is the chat window not popping open.
        private static readonly FieldInfo HideTimerField = AccessTools.Field(typeof(Chat), "m_hideTimer");

        private static Talker.Type _incomingType;
        private static bool _missedMessages;
        private static string _pendingTimestamp;
        private static bool _pendingSound;
        private static DateTime? _heldMessageTime;

        private class HeldMessage
        {
            public string SenderName;
            public string Text;
            public Talker.Type Type;
            public DateTime ReceivedAt;
        }

        // Normal chat is sent through the speaker's character (Talker.Say), so it only reaches players who have that
        // character loaded and receivers discard it beyond 15m. The ChatMessage RPC that shouts use reaches everyone
        // and carries the chat type as a plain value, so world chat sends normal messages over it. /s shouts are sent
        // the same way, so they look the same as any other world chat message. One copy goes to everybody, rather than
        // vanilla's copy per player, which skips anyone without a character (i.e. respawning or still loading in).
        [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
        [HarmonyPrefix]
        private static bool SendTextPrefix(Talker.Type type, string text)
        {
            if (!Plugin.WorldChatEnabled || (type != Talker.Type.Normal && type != Talker.Type.Shout))
            {
                return true;
            }

            Player player = Player.m_localPlayer;

            if (player != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ChatMessage", player.GetHeadPoint(), (int)Talker.Type.Normal, UserInfo.GetLocalUser(), text);
            }

            return false;
        }

        [HarmonyPatch(typeof(Chat), "AddInworldText")]
        [HarmonyPrefix]
        private static void AddInworldTextPrefix(Talker.Type type)
        {
            _incomingType = type;
        }

        // The game keeps one floating text per sender, so a ping and a chat message from the same player overwrite
        // each other ("meet me here" right after a map ping would remove the ping from everyone's map). Only reuse an
        // entry of the same kind, so pings and chat stay separate. FindExistingWorldText is only called from
        // AddInworldText, whose prefix has just recorded the incoming type.
        [HarmonyPatch(typeof(Chat), "FindExistingWorldText")]
        [HarmonyPrefix]
        private static bool FindExistingWorldTextPrefix(Chat __instance, long senderID, ref Chat.WorldTextInstance __result)
        {
            if (!Plugin.WorldChatEnabled)
            {
                return true;
            }

            bool isPing = _incomingType == Talker.Type.Ping;
            __result = null;

            foreach (Chat.WorldTextInstance worldText in __instance.WorldTexts)
            {
                if (worldText.m_talkerID == senderID && (worldText.m_type == Talker.Type.Ping) == isPing)
                {
                    __result = worldText;
                    break;
                }
            }

            return false;
        }

        // World chat arrives without the speaker's character, so it's attached here, before vanilla positions the
        // text, and retried every frame in case the speaker loads in (or respawns) while the text is still up.
        // Vanilla also moves every text's stored position upwards each frame. That shows whenever the text isn't
        // attached to a character, so the position is saved here and put back afterwards, however vanilla moved it.
        [HarmonyPatch(typeof(Chat), "UpdateWorldTexts")]
        [HarmonyPrefix]
        private static void UpdateWorldTextsPrefix(Chat __instance)
        {
            SavedPositions.Clear();

            if (!Plugin.WorldChatEnabled)
            {
                return;
            }

            foreach (Chat.WorldTextInstance worldText in __instance.WorldTexts)
            {
                if (worldText.m_type != Talker.Type.Normal)
                {
                    continue;
                }

                if (!worldText.m_go)
                {
                    worldText.m_go = FindPlayerObject(worldText.m_talkerID);
                }

                SavedPositions.Add(new KeyValuePair<Chat.WorldTextInstance, Vector3>(worldText, worldText.m_position));
            }
        }

        [HarmonyPatch(typeof(Chat), "UpdateWorldTexts")]
        [HarmonyPostfix]
        private static void UpdateWorldTextsPostfix()
        {
            Camera camera = Utils.GetMainCamera();
            Player localPlayer = Player.m_localPlayer;

            foreach (KeyValuePair<Chat.WorldTextInstance, Vector3> saved in SavedPositions)
            {
                Chat.WorldTextInstance worldText = saved.Key;
                Character character = worldText.m_go ? worldText.m_go.GetComponent<Character>() : null;

                // While attached, keep the stored position on the speaker's head, so that if their character is
                // destroyed (died, teleported, logged out) the text stays where they last were.
                worldText.m_position = character ? character.GetHeadPoint() : saved.Value;

                if (camera != null)
                {
                    ShowName(worldText, !IsNearAndOnScreen(worldText, character, localPlayer, camera));
                }
            }

            SavedPositions.Clear();
        }

        // A chat message's sender ID is the sender's network session ID, which is also the owner part of their
        // character's ZDOID.
        private static GameObject FindPlayerObject(long senderID)
        {
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null && player.GetZDOID().UserID == senderID)
                {
                    return player.gameObject;
                }
            }

            return null;
        }

        // Vanilla sets a normal-chat text to exactly the message, and the name is only ever added in front of it, so
        // the length tells whether the name is currently shown without building the string every frame.
        private static void ShowName(Chat.WorldTextInstance worldText, bool showName)
        {
            string message = worldText.m_text ?? "";
            bool nameShown = worldText.m_textMeshField.text.Length > message.Length;

            if (nameShown != showName)
            {
                worldText.m_textMeshField.text = showName ? worldText.m_name + ": " + message : message;
            }
        }

        // Distance is measured between the two characters, as Talker does when deciding who hears normal chat. The
        // screen test mirrors UpdateWorldTexts (anything failing it is pinned to the screen edge).
        private static bool IsNearAndOnScreen(Chat.WorldTextInstance worldText, Character character, Player localPlayer, Camera camera)
        {
            if (localPlayer == null || character == null)
            {
                return false;
            }

            if (Vector3.Distance(localPlayer.transform.position, character.transform.position) >= NormalChatDistance)
            {
                return false;
            }

            Vector3 screenPos = camera.WorldToScreenPointScaled(worldText.m_position + Vector3.up * 0.3f);
            return screenPos.z >= 0f && screenPos.x >= 0f && screenPos.x <= Screen.width && screenPos.y >= 0f && screenPos.y <= Screen.height;
        }

        // Vanilla silently drops any chat message that arrives while there's no local player (respawning after a death,
        // still loading in) or while the arrival intro plays. Hold those messages instead and show them in the chat log
        // once the player is back. They're old by then, so they get no floating text.
        [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
        [HarmonyPrefix]
        private static bool OnNewChatMessagePrefix(Chat __instance, Talker.Type type, UserInfo sender, string text)
        {
            if (!Plugin.WorldChatEnabled)
            {
                return true;
            }

            if (CanReceiveChat())
            {
                // Messages held from before come first, even if this one arrives before Chat.Update gets to them.
                ShowHeldMessages(__instance);

                // Shown now, but behind the death/teleport/sleep loading screen, and likely gone from view by the time
                // it lifts, so bring the chat window back up then.
                if (type != Talker.Type.Ping && !CanSeeScreen())
                {
                    _missedMessages = true;
                }

                return true;
            }

            if (type == Talker.Type.Ping)
            {
                return true;
            }

            // The sender's name is taken from the server's player list now, while they're certainly connected, in case
            // they've left by the time it's shown. A sender the server doesn't know is dropped, as vanilla would.
            if (sender == null || !ZNet.TryGetPlayerByPlatformUserID(sender.UserId, out ZNet.PlayerInfo playerInfo))
            {
                return false;
            }

            if (HeldMessages.Count >= MaxHeldMessages)
            {
                HeldMessages.RemoveAt(0);
            }

            HeldMessages.Add(new HeldMessage { SenderName = playerInfo.m_name ?? "", Text = text, Type = type, ReceivedAt = DateTime.Now });
            return false;
        }

        // Chat messages get a [HH:mm:ss] local-time prefix in the chat window, and other players' messages a notification
        // sound. Vanilla's own timestamp option uses a long "[MM-dd-yyyy HH:mm:ss]" format, so instead the chat-message
        // overloads of Terminal.AddString mark the line and the plain AddString(string) they finish with adds the prefix
        // and plays the sound - only once the line is really written, as vanilla drops messages from unknown senders.
        // Other lines (command help and output) aren't stamped and stay silent.
        [HarmonyPatch]
        private static class ChatMessageLinePatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Terminal), nameof(Terminal.AddString), new[] { typeof(PlatformUserID), typeof(string), typeof(Talker.Type), typeof(bool) });
                yield return AccessTools.Method(typeof(Terminal), nameof(Terminal.AddString), new[] { typeof(string), typeof(string), typeof(Talker.Type), typeof(bool) });
            }

            // The two overloads differ in their first parameter (the sender's platform ID, or a plain name for held
            // messages), so it's read from __args. Held messages are treated as other players': the local player's own
            // message would have to arrive after they'd already died to end up held.
            private static void Prefix(Terminal __instance, bool timestamp, object[] __args)
            {
                if (!Plugin.WorldChatEnabled || !(__instance is Chat))
                {
                    return;
                }

                if (!timestamp)
                {
                    // ':' in a custom format is the culture's time separator, so the culture is pinned to keep it a colon.
                    _pendingTimestamp = "[" + (_heldMessageTime ?? DateTime.Now).ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] ";
                }

                _pendingSound = Plugin.ChatSoundAlertEnabled && !(__args[0] is PlatformUserID sender && sender == PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
            }

            // Runs even if the method returned early (e.g. an unknown sender) or threw, so a stamp or sound never leaks
            // onto an unrelated line.
            private static void Finalizer()
            {
                _pendingTimestamp = null;
                _pendingSound = false;
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), new[] { typeof(string) })]
        [HarmonyPrefix]
        private static void AddLinePrefix(ref string text)
        {
            if (_pendingTimestamp != null)
            {
                text = _pendingTimestamp + text;
                _pendingTimestamp = null;
            }

            if (_pendingSound)
            {
                _pendingSound = false;
                ChatSoundService.Play();
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(Chat __instance)
        {
            if (!Plugin.WorldChatEnabled)
            {
                ClearSession();
                return;
            }

            if (CanReceiveChat())
            {
                ShowHeldMessages(__instance);
            }

            if (_missedMessages && CanSeeScreen())
            {
                _missedMessages = false;
                ShowChatWindow(__instance);
            }
        }

        private static void ShowHeldMessages(Chat chat)
        {
            if (HeldMessages.Count == 0)
            {
                return;
            }

            List<HeldMessage> messages = new List<HeldMessage>(HeldMessages);
            HeldMessages.Clear();

            foreach (HeldMessage message in messages)
            {
                // One bad message shouldn't lose the rest.
                try
                {
                    // Written straight to the chat log in the same format vanilla uses, stripping '<' and '>' as
                    // vanilla does so the message can't inject rich text.
                    string name = message.SenderName.Replace('<', ' ').Replace('>', ' ');
                    string text = (message.Text ?? "").Replace('<', ' ').Replace('>', ' ');

                    // Stamped with when the message arrived, not when it's finally shown.
                    _heldMessageTime = message.ReceivedAt;
                    ShowChatWindow(chat);
                    chat.AddString(name, text, message.Type);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Failed to show a chat message received while respawning: {e}");
                }
                finally
                {
                    _heldMessageTime = null;
                }
            }
        }

        private static void ShowChatWindow(Chat chat)
        {
            HideTimerField?.SetValue(chat, 0f);
        }

        // Same conditions under which Hud shows its loading screen, plus the arrival intro.
        private static bool CanSeeScreen()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.InIntro() && !player.IsDead() && !player.IsTeleporting() && !player.IsSleeping();
        }

        private static bool CanReceiveChat()
        {
            return Player.m_localPlayer != null && !Player.m_localPlayer.InIntro();
        }

        // Held messages belong to one world session; don't carry them over to the next one or keep them after leaving.
        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        [HarmonyPostfix]
        private static void AwakePostfix()
        {
            ClearSession();
        }

        [HarmonyPatch(typeof(Chat), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroyPostfix()
        {
            ClearSession();
        }

        private static void ClearSession()
        {
            HeldMessages.Clear();
            _missedMessages = false;
        }
    }
}
