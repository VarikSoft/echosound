using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Combat and perception tuning for monsters/stalkers.
    /// Handles attack power, attack ranges, detection hearing sensitivity,
    /// and attacking damageable targets.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    [AddComponentMenu("EchoSound/Combat/Monster Stats")]
    public class MonsterStats : MonoBehaviour
    {
        [Header("Monster Identity")]
        public string monsterName = "Stalker";

        [Header("Attack Attributes")]
        [Min(1f)] public float attackDamage = 25f;
        [Min(0.5f)] public float attackRange = 2.0f;
        [Min(0.2f)] public float attackCooldown = 1.2f;

        [Header("Perception & Acoustics")]
        [Tooltip("Multiplier applied to SoundBus acoustic wave hearing range.")]
        [Min(0.1f)] public float hearingSensitivity = 1.25f;

        [Header("Audio")]
        public SoundCue attackCue;
        public SoundCue alertCue;

        public HealthComponent Health { get; private set; }
        public bool IsAlive => Health != null && Health.IsAlive;

        private float nextAttackTime;

        private void Awake()
        {
            Health = GetComponent<HealthComponent>();
            EnsureDefaultAudio();
        }

        public void EnsureDefaultAudio()
        {
            if (attackCue == null) attackCue = Resources.Load<SoundCue>("Audio/Monsters/Stalker_Attack");
            if (alertCue == null) alertCue = Resources.Load<SoundCue>("Audio/Monsters/Stalker_Alert");
        }

        public bool CanAttack(Vector3 targetPosition)
        {
            if (!IsAlive || Time.time < nextAttackTime) return false;
            float distSq = (targetPosition - transform.position).sqrMagnitude;
            return distSq <= (attackRange * attackRange);
        }

        public bool TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || !CanAttack(target is Component comp ? comp.transform.position : transform.position))
                return false;

            nextAttackTime = Time.time + attackCooldown;

            if (attackCue != null)
            {
                AudioManager.Instance.Play(attackCue, transform.position);
            }

            var damage = new DamageInfo(attackDamage, DamageType.MonsterBite, gameObject, transform.position);
            target.TakeDamage(damage);
            return true;
        }

        public void PlayAlertSound()
        {
            if (alertCue != null)
            {
                AudioManager.Instance.Play(alertCue, transform.position);
            }
        }
    }
}
