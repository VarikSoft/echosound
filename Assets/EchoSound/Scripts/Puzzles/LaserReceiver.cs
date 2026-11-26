using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>On in every frame a laser beam hits it.</summary>
    [RequireComponent(typeof(Signal))]
    public sealed class LaserReceiver : MonoBehaviour
    {
        private Signal output;
        private int litFrame = -1;

        public Signal Output => output != null ? output : output = GetComponent<Signal>();

        /// <summary>Called by a LaserEmitter whose beam ends here, once per frame.</summary>
        public void Illuminate() => litFrame = Time.frameCount;

        // Emitters cast in Update, so by LateUpdate every beam of this frame is known.
        private void LateUpdate() => Output.Set(litFrame == Time.frameCount);
    }
}
