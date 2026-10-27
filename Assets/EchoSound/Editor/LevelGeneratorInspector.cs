using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoSound.Generation;
using EchoSound.Rooms;

namespace EchoSound.Editor
{
    [CustomEditor(typeof(LevelGenerator))]
    public sealed class LevelGeneratorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var generator = (LevelGenerator)target;
            EditorGUILayout.HelpBox("Generate Preview places real prefab instances in the scene. Edit a room prefab to change every future instance. Preview changes are replaced on regeneration.", MessageType.Info);
            if (GUILayout.Button("Generate Preview / Same Seed")) Generate(generator, generator.seed);
            if (GUILayout.Button("Generate Next Seed")) Generate(generator, unchecked(generator.seed + 1));
            if (GUILayout.Button("Clear Generated Rooms"))
            {
                generator.Clear();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
        static void Generate(LevelGenerator generator, int seed)
        {
            try
            {
                generator.Generate(seed);
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
            catch (System.Exception exception) { Debug.LogException(exception, generator); }
        }
    }
}
