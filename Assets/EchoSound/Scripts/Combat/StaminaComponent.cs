using System;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Tracks stamina for physical exertions like sprinting and jumping.
    /// Handles exhaustion state, recovery cooldown, heavy breathing audio,
    /// and stealth acoustic emission into SoundBus.
    /// </summary>
    [AddComponentMenu("EchoSound/Combat/Stamina Component")]
    public class StaminaComponent : MonoBehaviour
    {
        [Header("Stamina Tuning")]
        [Min(1f)] public float maxStamina = 100f;
        public float currentStamina;

        [Tooltip("Stamina drained per second while sprinting.")]
        [Min(0f)] public float sprintDrainRate = 22f;

        [Tooltip("Stamina drained on each jump.")]
        [Min(0f)] public float jumpCost = 15f;

        [Tooltip("Stamina regenerated per second when resting.")]
        [Min(0f)] public float regenRate = 20f;

        [Tooltip("Delay in seconds before stamina begins regenerating after sprinting or jumping.")]
        [Min(0f)] public float regenDelay = 1.2f;

        [Header("Exhaustion & Audio")]
        [Tooltip("Minimum stamina percentage required to resume sprinting after reaching total exhaustion.")]
        [Range(0.05f, 0.5f)] public float recoveryThreshold = 0.20f;

        [Tooltip("SoundCue played when exhausted (heavy panting / breathing).")]
        public SoundCue exhaustedBreathingCue;

        [Tooltip("Acoustic wave radius emitted into SoundBus when heavily panting (allows stealth monsters to hear the player).")]
        public float exhaustedAcousticRadius = 6f;

        [Tooltip("Interval between heavy breathing sounds/pulses while exhausted.")]
        public float breathingInterval = 2.2f;

        public bool IsExhausted { get; private set; }
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaPercent => Mathf.Clamp01(currentStamina / Mathf.Max(1f, maxStamina));

        public event Action<float, float> OnStaminaChanged; // (current, max)
        public event Action<bool> OnExhaustionChanged;       // (isExhausted)

        private float nextRegenTime;
        private float nextBreathingTime;
        private static SoundCue defaultBreathingCue;

        private void Awake()
        {
            if (currentStamina <= 0f) currentStamina = maxStamina;
            EnsureDefaultAudio();
        }

        public void EnsureDefaultAudio()
        {
            if (exhaustedBreathingCue == null)
            {
                if (defaultBreathingCue == null)
                {
                    defaultBreathingCue = Resources.Load<SoundCue>("Audio/Player/Exhausted_Breathing");
                }
                exhaustedBreathingCue = defaultBreathingCue;
            }
        }

        public bool CanSprint => !IsExhausted && currentStamina > 0.5f;

        public void ConsumeSprint(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            nextRegenTime = Time.time + regenDelay;

            currentStamina = Mathf.Max(0f, currentStamina - sprintDrainRate * deltaTime);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0.05f && !IsExhausted)
            {
                SetExhausted(true);
            }
        }

        public bool TryConsumeJump()
        {
            if (currentStamina < jumpCost && IsExhausted) return false;

            nextRegenTime = Time.time + regenDelay;
            currentStamina = Mathf.Max(0f, currentStamina - jumpCost);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0.05f && !IsExhausted)
            {
                SetExhausted(true);
            }
            return true;
        }

        private void Update()
        {
            // Passive Regeneration
            if (Time.time >= nextRegenTime && currentStamina < maxStamina)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * Time.deltaTime);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);

                if (IsExhausted && (currentStamina / maxStamina) >= recoveryThreshold)
                {
                    SetExhausted(false);
                }
            }

            // Periodic heavy breathing while exhausted
            if (IsExhausted && Time.time >= nextBreathingTime)
            {
                nextBreathingTime = Time.time + breathingInterval;
                PlayExhaustedPulse();
            }
        }

        private void SetExhausted(bool exhausted)
        {
            if (IsExhausted == exhausted) return;
            IsExhausted = exhausted;
            OnExhaustionChanged?.Invoke(IsExhausted);

            if (IsExhausted)
            {
                nextBreathingTime = Time.time; // Trigger immediately
            }
        }

        private void PlayExhaustedPulse()
        {
            EnsureDefaultAudio();
            if (exhaustedBreathingCue != null)
            {
                AudioManager.Instance.Play(exhaustedBreathingCue, transform.position, transform);
            }

            if (exhaustedAcousticRadius > 0f)
            {
                SoundBus.Emit(transform.position, exhaustedAcousticRadius, 0.4f, SoundType.FootstepWalk, gameObject);
            }
        }
    }
}
