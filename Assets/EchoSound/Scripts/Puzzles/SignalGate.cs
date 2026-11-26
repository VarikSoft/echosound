using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// Combines several signals into one: all of them (two levers at once), any of them, or an odd number
    /// of them (two-way switches: every switch flips the output, like a light switch at each end of a corridor).
    /// Its output is a signal too, so gates can feed other gates.
    /// </summary>
    [RequireComponent(typeof(Signal))]
    public sealed class SignalGate : MonoBehaviour
    {
        public enum Rule { All, Any, Odd }

        public Rule rule = Rule.All;
        [SerializeField] private List<Signal> inputs = new List<Signal>();
        [Tooltip("Once on, stay on for good: the puzzle counts as solved.")]
        public bool latch;

        private Signal output;

        public Signal Output => output != null ? output : output = GetComponent<Signal>();

        public void AddInput(Signal signal)
        {
            inputs.Add(signal);
            if (isActiveAndEnabled) signal.Changed += Refresh;
            Refresh(signal.IsOn);
        }

        private void OnEnable()
        {
            foreach (Signal signal in inputs) if (signal != null) signal.Changed += Refresh;
            Refresh(false);
        }

        private void OnDisable()
        {
            foreach (Signal signal in inputs) if (signal != null) signal.Changed -= Refresh;
        }

        private void Refresh(bool _)
        {
            if (latch && Output.IsOn) return;
            int onCount = inputs.FindAll(signal => signal != null && signal.IsOn).Count;
            bool on = rule switch
            {
                Rule.All => inputs.Count > 0 && onCount == inputs.Count,
                Rule.Any => onCount > 0,
                _ => onCount % 2 == 1
            };
            Output.Set(on);
        }
    }
}
