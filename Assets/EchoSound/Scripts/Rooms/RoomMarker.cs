using UnityEngine;

namespace EchoSound.Rooms
{
    public enum RoomMarkerKind
    {
        Secret,
        Reward,
        PressurePlate,
        Clue,
        Mechanism,
        MonsterSpawn,
        AmbushTrigger
    }

    /// <summary>
    /// An editable placement hint for future gameplay systems.
    /// Does not spawn enemies, activate puzzles, or create triggers at runtime.
    /// </summary>
    public sealed class RoomMarker : MonoBehaviour
    {
        public RoomMarkerKind kind;
        [Tooltip("Markers belonging to the same planned puzzle share a group name.")]
        public string group = "";
        [TextArea] public string designerNote = "";
        [Min(0.1f)] public float previewRadius = 0.4f;

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == RoomMarkerKind.MonsterSpawn || kind == RoomMarkerKind.AmbushTrigger
                ? new Color(1, 0.3f, 0.2f)
                : new Color(0.8f, 0.6f, 1);
            Gizmos.DrawWireSphere(transform.position, previewRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, name);
#endif
        }
    }
}
