using System;
using UnityEngine;

namespace EchoSound.Rooms
{
    public enum DoorSide { North, East, South, West }

    // Four independent flags: a room can have more than one doorway.
    [Flags]
    public enum DoorMask
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    /// <summary>Shared direction conventions used by the layout and prefab matching.</summary>
    public static class DoorLayout
    {
        // Clockwise order matches positive 90-degree rotations around Unity's Y axis.
        public static readonly Vector2Int[] Steps =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        public static DoorMask Bit(int side) => (DoorMask)(1 << side);
        public static int Opposite(int side) => (side + 2) % 4;

        public static DoorMask Rotate(DoorMask mask, int quarterTurns)
        {
            DoorMask rotated = DoorMask.None;
            for (int side = 0; side < 4; side++)
            {
                if ((mask & Bit(side)) == 0) continue;
                int rotatedSide = (side + quarterTurns) % 4;
                rotated |= Bit(rotatedSide);
            }
            return rotated;
        }

        public static Vector3 Position(DoorSide side, float footprint)
        {
            Vector2Int step = Steps[(int)side];
            return new Vector3(step.x, 0, step.y) * footprint * 0.5f;
        }
    }
}
