using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Attach to a throwable rigidbody to turn landings and collisions into sound pulses.
    /// Optionally triggers audible collision sound cues via SurfaceAudioProfile or a default impact cue.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [AddComponentMenu("EchoSound/Sound/Impact Sound Emitter")]
    public sealed class ImpactSoundEmitter : MonoBehaviour
    {
        [Min(0f)] public float minimumSpeed = 0.8f;
        [Min(1f)] public float fullStrengthSpeed = 10f;
        [Min(1f)] public float maximumRadius = 24f;
        [Min(0.05f)] public float cooldown = 0.15f;

        [Header("Audio Playback (Optional)")]
        [Tooltip("Surface audio profile to look up material-specific impact sound cues.")]
        public SurfaceAudioProfile surfaceAudio;

        [Tooltip("Direct fallback sound cue if no surface audio is configured.")]
        public SoundCue defaultImpactCue;

        private float nextImpact;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextImpact || collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < minimumSpeed) return;
            nextImpact = Time.time + cooldown;
            float strength = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullStrengthSpeed), speed);
            Vector3 impactPoint = contact.point + contact.normal * 0.03f;
            float contactGain = AcousticSurface.ContactGain(collision.collider);

            SoundBus.Emit(impactPoint,
                Mathf.Lerp(3f, maximumRadius, strength), Mathf.Lerp(0.25f, 1.8f, strength) * contactGain,
                SoundType.Impact, gameObject);

            PlayImpactAudio(collision.collider, impactPoint);
        }

        private void PlayImpactAudio(Collider hitCollider, Vector3 point)
        {
            SoundCue cue = null;
            if (surfaceAudio != null)
            {
                AcousticSurface surface = hitCollider != null ? AcousticSurface.Find(hitCollider) : null;
                AcousticMaterialKind kind = surface != null && surface.profile != null ? surface.profile.kind : AcousticMaterialKind.Unmarked;
                cue = surfaceAudio.GetImpactCue(kind);
            }

            if (cue == null)
            {
                cue = defaultImpactCue;
            }

            if (cue != null)
            {
                AudioManager.Instance.Play(cue, point);
            }
        }
    }
}
