using System;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Core health management component for players, companions, and monsters.
    /// Tracks current/max health, damage taking with i-frames, healing, and hurt/death audio triggers.
    /// </summary>
    [AddComponentMenu("EchoSound/Combat/Health Component")]
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [Min(1f)] public float maxHealth = 100f;
        public float currentHealth;

        [Tooltip("Invulnerability duration in seconds after taking damage.")]
        public float invulnerabilityDuration = 0.4f;

        [Header("Audio Playback (Optional)")]
        public SoundCue hurtCue;
        public SoundCue deathCue;

        public bool IsAlive => currentHealth > 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<DamageInfo> OnDamageTaken;
        public event Action<float> OnHealed;
        public event Action OnDeath;

        private float nextDamageTime;

        protected virtual void Awake()
        {
            if (currentHealth <= 0f) currentHealth = maxHealth;
        }

        public virtual void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive || Time.time < nextDamageTime || damage.amount <= 0f) return;

            nextDamageTime = Time.time + invulnerabilityDuration;
            currentHealth = Mathf.Max(0f, currentHealth - damage.amount);

            if (hurtCue != null)
            {
                AudioManager.Instance.Play(hurtCue, transform.position);
            }

            OnDamageTaken?.Invoke(damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public virtual void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public virtual void SetHealth(float newHealth)
        {
            currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth <= 0f) Die();
        }

        protected virtual void Die()
        {
            if (deathCue != null)
            {
                AudioManager.Instance.Play(deathCue, transform.position);
            }
            OnDeath?.Invoke();
        }
    }
}
