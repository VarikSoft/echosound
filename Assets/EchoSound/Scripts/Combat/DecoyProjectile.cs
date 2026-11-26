using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Thrown decoy (keys) that emits a loud SoundEvent and 3D SoundCue on impact
    /// to lure blind StalkerController monsters away from the player.
    /// </summary>
    [AddComponentMenu("EchoSound/Combat/Decoy Projectile")]
    public class DecoyProjectile : MonoBehaviour
    {
        [Header("Acoustic Lure")]
        [Min(2f)] public float noiseRadius = 24f;
        [Range(0.1f, 1.5f)] public float noiseIntensity = 1.0f;
        [Min(1)] public int maxImpactEmissions = 2;
        [Min(0.05f)] public float impactCooldown = 0.25f;
        [Min(1f)] public float lifetime = 6f;

        [Header("Audio")]
        public SoundCue impactCue;

        private static SoundCue defaultImpactCue;
        private int emissionCount;
        private float nextImpactTime;

        private void Awake()
        {
            if (impactCue == null)
            {
                if (defaultImpactCue == null)
                {
                    defaultImpactCue = Resources.Load<SoundCue>("Audio/Player/Decoy_Keys_Impact");
                }
                impactCue = defaultImpactCue;
            }
            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (emissionCount >= maxImpactEmissions || Time.time < nextImpactTime) return;

            emissionCount++;
            nextImpactTime = Time.time + impactCooldown;

            Vector3 hitPos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            float intensityScale = emissionCount == 1 ? 1.0f : 0.6f;

            if (impactCue != null)
            {
                AudioManager.Instance.Play(impactCue, hitPos);
            }

            SoundBus.Emit(hitPos, noiseRadius * intensityScale, noiseIntensity * intensityScale, SoundType.Impact, gameObject);
        }
    }
}
