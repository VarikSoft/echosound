using UnityEngine;

namespace EchoSound.Rooms
{
    /// <summary>
    /// Metadata attached to a room prefab. Geometry remains ordinary editable scene objects.
    /// Door markers describe connections; the anchor defines a safe place to spawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomTemplate : MonoBehaviour
    {
        public string displayName = "Room";
        [Min(4)] public float footprint = 12;
        [Tooltip("Place this on a safe walking surface, not above a pit.")]
        public Transform anchor;

        public Vector3 AnchorPosition => anchor != null ? anchor.position : transform.position;

        public DoorMask Doors
        {
            get
            {
                DoorMask result = DoorMask.None;
                foreach (RoomDoor door in GetComponentsInChildren<RoomDoor>())
                {
                    result |= DoorLayout.Bit((int)door.side);
                }
                return result;
            }
        }

        public bool Validate(float expectedSize, out string error)
        {
            error = null;
            if (Mathf.Abs(footprint - expectedSize) > 0.01f || transform.localScale != Vector3.one)
            {
                error = "Footprint must match the catalog and root scale must be (1,1,1).";
                return false;
            }

            DoorMask foundDoors = DoorMask.None;
            foreach (RoomDoor door in GetComponentsInChildren<RoomDoor>())
            {
                int direction = (int)door.side;
                if (direction < 0 || direction > 3)
                {
                    error = "Invalid door direction.";
                    return false;
                }

                DoorMask doorBit = DoorLayout.Bit(direction);
                if ((foundDoors & doorBit) != 0)
                {
                    error = "Only one door per side is supported.";
                    return false;
                }
                foundDoors |= doorBit;

                Vector3 localPosition = transform.InverseTransformPoint(door.transform.position);
                Vector3 expectedPosition = DoorLayout.Position(door.side, footprint);
                if (Vector3.Distance(localPosition, expectedPosition) > 0.05f)
                {
                    error = "Door markers must sit at side centres, at floor height.";
                    return false;
                }
                if (door.width < 1.2f || door.height < 2)
                {
                    error = "Door clearance must be at least 1.2 m wide and 2 m high.";
                    return false;
                }
            }

            if (foundDoors == DoorMask.None)
            {
                error = "Add at least one RoomDoor marker.";
                return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1);
            Gizmos.DrawWireCube(new Vector3(0, 1.5f, 0), new Vector3(footprint, 3, footprint));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(AnchorPosition + Vector3.up * 0.2f, 0.3f);
        }
    }
}
