using System;
using UnityEngine;
using UnityEngine.Audio;

namespace EchoSound.Sound
{
    public enum ClipSelectionMode
    {
        Random = 0,
        RandomNoRepeat = 1,
        Sequential = 2
    }

    /// <summary>
    /// Data-driven configuration for sound playback.
    /// Supports multiple clips, pitch/volume randomization, spatial attenuation,
    /// cooldown throttling, and optional synchronization with the acoustic SoundBus.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundCue", menuName = "EchoSound/Audio/Sound Cue")]
    public class SoundCue : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("Clips to choose from when playing this sound.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Tooltip("Rule for selecting an audio clip from the array.")]
        public ClipSelectionMode selectionMode = ClipSelectionMode.RandomNoRepeat;

        [Header("Volume & Pitch")]
        [Range(0f, 1f)]
        public float baseVolume = 1f;

        [Tooltip("Random volume multiplier range [min, max].")]
        public Vector2 volumeVariation = new Vector2(0.95f, 1.05f);

        [Range(0.1f, 3f)]
        public float basePitch = 1f;

        [Tooltip("Random pitch multiplier range [min, max].")]
        public Vector2 pitchVariation = new Vector2(0.95f, 1.05f);

        [Header("Spatial Audio")]
        [Range(0f, 1f)]
        [Tooltip("0 = 2D (UI/ambient), 1 = full 3D spatialized.")]
        public float spatialBlend = 1f;

        public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Min(0.01f)]
        public float minDistance = 1f;

        [Min(0.1f)]
        public float maxDistance = 20f;

        [Header("Throttling")]
        [Tooltip("Minimum time (seconds) between triggers to prevent repetitive audio spam.")]
        public float cooldown = 0.05f;

        [Header("Routing")]
        public AudioMixerGroup mixerGroup;

        [Header("Acoustic SoundBus Sync")]
        [Tooltip("If true, playing this cue automatically emits an acoustic wave into SoundBus for echolocation VFX and AI perception.")]
        public bool emitToSoundBus = false;

        public SoundType soundType = SoundType.Generic;

        [Tooltip("Propagation radius for SoundBus if emitToSoundBus is enabled.")]
        public float busRadius = 12f;

        [Tooltip("Sound pressure intensity for SoundBus if emitToSoundBus is enabled.")]
        public float busIntensity = 1f;

        [NonSerialized] private int lastSelectedIndex = -1;
        [NonSerialized] private int sequentialIndex = 0;
        [NonSerialized] private float lastTriggerTime = -999f;

        /// <summary>
        /// Checks if this cue can be played given the configured cooldown.
        /// If allowed, updates the internal trigger timestamp.
        /// </summary>
        public bool CanPlay(float currentTime)
        {
            if (cooldown > 0f && currentTime - lastTriggerTime < cooldown)
                return false;

            lastTriggerTime = currentTime;
            return true;
        }

        /// <summary>
        /// Selects an AudioClip according to the configured selection mode.
        /// </summary>
        public AudioClip SelectClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];

            switch (selectionMode)
            {
                case ClipSelectionMode.Sequential:
                    AudioClip seqClip = clips[sequentialIndex % clips.Length];
                    sequentialIndex = (sequentialIndex + 1) % clips.Length;
                    return seqClip;

                case ClipSelectionMode.RandomNoRepeat:
                    int nextIndex = UnityEngine.Random.Range(0, clips.Length);
                    if (nextIndex == lastSelectedIndex)
                        nextIndex = (nextIndex + 1 + UnityEngine.Random.Range(0, clips.Length - 1)) % clips.Length;
                    lastSelectedIndex = nextIndex;
                    return clips[nextIndex];

                case ClipSelectionMode.Random:
                default:
                    return clips[UnityEngine.Random.Range(0, clips.Length)];
            }
        }

        /// <summary>
        /// Evaluates a randomized volume and pitch within configured variation ranges.
        /// </summary>
        public (AudioClip clip, float volume, float pitch) EvaluatePlayback()
        {
            AudioClip clip = SelectClip();

            float vMult = UnityEngine.Random.Range(
                Mathf.Min(volumeVariation.x, volumeVariation.y),
                Mathf.Max(volumeVariation.x, volumeVariation.y));
            float volume = Mathf.Clamp01(baseVolume * (vMult <= 0f ? 1f : vMult));

            float pMult = UnityEngine.Random.Range(
                Mathf.Min(pitchVariation.x, pitchVariation.y),
                Mathf.Max(pitchVariation.x, pitchVariation.y));
            float pitch = Mathf.Clamp(basePitch * (pMult <= 0f ? 1f : pMult), 0.1f, 3f);

            return (clip, volume, pitch);
        }

        /// <summary>Resets cooldown and playback state.</summary>
        public void ResetRuntimeState()
        {
            lastSelectedIndex = -1;
            sequentialIndex = 0;
            lastTriggerTime = -999f;
        }
    }
}
