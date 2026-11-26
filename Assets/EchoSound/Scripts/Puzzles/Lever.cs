using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>A lever: pulling tilts the handle from forward to back around its local X axis.</summary>
    public sealed class Lever : SignalSwitch
    {
        [Range(0, 90)] public float angle = 40;

        private Quaternion restRotation;

        public override string Kind => "lever";
        protected override InteractionAction Action => InteractionAction.Pull;
        protected override string OnState => "pulled";
        protected override string OffState => "not pulled";

        protected override void Awake()
        {
            if (handle != null) restRotation = handle.localRotation;
            base.Awake();
        }

        protected override void ShowState(Transform lever, bool on)
        {
            lever.localRotation = restRotation * Quaternion.Euler(on ? -angle : angle, 0, 0);
        }
    }
}
