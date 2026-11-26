using System.Collections.Generic;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// Something the player or the companion uses to turn a signal on: a button, a lever.
    /// It either toggles, or springs back after a few seconds, which makes "both at once" puzzles possible.
    /// Subclasses only name the action and show the state.
    /// </summary>
    [RequireComponent(typeof(Signal))]
    public abstract class SignalSwitch : Interactable
    {
        [Tooltip("Seconds until the switch springs back. 0 means it toggles and stays until used again.")]
        [Min(0)] public float onSeconds;
        [Tooltip("The moving part, for example the button cap or the lever handle.")]
        public Transform handle;
        [Min(0.5f)] public float standDistance = 1.1f;

        private InteractionAction[] actions;
        private Signal output;
        private float offTime;

        public Signal Output => output != null ? output : output = GetComponent<Signal>();
        public override IReadOnlyList<InteractionAction> Actions => actions ??= new[] { Action };
        public override string State => !Output.IsOn ? OffState
            : onSeconds > 0 ? $"{OnState}, springs back in {Mathf.CeilToInt(offTime - Time.time)} s" : OnState;
        public override IEnumerable<Vector3> StandPoints => PuzzleStandPoints.Around(transform, standDistance);

        /// <summary>The one verb this switch answers to, for example Press.</summary>
        protected abstract InteractionAction Action { get; }
        protected abstract string OnState { get; }
        protected abstract string OffState { get; }
        /// <summary>Puts the handle into the on or off pose.</summary>
        protected abstract void ShowState(Transform handle, bool on);

        protected virtual void Awake()
        {
            Output.Changed += OnChanged;
            OnChanged(Output.IsOn);
        }

        protected virtual void OnDestroy()
        {
            if (output != null) output.Changed -= OnChanged;
        }

        protected override bool CanPerform(InteractionAction action, out string problem)
        {
            problem = onSeconds > 0 && Output.IsOn ? $"it is already {OnState}" : null;
            return problem == null;
        }

        protected override void Perform(Interactor user, InteractionAction action)
        {
            offTime = Time.time + onSeconds;
            Output.Set(onSeconds > 0 || !Output.IsOn);
        }

        private void Update()
        {
            if (onSeconds > 0 && Output.IsOn && Time.time >= offTime) Output.Set(false);
        }

        private void OnChanged(bool on)
        {
            if (handle != null) ShowState(handle, on);
        }
    }
}
