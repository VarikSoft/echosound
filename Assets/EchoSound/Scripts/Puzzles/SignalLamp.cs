using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>Shows a signal by colour, for example a light over a solved door.</summary>
    public sealed class SignalLamp : SignalReceiver
    {
        [Tooltip("Empty means the renderer on this object.")]
        public Renderer lamp;
        public Color offColor = new Color(0.35f, 0.05f, 0.05f);
        public Color onColor = new Color(0.2f, 1f, 0.3f);

        protected override void OnSignal(bool on)
        {
            if (lamp == null) lamp = GetComponent<Renderer>();
            // .material makes a per-lamp copy, so lamps sharing a material do not change together.
            if (lamp != null) lamp.material.color = on ? onColor : offColor;
        }
    }
}
