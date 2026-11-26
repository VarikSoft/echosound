using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// Moves and turns an object between its rest pose and an "on" pose while the signal is on:
    /// a beam lowering over a pit, a barrier sliding up, a platform rising.
    /// </summary>
    public sealed class SignalMover : SignalReceiver
    {
        [Tooltip("What moves. Empty means this object.")]
        public Transform target;
        [Tooltip("Local offset from the rest position when the signal is on.")]
        public Vector3 onOffset;
        [Tooltip("Local rotation added to the rest rotation when the signal is on, in degrees.")]
        public Vector3 onRotation;
        [Min(0.05f)] public float seconds = 1;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool hasRestPose;
        // 0 is the rest pose, 1 is the on pose.
        private float amount;
        private float goal;

        protected override void OnSignal(bool on)
        {
            RememberRestPose();
            goal = on ? 1 : 0;
        }

        private void Update()
        {
            if (amount == goal) return;
            amount = Mathf.MoveTowards(amount, goal, Time.deltaTime / seconds);
            target.localPosition = restPosition + onOffset * amount;
            target.localRotation = restRotation * Quaternion.Euler(onRotation * amount);
        }

        private void RememberRestPose()
        {
            if (hasRestPose) return;
            if (target == null) target = transform;
            restPosition = target.localPosition;
            restRotation = target.localRotation;
            hasRestPose = true;
        }
    }
}
