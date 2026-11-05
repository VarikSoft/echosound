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

        public CompanionBrain companion;
        public CuratorVoice voice;
        [Tooltip("Typed orders, for testing without a microphone or a speech recognition server.")]
        public bool allowTyping;
        [Min(1)] public int visibleLines = 8;

        private readonly List<string> lines = new List<string>();
        private string draft = "";
        private string status = "";
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
            companion.Failed -= OnFailed;
            companion.StatusChanged -= OnStatusChanged;
        }

        private void OnOrderReceived(string order) => AddLine("<b>Curator:</b> " + order);
        private void OnSpoke(string text) => AddLine("<b>Companion:</b> " + text);
        private void OnFailed(string error) => AddLine("<color=#ff8866>" + error + "</color>");
        private void OnStatusChanged(string text) => status = text;

        private void AddLine(string line)
        {
            lines.Add(line);
            if (lines.Count > visibleLines) lines.RemoveRange(0, lines.Count - visibleLines);
        }

        private void OnGUI()
        {
            // Keys are read here, not in Update: the text field consumes them while it has focus.
            // After a state change, skip drawing: this event's layout was measured for the old state.
            Event current = Event.current;
            if (allowTyping && current.type == EventType.KeyDown)
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
            foreach (string line in lines) GUILayout.Label(line, lineStyle);
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
                GUILayout.Label(allowTyping
                    ? "<i>Hold V and speak, or press Enter to type an order</i>"
                    : "<i>Hold V and speak to give the companion an order</i>", lineStyle);
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
