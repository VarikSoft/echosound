using UnityEditor;
using UnityEditor.SceneManagement;

namespace EchoSound.Editor
{
    /// <summary>Shortcuts for opening the two authoring and testing scenes.</summary>
    public static class GenerationTools
    {
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
