using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>A button: pressing sinks the cap.</summary>
    public sealed class PushButton : SignalSwitch
    {
        [Min(0)] public float pressDepth = 0.04f;

        private Vector3 restPosition;

        public override string Kind => "button";
        protected override InteractionAction Action => InteractionAction.Press;
        protected override string OnState => "pressed";
        protected override string OffState => "not pressed";

        protected override void Awake()
        {
            if (handle != null) restPosition = handle.localPosition;
            base.Awake();
        }

        protected override void ShowState(Transform cap, bool on)
        {
            cap.localPosition = restPosition + (on ? Vector3.down * pressDepth : Vector3.zero);
        }
    }
}
