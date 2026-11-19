using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Companion
{
    /// <summary>
    /// Shows the conversation with the companion: heard orders, replies and problems. Orders arrive by voice
    /// through CuratorVoice. With allowTyping: Enter to type, Enter to send, Esc to cancel.
    /// </summary>
    public sealed class CuratorChat : MonoBehaviour
    {
        private const string InputName = "CuratorInput";
        private static readonly string[] CompanionModes = { "Quick commands", "Fast LLM", "Free dialogue (slower)" };
        private static readonly string[] VoiceLanguages = { "Russian", "Polish", "English", "Auto" };

        public CompanionBrain companion;
        public CuratorVoice voice;
        [Tooltip("Typed orders are also always available if voice recognition is unavailable.")]
        public bool allowTyping = true;
        [Min(1)] public int visibleLines = 8;

        private readonly List<string> lines = new List<string>();
        private string draft = "";
        private string status = "";
        private string partialReply = "";
        private bool typing;
        private bool focusInput;
        private GUIStyle lineStyle;

        public bool IsTyping => typing;

        private void OnEnable()
        {
            if (voice != null)
            {
                voice.Failed += OnFailed;
                voice.StatusChanged += OnStatusChanged;
            }
            if (companion == null) return;
            companion.OrderReceived += OnOrderReceived;
            companion.Spoke += OnSpoke;
            companion.ReplyProgress += OnReplyProgress;
            companion.Failed += OnFailed;
            companion.StatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            if (voice != null)
            {
                voice.Failed -= OnFailed;
                voice.StatusChanged -= OnStatusChanged;
            }
            if (companion == null) return;
            companion.OrderReceived -= OnOrderReceived;
            companion.Spoke -= OnSpoke;
            companion.ReplyProgress -= OnReplyProgress;
            partialReply = "";
            companion.Failed -= OnFailed;
            companion.StatusChanged -= OnStatusChanged;
        }

        private void OnOrderReceived(string order) => AddLine("<b>Curator:</b> " + order);
        private void OnSpoke(string text) => AddLine("<b>Companion:</b> " + text);
        private void OnReplyProgress(string text) => partialReply = text;
        private void OnFailed(string error) => AddLine("<color=#ff8866>" + error + "</color>");
        private void OnStatusChanged(string text) => status = text;

        private void AddLine(string line)
        {
            lines.Add(line);
            if (lines.Count > visibleLines) lines.RemoveRange(0, lines.Count - visibleLines);
        }

        private void OnGUI()
        {
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            bool canType = allowTyping || !runtime.SpeechReady;
            // Keys are read here, not in Update: the text field consumes them while it has focus.
            // After a state change, skip drawing: this event's layout was measured for the old state.
            Event current = Event.current;
            if (canType && current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                {
                    if (typing) Send();
                    else Open();
                    current.Use();
                    return;
                }
                if (typing && current.keyCode == KeyCode.Escape)
                {
                    Close();
                    current.Use();
                    return;
                }
            }

            if (lineStyle == null) lineStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };

            float width = Mathf.Min(620, Screen.width - 32);
            // Ends above the push-to-talk indicator in the bottom-left corner.
            GUILayout.BeginArea(new Rect(16, Screen.height - 390, width, 320));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUI.skin.box);
            if (companion != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    GUILayout.Label(companion.FreeDialogue ? "Free dialogue · slower replies" : companion.Mode == CompanionBrain.CompanionMode.FastLlm ? "Fast LLM · Esc for dialogue settings" : "Quick commands · Esc for dialogue settings");
                else
                {
                    int mode = (int)companion.Mode;
                    int nextMode = GUILayout.Toolbar(mode, CompanionModes);
                    if (nextMode != mode) companion.SetMode((CompanionBrain.CompanionMode)nextMode);
                }
            }
            if (voice != null)
            {
                string currentLanguage = voice.whisper.RecognitionLanguage;
                int selected = currentLanguage == "russian" ? 0 : currentLanguage == "polish" ? 1 : currentLanguage == "english" ? 2 : 3;
                if (Cursor.lockState == CursorLockMode.Locked) GUILayout.Label("Voice: " + VoiceLanguages[selected] + " · Esc to change");
                else
                {
                    int next = GUILayout.Toolbar(selected, VoiceLanguages);
                    if (next != selected) voice.SetLanguage(next == 0 ? WhisperClient.SpeechLanguage.Russian : next == 1 ? WhisperClient.SpeechLanguage.Polish : next == 2 ? WhisperClient.SpeechLanguage.English : WhisperClient.SpeechLanguage.Auto);
                }
            }
            if (runtime.ChatStarting || runtime.SpeechStarting || (companion != null && companion.FastStarting)) GUILayout.Label("Preparing your companion. Basic orders are available.");
            else if (companion != null && companion.Mode != CompanionBrain.CompanionMode.QuickCommands && !string.IsNullOrEmpty(runtime.ChatError)) GUILayout.Label(runtime.ChatError, lineStyle);
            if (!runtime.SpeechStarting && !runtime.SpeechReady) GUILayout.Label(runtime.SpeechError, lineStyle);
            foreach (string line in lines) GUILayout.Label(line, lineStyle);
            if (!string.IsNullOrEmpty(partialReply))
                GUILayout.Label("<b>Companion:</b> " + partialReply.Replace('<', '＜').Replace('>', '＞'), lineStyle);
            if (!string.IsNullOrEmpty(status)) GUILayout.Label("<i>Companion: " + status + "...</i>", lineStyle);

            if (typing)
            {
                GUI.SetNextControlName(InputName);
                draft = GUILayout.TextField(draft, 200);
                if (focusInput)
                {
                    GUI.FocusControl(InputName);
                    focusInput = false;
                }
            }
            else
            {
                GUILayout.Label(canType
                    ? "<i>Hold V and speak, or press Enter to type an order</i>"
                    : "<i>Hold V and speak to give the companion an order</i>", lineStyle);
                if (Cursor.lockState != CursorLockMode.Locked && companion != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Follow me")) companion.Command("follow me");
                    if (GUILayout.Button("Stop")) companion.Command("stop");
                    if (GUILayout.Button("Come here")) companion.Command("come here");
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void Open()
        {
            typing = true;
            focusInput = true;
            draft = "";
            // FirstPersonPlayer ignores input while the cursor is unlocked, so typing does not move the player.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Send()
        {
            string order = draft.Trim();
            Close();
            if (order.Length == 0 || companion == null) return;
            companion.Command(order);
        }

        private void Close()
        {
            typing = false;
            draft = "";
            GUI.FocusControl(null);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
