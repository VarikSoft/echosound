using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>Keeps a door locked while its signal is off. Optionally opens and closes the door with the signal.</summary>
    public sealed class DoorLock : SignalReceiver
    {
        public Door door;
        [Tooltip("Open the door when the signal turns on and close it when the signal turns off.")]
        public bool driveDoor = true;

        // Set when the door could not close because someone stood in the doorway.
        private bool closePending;

        protected override void OnSignal(bool on)
        {
            if (door == null) door = GetComponent<Door>();
            if (door == null) return;

            door.IsLocked = !on;
            closePending = false;
            if (driveDoor) closePending = !door.SetOpen(on);
        }

        private void Update()
        {
            // Retry until the doorway is clear.
            if (closePending) closePending = !door.SetOpen(false);
        }
    }
}
