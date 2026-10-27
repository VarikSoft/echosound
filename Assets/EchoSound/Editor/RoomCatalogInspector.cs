using UnityEditor;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Editor
{
    [CustomEditor(typeof(RoomCatalog))]
    public sealed class RoomCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Add prefab variants here. Weight 0 excludes an entry; higher weights make a compatible variant more likely. Keep the five basic door shapes available.", MessageType.Info);
            if (GUILayout.Button("Validate Catalog"))
            {
                try { ((RoomCatalog)target).ValidateOrThrow(); Debug.Log("Room catalog is valid.", target); }
                catch (System.Exception exception) { Debug.LogException(exception, target); }
            }
        }
    }
}
