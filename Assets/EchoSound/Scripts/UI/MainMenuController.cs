using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoSound.UI
{
    /// <summary>
    /// Simple Main Menu controller allowing the player to start/restart the level,
    /// adjust audio and mouse settings, or quit the application.
    /// </summary>
    [AddComponentMenu("EchoSound/UI/Main Menu Controller")]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [Tooltip("Name of the gameplay level scene to load when Start Level is clicked.")]
        public string levelSceneName = "GenerationTest";

        private bool showingSettings;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            SettingsMenuUI.EnsureStyles();
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), SettingsMenuUI.BackdropTex);

            float width = showingSettings ? 420f : 340f;
            float height = showingSettings ? 340f : 280f;
            Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            if (showingSettings)
            {
                SettingsMenuUI.DrawSettingsPanel(panelRect, () => showingSettings = false);
                return;
            }

            GUI.DrawTexture(panelRect, SettingsMenuUI.PanelBg);
            GUILayout.BeginArea(new Rect(panelRect.x + 28f, panelRect.y + 24f, panelRect.width - 56f, panelRect.height - 48f));

            GUILayout.Label("ECHOSOUND", SettingsMenuUI.TitleStyle, GUILayout.Height(46f));
            GUILayout.Space(20f);

            if (GUILayout.Button("Start Level", SettingsMenuUI.ButtonStyle, GUILayout.Height(42f)))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(levelSceneName);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Settings", SettingsMenuUI.ButtonStyle, GUILayout.Height(40f)))
            {
                showingSettings = true;
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Quit", SettingsMenuUI.ButtonStyle, GUILayout.Height(40f)))
            {
                QuitApplication();
            }

            GUILayout.EndArea();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
