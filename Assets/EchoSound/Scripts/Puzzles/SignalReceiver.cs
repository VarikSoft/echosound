using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>Base for anything that reacts to one signal: a door lock, a mover, a lamp.</summary>
    public abstract class SignalReceiver : MonoBehaviour
    {
        [SerializeField] private Signal input;

        /// <summary>The signal to follow. Can be changed at runtime, for example by code that builds a level.</summary>
        public Signal Input
        {
            get => input;
            set
            {
                if (isActiveAndEnabled) Unsubscribe();
                input = value;
                if (isActiveAndEnabled) Subscribe();
            }
        }

        /// <summary>Called on every change, and once when enabled, so the receiver starts in the right state.</summary>
        protected abstract void OnSignal(bool on);

        protected virtual void OnEnable() => Subscribe();
        protected virtual void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (input == null) return;
            input.Changed += OnSignal;
            OnSignal(input.IsOn);
        }

        private void Unsubscribe()
        {
            if (input != null) input.Changed -= OnSignal;
        }
    }
}
