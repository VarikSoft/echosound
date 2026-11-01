using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;

namespace EchoSound.Testing
{
    /// <summary>Lab controls: identical pulses for comparison and optional normal lighting.</summary>
    public sealed class AcousticComparison : MonoBehaviour
    {
        public SoundEmitter[] emitters;
        public EcholocationEffect effect;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E)) EmitComparison();
            if (Input.GetKeyDown(KeyCode.F) && effect != null) effect.enabled = !effect.enabled;
        }

        [ContextMenu("Emit comparison pulses")]
        public void EmitComparison()
        {
            foreach (SoundEmitter emitter in emitters)
                if (emitter != null) emitter.Emit();
        }
    }
}
