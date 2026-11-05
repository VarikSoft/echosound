using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Component that can be placed on interactive objects (doors, levers, traps, cameras, projectiles)
    /// to emit sounds into the SoundBus. Can be triggered from scripts, Animation Events, or UnityEvents.
    /// </summary>
    [AddComponentMenu("EchoSound/Sound/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        [Header("Sound Properties")]
        [Tooltip("Type of sound emitted.")]
        public SoundType soundType = SoundType.Interaction;

        [Tooltip("Max hearing / illumination distance in meters.")]
        [Min(0.1f)]
        public float radius = 8f;

        [Tooltip("Loudness multiplier (1.0 = normal).")]
        [Range(0f, 2f)]
        public float intensity = 1f;

        [Tooltip("Transform used as origin point for emitted sounds. If null, this GameObject's transform is used.")]
        public Transform emissionPoint;

        public Vector3 SoundPosition => emissionPoint != null ? emissionPoint.position : transform.position;

        /// <summary>
        /// Emits a sound using the configured properties on this component.
        /// </summary>
        public void Emit()
        {
            SoundBus.Emit(SoundPosition, radius, intensity, soundType, gameObject);
        }

        /// <summary>
        /// Emits a sound with a custom radius override.
        /// </summary>
        public void EmitWithRadius(float customRadius)
        {
            SoundBus.Emit(SoundPosition, customRadius, intensity, soundType, gameObject);
        }

        /// <summary>
        /// Emits a sound with an explicit SoundType override.
        /// </summary>
        public void EmitWithType(SoundType overrideType)
        {
            SoundBus.Emit(SoundPosition, radius, intensity, overrideType, gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(SoundPosition, radius);
        }
    }
}
