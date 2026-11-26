using UnityEngine;
using UnityEngine.SceneManagement;
using EchoSound.Combat;

namespace EchoSound.UI
{
    /// <summary>
    /// In-game pause menu toggled with ESC.
    /// Provides Resume, Settings, and Leave to Main Menu actions.
    /// </summary>
    [AddComponentMenu("EchoSound/UI/Pause Menu Controller")]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Scene Navigation")]
        public string mainMenuSceneName = "MainMenu";

        [Header("Target References (Optional)")]
        public HealthComponent health;

        public bool IsPaused { get; private set; }
        private bool showingSettings;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<HealthComponent>();
        }

        private void Update()
        {
            if (health != null && !health.IsAlive) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused && showingSettings)
                {
                    showingSettings = false;
                }
                else
                {
                    SetPaused(!IsPaused);
                }
            }
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            showingSettings = false;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        public void LeaveToMainMenu()
        {
            IsPaused = false;
            showingSettings = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnDisable()
        {
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }

        private void OnGUI()
        {
            if (!IsPaused) return;
            if (health != null && !health.IsAlive) return;

            SettingsMenuUI.EnsureStyles();
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), SettingsMenuUI.BackdropTex);

            float width = showingSettings ? 420f : 320f;
            float height = showingSettings ? 340f : 270f;
            Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            if (showingSettings)
            {
                SettingsMenuUI.DrawSettingsPanel(
                    panelRect,
                    () => showingSettings = false,
                    newSens => gameObject.SendMessage("SetSensitivity", newSens, SendMessageOptions.DontRequireReceiver));
                return;
            }

            GUI.DrawTexture(panelRect, SettingsMenuUI.PanelBg);
            GUILayout.BeginArea(new Rect(panelRect.x + 28f, panelRect.y + 24f, panelRect.width - 56f, panelRect.height - 48f));

            GUILayout.Label("PAUSED", SettingsMenuUI.TitleStyle, GUILayout.Height(42f));
            GUILayout.Space(16f);

            if (GUILayout.Button("Resume", SettingsMenuUI.ButtonStyle, GUILayout.Height(40f)))
            {
                SetPaused(false);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Settings", SettingsMenuUI.ButtonStyle, GUILayout.Height(40f)))
            {
                showingSettings = true;
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Leave to Main Menu", SettingsMenuUI.ButtonStyle, GUILayout.Height(40f)))
            {
                LeaveToMainMenu();
            }

            GUILayout.EndArea();
        }
    }
}
