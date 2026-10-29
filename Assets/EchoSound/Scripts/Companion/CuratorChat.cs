using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Companion
{
    /// <summary>
    /// Temporary text channel from the curator to the companion: Enter to type, Enter to send, Esc to cancel.
    /// Speech-to-text can later call CompanionBrain.Command in the same way.
    /// </summary>
    public sealed class CuratorChat : MonoBehaviour
    {
        private const string InputName = "CuratorInput";

        public CompanionBrain companion;
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
            if (companion == null) return;
            companion.Spoke += OnSpoke;
            companion.Failed += OnFailed;
            companion.StatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            if (companion == null) return;
            companion.Spoke -= OnSpoke;
            companion.Failed -= OnFailed;
            companion.StatusChanged -= OnStatusChanged;
        }

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
            if (current.type == EventType.KeyDown)
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
            GUILayout.BeginArea(new Rect(16, Screen.height - 336, width, 320));
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
                GUILayout.Label("<i>Enter: give an order to the companion</i>", lineStyle);
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
            AddLine("<b>Curator:</b> " + order);
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
