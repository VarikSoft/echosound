using System;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.UI
{
    /// <summary>
    /// Shared IMGUI settings panel and texture/style helpers used by MainMenuController and PauseMenuController.
    /// </summary>
    public static class SettingsMenuUI
    {
        public const string SensitivityPrefsKey = "EchoSound_MouseSensitivity";
        public const float DefaultSensitivity = 2.0f;

        private static Texture2D panelBg;
        private static Texture2D backdropTex;
        private static GUIStyle titleStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle buttonStyle;

        public static float GetSavedSensitivity()
        {
            return PlayerPrefs.GetFloat(SensitivityPrefsKey, DefaultSensitivity);
        }

        public static void SaveSensitivity(float value)
        {
            PlayerPrefs.SetFloat(SensitivityPrefsKey, Mathf.Clamp(value, 0.2f, 10f));
            PlayerPrefs.Save();
        }

        public static void EnsureStyles()
        {
            if (panelBg == null) panelBg = CreateSolidTexture(new Color(0.08f, 0.10f, 0.14f, 0.94f));
            if (backdropTex == null) backdropTex = CreateSolidTexture(new Color(0f, 0f, 0f, 0.65f));

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(0.92f, 0.95f, 1f);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft
                };
                labelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        public static GUIStyle TitleStyle { get { EnsureStyles(); return titleStyle; } }
        public static GUIStyle ButtonStyle { get { EnsureStyles(); return buttonStyle; } }
        public static Texture2D PanelBg { get { EnsureStyles(); return panelBg; } }
        public static Texture2D BackdropTex { get { EnsureStyles(); return backdropTex; } }

        public static void DrawSettingsPanel(Rect panelRect, Action onBack, Action<float> onSensitivityChanged = null)
        {
            EnsureStyles();
            GUI.DrawTexture(panelRect, panelBg);

            GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 20f, panelRect.width - 48f, panelRect.height - 40f));
            GUILayout.Label("SETTINGS", titleStyle, GUILayout.Height(36f));
            GUILayout.Space(12f);

            var audio = AudioManager.Instance;
            float master = audio != null ? audio.MasterVolume : AudioListener.volume;
            float sfx = audio != null ? audio.SFXVolume : 1f;
            float voice = audio != null ? audio.VoiceVolume : 1f;
            float ambience = audio != null ? audio.AmbienceVolume : 1f;

            float newMaster = DrawSlider("Master Volume", master, 0f, 1f);
            if (!Mathf.Approximately(newMaster, master))
            {
                if (audio != null) audio.SetMasterVolume(newMaster);
                else AudioListener.volume = newMaster;
            }

            if (audio != null)
            {
                float newSfx = DrawSlider("SFX Volume", sfx, 0f, 1f);
                if (!Mathf.Approximately(newSfx, sfx)) audio.SetSFXVolume(newSfx);

                float newVoice = DrawSlider("Voice Volume", voice, 0f, 1f);
                if (!Mathf.Approximately(newVoice, voice)) audio.SetVoiceVolume(newVoice);

                float newAmbience = DrawSlider("Ambience Volume", ambience, 0f, 1f);
                if (!Mathf.Approximately(newAmbience, ambience)) audio.SetAmbienceVolume(newAmbience);
            }

            GUILayout.Space(6f);
            float sens = GetSavedSensitivity();
            float newSens = DrawSlider("Mouse Sensitivity", sens, 0.2f, 8f, "F1");
            if (!Mathf.Approximately(newSens, sens))
            {
                SaveSensitivity(newSens);
                onSensitivityChanged?.Invoke(newSens);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(38f)))
            {
                onBack?.Invoke();
            }
            GUILayout.EndArea();
        }

        private static float DrawSlider(string label, float value, float min, float max, string format = "P0")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(150f));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            string display = format == "P0" ? $"{Mathf.RoundToInt(next * 100f)}%" : next.ToString(format);
            GUILayout.Label(display, labelStyle, GUILayout.Width(52f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            return next;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
