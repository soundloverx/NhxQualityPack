using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NhxQualityPack.UI
{
    internal static class TamingBarRenderer
    {
        private const string BarName = "NhxTamingBar";
        private const string HealthName = "Health";
        private const string HealthBarName = "health_fast";
        private const float BarGap = 2f;

        private static readonly Color BarColor = Color.cyan;

        private static readonly FieldInfo HudsField = AccessTools.Field(typeof(EnemyHud), "m_huds");
        private static readonly Type HudDataType = AccessTools.Inner(typeof(EnemyHud), "HudData");
        private static readonly FieldInfo HudCharacterField = HudDataType != null ? AccessTools.Field(HudDataType, "m_character") : null;
        private static readonly FieldInfo HudGuiField = HudDataType != null ? AccessTools.Field(HudDataType, "m_gui") : null;

        internal static void Refresh(EnemyHud enemyHud)
        {
            IDictionary huds = HudsField?.GetValue(enemyHud) as IDictionary;

            if (huds == null || HudCharacterField == null || HudGuiField == null)
            {
                return;
            }

            foreach (object hud in huds.Values)
            {
                GameObject gui = HudGuiField.GetValue(hud) as GameObject;

                if (gui == null || !gui.activeInHierarchy)
                {
                    continue;
                }

                float progress = Plugin.ShowTamingProgress ? GetTamingProgress(HudCharacterField.GetValue(hud) as Character) : 0f;
                Transform existing = gui.transform.Find(BarName);

                if (progress <= 0f)
                {
                    if (existing != null)
                    {
                        existing.gameObject.SetActive(false);
                    }

                    continue;
                }

                GuiBar bar = existing != null ? existing.Find(HealthBarName)?.GetComponent<GuiBar>() : CreateBar(enemyHud, gui);

                if (bar == null)
                {
                    continue;
                }

                bar.transform.parent.gameObject.SetActive(true);
                bar.SetValue(progress);
            }
        }

        private static float GetTamingProgress(Character character)
        {
            if (character == null)
            {
                return 0f;
            }

            Tameable tameable = character.GetComponent<Tameable>();

            if (tameable == null || tameable.IsTamed() || tameable.m_tamingTime <= 0f)
            {
                return 0f;
            }

            ZNetView nview = character.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
            {
                return 0f;
            }

            // same math as Tameable.GetTameness, read straight from the synced ZDO so it works on any client
            float remaining = nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, tameable.m_tamingTime);
            float progress = 1f - Mathf.Clamp01(remaining / tameable.m_tamingTime);

            // the hover text keeps saying "Wild" until the truncated percentage reaches 1%, so match that
            return (int)(progress * 100f) > 0 ? progress : 0f;
        }

        private static GuiBar CreateBar(EnemyHud enemyHud, GameObject gui)
        {
            // Cloned from the never-used template rather than this hud's own health bar: GuiBar takes its full width
            // from the bar's current size, and the live one has already been shrunk to the creature's health.
            Transform template = enemyHud.m_baseHud != null ? enemyHud.m_baseHud.transform.Find(HealthName) : null;
            Transform health = gui.transform.Find(HealthName);

            if (template == null || health == null)
            {
                return null;
            }

            GameObject root = Object.Instantiate(template.gameObject, gui.transform);
            root.name = BarName;
            root.transform.SetSiblingIndex(health.GetSiblingIndex() + 1);

            GuiBar bar = null;

            foreach (GuiBar guiBar in root.GetComponentsInChildren<GuiBar>(true))
            {
                if (guiBar.gameObject == root)
                {
                    continue;
                }

                if (guiBar.name == HealthBarName && guiBar.transform.parent == root.transform)
                {
                    bar = guiBar;
                }
                else
                {
                    Object.Destroy(guiBar.gameObject);
                }
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                Object.Destroy(text.gameObject);
            }

            if (bar == null)
            {
                Object.Destroy(root);
                return null;
            }

            RectTransform rootRect = (RectTransform)root.transform;
            RectTransform barRect = (RectTransform)bar.transform;
            float height = Mathf.Max(rootRect.rect.height, barRect.rect.height);
            rootRect.anchoredPosition += Vector2.down * (height + BarGap);

            bar.gameObject.SetActive(true);
            bar.m_smoothDrain = false;
            bar.m_smoothFill = false;

            Image image = bar.m_bar != null ? bar.m_bar.GetComponent<Image>() : null;

            if (image != null)
            {
                image.color = BarColor;
            }

            return bar;
        }
    }
}
