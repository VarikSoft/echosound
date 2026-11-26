using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Blind, acoustic-driven monster controller.
    /// Patrols randomly while emitting red SoundType.Enemy echolocation waves.
    /// Accumulates sound wave hits (4-5 hits) to pinpoint the player's last wave position and hunt there.
    /// Cannot detect or kill a completely silent player.
    /// </summary>
    [RequireComponent(typeof(MonsterStats))]
    [AddComponentMenu("EchoSound/Combat/Stalker Controller")]
    public class StalkerController : MonoBehaviour, ISoundListener
    {
        public enum State
        {
            Patrol,
            Investigate,
            Hunt
        }

        [Header("Acoustic Hunting")]
        [Tooltip("Number of sound wave hits required before the monster locks onto the sound origin and hunts.")]
        [Range(1, 10)] public int hitsToLocatePlayer = 4;

        [Tooltip("Seconds without hearing a sound before accumulated sound hits reset.")]
        [Min(1f)] public float soundMemoryDecay = 8f;

        [Tooltip("How long the monster searches at the last heard position before returning to patrol.")]
        [Min(0.5f)] public float huntTimeout = 6f;

        [Header("Movement & Patrol")]
        [Min(0.1f)] public float patrolSpeed = 2.0f;
        [Min(0.1f)] public float investigateSpeed = 2.6f;
        [Min(0.1f)] public float huntSpeed = 4.8f;
        [Min(0.1f)] public float turnSpeed = 7f;
        [Min(2f)] public float patrolRadius = 10f;
        [Min(0.5f)] public float obstacleCheckDistance = 1.1f;

        [Header("Red Echolocation Waves (Enemy Footsteps)")]
        [Tooltip("Radius of red SoundType.Enemy waves emitted as the monster walks.")]
        [Min(1f)] public float stepWaveRadius = 12f;
        [Min(0.15f)] public float patrolWaveInterval = 0.7f;
        [Min(0.15f)] public float huntWaveInterval = 0.4f;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private State currentState = State.Patrol;
        [SerializeField] private int soundHitCount;

        public State CurrentState => currentState;
        public int SoundHitCount => soundHitCount;
        public Vector3 LastHeardPosition => targetPosition;
        public Vector3 ListenerPosition => transform.position;
        public float HearingSensitivity => stats != null ? stats.hearingSensitivity : 1f;

        private MonsterStats stats;
        private StaminaComponent stamina;
        private CharacterController controller;

        private Vector3 spawnOrigin;
        private Vector3 targetPosition;
        private IDamageable targetDamageable;
        private Transform targetTransform;
        private float lastSoundHeardTime = -999f;
        private float stateExpireTime;
        private float nextStepWaveTime;
        private float nextPatrolPickTime;

        private void Awake()
        {
            stats = GetComponent<MonsterStats>();
            stamina = GetComponent<StaminaComponent>();
            controller = GetComponent<CharacterController>();
            spawnOrigin = transform.position;
            PickNewPatrolPoint();
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
            if (soundEvent.Type == SoundType.Enemy) return;
            if (soundEvent.IsReflection) return;

            // Ignore sounds from other monsters
            if (soundEvent.Source != null && soundEvent.Source.GetComponent<MonsterStats>() != null)
                return;

            // Decay old hits if too much silence passed
            if (Time.time - lastSoundHeardTime > soundMemoryDecay)
            {
                soundHitCount = 0;
            }

            soundHitCount++;
            lastSoundHeardTime = Time.time;

            // Record exact position at the moment of this wave (never tracks live position)
            targetPosition = soundEvent.Position;

            var damageable = soundEvent.Source != null ? soundEvent.Source.GetComponent<IDamageable>() : null;
            if (damageable != null && damageable.IsAlive)
            {
                targetDamageable = damageable;
                targetTransform = soundEvent.Source.transform;
            }
            else
            {
                // Lured by a non-living sound source (e.g. thrown decoy keys)
                targetDamageable = null;
                targetTransform = null;
            }

            if (soundHitCount >= hitsToLocatePlayer)
            {
                if (currentState != State.Hunt)
                {
                    stats.PlayAlertSound();
                }
                currentState = State.Hunt;
                stateExpireTime = Time.time + huntTimeout;
            }
            else if (currentState != State.Hunt)
            {
                currentState = State.Investigate;
                stateExpireTime = Time.time + huntTimeout;
            }
        }

        private void Update()
        {
            if (stats == null || !stats.IsAlive) return;

            // Decay sound hit counter after prolonged silence
            if (soundHitCount > 0 && Time.time - lastSoundHeardTime > soundMemoryDecay)
            {
                soundHitCount = 0;
            }

            if (currentState != State.Patrol && Time.time > stateExpireTime)
            {
                ReturnToPatrol();
            }

            // Can ONLY kill if actively hunting a located sound AND target is still near the last heard wave position
            if (currentState == State.Hunt && targetDamageable != null && targetDamageable.IsAlive && targetTransform != null)
            {
                float distToLastSound = Vector3.Distance(targetTransform.position, targetPosition);
                if (distToLastSound <= stats.attackRange + 1.0f && stats.CanAttack(targetTransform.position))
                {
                    stats.TryAttack(targetDamageable);
                }
            }

            UpdateMovement();
        }

        private void UpdateMovement()
        {
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float sqrDist = toTarget.sqrMagnitude;

            float stopDist = currentState == State.Patrol ? 0.7f : Mathf.Max(0.5f, stats.attackRange * 0.7f);
            if (sqrDist <= stopDist * stopDist)
            {
                if (currentState == State.Patrol)
                {
                    if (Time.time >= nextPatrolPickTime)
                    {
                        PickNewPatrolPoint();
                    }
                }
                else
                {
                    // Reached the last heard wave position; wait until stateExpireTime then return to patrol
                    if (Time.time > stateExpireTime - (huntTimeout * 0.5f))
                    {
                        ReturnToPatrol();
                    }
                }
                return;
            }

            Vector3 dir = toTarget.normalized;

            // Simple obstacle avoidance while patrolling so monster doesn't get stuck on walls
            if (currentState == State.Patrol && Physics.Raycast(transform.position + Vector3.up * 0.6f, dir, obstacleCheckDistance))
            {
                PickNewPatrolPoint();
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            float speed = patrolSpeed;
            if (currentState == State.Investigate)
            {
                speed = investigateSpeed;
            }
            else if (currentState == State.Hunt)
            {
                bool canSprint = stamina == null || stamina.CanSprint;
                if (canSprint)
                {
                    speed = huntSpeed;
                    if (stamina != null) stamina.ConsumeSprint(Time.deltaTime);
                }
                else
                {
                    speed = investigateSpeed;
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

            EmitWalkWave();
        }

        private void EmitWalkWave()
        {
            float interval = currentState == State.Hunt ? huntWaveInterval : patrolWaveInterval;
            if (Time.time < nextStepWaveTime) return;

            nextStepWaveTime = Time.time + interval;
            SoundBus.Emit(transform.position, stepWaveRadius, 1.0f, SoundType.Enemy, gameObject);
        }

        private void PickNewPatrolPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            targetPosition = spawnOrigin + new Vector3(randomCircle.x, 0f, randomCircle.y);
            nextPatrolPickTime = Time.time + Random.Range(2f, 5f);
        }

        private void ReturnToPatrol()
        {
            currentState = State.Patrol;
            soundHitCount = 0;
            targetDamageable = null;
            targetTransform = null;
            PickNewPatrolPoint();
        }
    }
}
