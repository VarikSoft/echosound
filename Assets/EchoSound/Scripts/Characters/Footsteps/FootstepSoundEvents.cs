using EchoSound.Sound;
using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>
    /// Times the player's gameplay sound events (echo pulses, enemy hearing) by the animated foot
    /// contacts, so pulses match the audible steps. Radii and loudness stay in PlayerSoundEmitter.
    /// </summary>
    [RequireComponent(typeof(PlayerSoundEmitter))]
    public sealed class FootstepSoundEvents : MonoBehaviour
    {
        private PlayerSoundEmitter emitter;
        private IFootstepSource source;

        private void Awake() => emitter = GetComponent<PlayerSoundEmitter>();

        private void OnEnable()
        {
            source = GetComponentInParent<IFootstepSource>();
            if (source == null) source = GetComponentInChildren<IFootstepSource>();
            if (source == null)
            {
                Debug.LogWarning("FootstepSoundEvents found no IFootstepSource; keeping distance-based steps.", this);
                return;
            }
            source.Footstep += Emit;
            emitter.ExternalTiming = true;
        }

        private void OnDisable()
        {
            if (source != null) source.Footstep -= Emit;
            // Fall back to the emitter's own timing.
            if (emitter != null) emitter.ExternalTiming = false;
        }

        private void Emit(FootstepEvent step)
        {
            switch (step.Kind)
            {
                case FootstepKind.Walk: emitter.EmitFootstep(false); break;
                case FootstepKind.Run: emitter.EmitFootstep(true); break;
                case FootstepKind.Jump: emitter.EmitJump(); break;
                case FootstepKind.Land: emitter.EmitLanding(step.ImpactSpeed); break;
            }
        }
    }
}
