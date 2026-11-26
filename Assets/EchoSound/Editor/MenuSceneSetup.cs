using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoSound.UI;

namespace EchoSound.Editor
{
    /// <summary>
    /// Creates MainMenu.unity if missing and ensures MainMenu + GenerationTest are registered in Build Settings.
    /// </summary>
    public static class MenuSceneSetup
    {
        private const string MainMenuScenePath = "Assets/EchoSound/Scenes/MainMenu.unity";
        private const string LevelScenePath = "Assets/EchoSound/Scenes/GenerationTest.unity";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    EnsureMainMenuAndBuildSettings();
                }
            };
        }

        [MenuItem("EchoSound/UI/Setup Main Menu Scene and Build Settings")]
        public static void EnsureMainMenuAndBuildSettings()
        {
            if (!File.Exists(MainMenuScenePath))
            {
                string folder = Path.GetDirectoryName(MainMenuScenePath);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 1f);
                    camGo.AddComponent<AudioListener>();
                    SceneManager.MoveGameObjectToScene(camGo, menuScene);

                    var menuGo = new GameObject("MainMenu");
                    var controller = menuGo.AddComponent<MainMenuController>();
                    controller.levelSceneName = "GenerationTest";
                    SceneManager.MoveGameObjectToScene(menuGo, menuScene);

                    EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
                    AssetDatabase.Refresh();
                    Debug.Log("[MenuSceneSetup] Created MainMenu scene at " + MainMenuScenePath);
                }
                finally
                {
                    EditorSceneManager.CloseScene(menuScene, true);
                }
            }

            UpdateBuildSettings();
        }

        private static void UpdateBuildSettings()
        {
            var desired = new List<string> { MainMenuScenePath, LevelScenePath };
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var updated = new List<EditorBuildSettingsScene>();

            foreach (string path in desired)
            {
                if (File.Exists(path))
                {
                    updated.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            foreach (var scene in existing)
            {
                if (!desired.Contains(scene.path))
                {
                    updated.Add(scene);
                }
            }

            bool changed = updated.Count != existing.Count;
            if (!changed)
            {
                for (int i = 0; i < updated.Count; i++)
                {
                    if (updated[i].path != existing[i].path || updated[i].enabled != existing[i].enabled)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                EditorBuildSettings.scenes = updated.ToArray();
                Debug.Log("[MenuSceneSetup] Updated EditorBuildSettings with MainMenu and GenerationTest.");
            }
        }
    }
}
