using UnityEditor;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Editor
{
    [CustomEditor(typeof(RoomTemplate))]
    public sealed class RoomTemplateInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var room = (RoomTemplate)target;
            EditorGUILayout.LabelField("Detected doors", room.Doors.ToString());
            EditorGUILayout.HelpBox("Edit Walls and Floor as normal objects. Each physical opening needs a RoomDoor child at its side centre. Enable Gizmos to see connectors. Apply scene overrides to the prefab to use edits in generation.", MessageType.Info);
            if (!room.Validate(room.footprint, out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
        void OnSceneGUI()
        {
            var room = (RoomTemplate)target;
            Handles.Label(room.transform.position + Vector3.up * 3.5f, room.displayName + " / " + room.Doors);
        }
    }
}
