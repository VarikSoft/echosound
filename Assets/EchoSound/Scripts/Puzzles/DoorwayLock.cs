using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// Marks a room doorway whose door stays locked until a signal turns on, for example the exit of a puzzle room.
    /// Put it on the room's RoomDoor marker. Doors are spawned per level, so the code that spawns them adds the DoorLock.
    /// </summary>
    public sealed class DoorwayLock : MonoBehaviour
    {
        public Signal unlockedBy;
    }
}
