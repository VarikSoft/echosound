using UnityEngine;

namespace EchoSound.Rooms
{
    public sealed class RoomDoor : MonoBehaviour
    {
        [Tooltip("Direction in the prefab's local coordinates, before generator rotation.")]
        public DoorSide side;
        public float width = 3;
        public float height = 2.6f;
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * height * 0.5f, new Vector3(width, height, 0.1f));
            Gizmos.DrawLine(Vector3.up, Vector3.up + Vector3.forward * 1.5f);
            Gizmos.DrawLine(Vector3.up + Vector3.forward * 1.5f, new Vector3(0.3f, 1, 1));
            Gizmos.DrawLine(Vector3.up + Vector3.forward * 1.5f, new Vector3(-0.3f, 1, 1));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
