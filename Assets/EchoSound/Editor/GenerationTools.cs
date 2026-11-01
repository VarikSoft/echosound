using UnityEditor;
using UnityEditor.SceneManagement;

namespace EchoSound.Editor
{
    /// <summary>Shortcuts for opening the two authoring and testing scenes.</summary>
    public static class GenerationTools
    {
        [MenuItem("EchoSound/Hide ceilings in Scene view")]
        public static void HideCeilings() => SetCeilingVisibility(true);

        [MenuItem("EchoSound/Show ceilings in Scene view")]
        public static void ShowCeilings() => SetCeilingVisibility(false);

        private static void SetCeilingVisibility(bool hidden)
        {
            var ceilings = UnityEngine.Object.FindObjectsByType<Rooms.RoomCeiling>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            foreach (var ceiling in ceilings)
            {
                if (hidden) SceneVisibilityManager.instance.Hide(ceiling.gameObject, true);
                else SceneVisibilityManager.instance.Show(ceiling.gameObject, true);
            }
        }

        [MenuItem("EchoSound/Open acoustic material test")]
        public static void OpenAcousticTest()
        {
            OpenScene("Assets/EchoSound/Scenes/AcousticMaterialsTest.unity");
        }

        [MenuItem("EchoSound/Open generation test")]
        public static void OpenTestScene()
        {
            OpenScene("Assets/EchoSound/Scenes/GenerationTest.unity");
        }

        [MenuItem("EchoSound/Open room showcase")]
        public static void OpenShowcase()
        {
            OpenScene("Assets/EchoSound/Scenes/RoomShowcase.unity");
        }

        private static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }
    }
}
