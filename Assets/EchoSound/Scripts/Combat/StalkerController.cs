using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Acoustic-driven monster controller that listens to SoundBus events,
    /// investigates noises, chases targets using optional StaminaComponent,
    /// and attacks IDamageable targets via MonsterStats.
    /// </summary>
    [RequireComponent(typeof(MonsterStats))]
    [AddComponentMenu("EchoSound/Combat/Stalker Controller")]
    public class StalkerController : MonoBehaviour, ISoundListener
    {
        public enum State
        {
            Idle,
            Investigate,
            Chase
        }

        [Header("Movement")]
        [Min(0.1f)] public float investigateSpeed = 2.6f;
        [Min(0.1f)] public float chaseSpeed = 4.8f;
        [Min(0.1f)] public float turnSpeed = 8f;
        [Min(0.5f)] public float investigationTimeout = 6f;
        [Min(0.5f)] public float chaseMemoryDuration = 5f;
        [Min(0.5f)] public float proximityDetectionRange = 4.5f;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private State currentState = State.Idle;

        public State CurrentState => currentState;
        public Vector3 ListenerPosition => transform.position;
        public float HearingSensitivity => stats != null ? stats.hearingSensitivity : 1f;

        private MonsterStats stats;
        private StaminaComponent stamina;
        private CharacterController controller;

        private Vector3 targetPosition;
        private Transform chaseTarget;
        private IDamageable chaseDamageable;
        private float stateExpireTime;

        private void Awake()
        {
            stats = GetComponent<MonsterStats>();
            stamina = GetComponent<StaminaComponent>();
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            SoundBus.RegisterListener(this);
        }

        private void OnDisable()
        {
            SoundBus.UnregisterListener(this);
        }

        public void OnSoundHeard(in SoundEvent soundEvent, float distance)
        {
            if (stats == null || !stats.IsAlive) return;
            if (soundEvent.Source == gameObject) return;
            if (soundEvent.IsReflection && currentState == State.Chase) return;

            // Ignore other monsters' sounds if they have MonsterStats
            if (soundEvent.Source != null && soundEvent.Source.GetComponent<MonsterStats>() != null)
                return;

            var heardDamageable = soundEvent.Source != null ? soundEvent.Source.GetComponent<IDamageable>() : null;
            if (heardDamageable != null && heardDamageable.IsAlive)
            {
                bool wasIdle = currentState == State.Idle;
                chaseTarget = soundEvent.Source.transform;
                chaseDamageable = heardDamageable;
                targetPosition = soundEvent.Position;
                currentState = State.Chase;
                stateExpireTime = Time.time + chaseMemoryDuration;
                if (wasIdle) stats.PlayAlertSound();
            }
            else if (currentState != State.Chase)
            {
                bool wasIdle = currentState == State.Idle;
                targetPosition = soundEvent.Position;
                currentState = State.Investigate;
                stateExpireTime = Time.time + investigationTimeout;
                if (wasIdle) stats.PlayAlertSound();
            }
        }

        private void Update()
        {
            if (stats == null || !stats.IsAlive) return;

            if (chaseTarget != null)
            {
                if (chaseDamageable == null || !chaseDamageable.IsAlive)
                {
                    ClearChase();
                }
                else
                {
                    float dist = Vector3.Distance(transform.position, chaseTarget.position);
                    if (dist <= proximityDetectionRange)
                    {
                        targetPosition = chaseTarget.position;
                        stateExpireTime = Time.time + chaseMemoryDuration;
                    }

                    if (stats.CanAttack(chaseTarget.position))
                    {
                        stats.TryAttack(chaseDamageable);
                    }
                }
            }

            if (currentState == State.Idle) return;

            if (Time.time > stateExpireTime)
            {
                ClearChase();
                return;
            }

            MoveTowardTarget();
        }

        private void MoveTowardTarget()
        {
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float sqrDist = toTarget.sqrMagnitude;

            float stopDist = currentState == State.Chase ? Mathf.Max(0.5f, stats.attackRange * 0.8f) : 0.6f;
            if (sqrDist <= stopDist * stopDist)
            {
                if (currentState == State.Investigate)
                {
                    currentState = State.Idle;
                }
                return;
            }

            Vector3 dir = toTarget.normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            float speed = investigateSpeed;
            if (currentState == State.Chase)
            {
                bool canSprint = stamina == null || stamina.CanSprint;
                if (canSprint)
                {
                    speed = chaseSpeed;
                    if (stamina != null) stamina.ConsumeSprint(Time.deltaTime);
                }
            }

            Vector3 motion = dir * speed * Time.deltaTime;
            if (controller != null && controller.enabled)
            {
                motion.y = -9.81f * Time.deltaTime;
                controller.Move(motion);
            }
            else
            {
                transform.position += motion;
            }
        }

        private void ClearChase()
        {
            currentState = State.Idle;
            chaseTarget = null;
            chaseDamageable = null;
        }
    }
}
