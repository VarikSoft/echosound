using System;
using System.Collections.Generic;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.VFX
{
    /// <summary>
    /// Configuration asset defining visual styling, speeds, decay curves,
    /// and color profiles for echolocation pulses.
    /// </summary>
    [CreateAssetMenu(fileName = "EcholocationConfig", menuName = "EchoSound/VFX/Echolocation Config")]
    public class EcholocationConfig : ScriptableObject
    {
        [Header("Wave Dynamics")]
        [Tooltip("Speed in meters per second at which the echo pulse expands.")]
        [Range(5f, 60f)]
        public float waveSpeed = 22f;

        [Tooltip("Width of the bright glowing wavefront edge in meters.")]
        [Range(0.1f, 3f)]
        public float waveFrontWidth = 0.6f;

        [Tooltip("How long (in seconds) geometry remains dimly revealed behind the passing wave.")]
        [Range(0.2f, 4f)]
        public float trailDecayDuration = 1.2f;

        [Header("World-Space Grid & Outlines")]
        [Tooltip("Grid spacing in meters.")]
        [Range(0.2f, 5f)]
        public float gridScale = 1.0f;

        [Tooltip("Brightness of the technical world-space grid overlay.")]
        [Range(0f, 2f)]
        public float gridIntensity = 0.75f;

        [Tooltip("Edge detection strength on geometric corners and silhouettes.")]
        [Range(0f, 3f)]
        public float edgeHighlightIntensity = 1.2f;

        [Header("Atmosphere")]
        [Tooltip("Very faint baseline ambient visibility so pitch black isn't completely disorienting.")]
        [Range(0f, 0.1f)]
        public float ambientBrightness = 0.015f;

        [Tooltip("Color of the distant void/sky.")]
        public Color voidColor = new Color(0.02f, 0.03f, 0.05f, 1f);

        [System.Serializable]
        public struct SoundVisualProfile
        {
            public SoundType soundType;
            public Color waveColor;
            [Range(0.5f, 2f)] public float speedMultiplier;
            [Range(0.2f, 3f)] public float intensityMultiplier;
            [Range(0.2f, 3f)] public float decayMultiplier;

            public SoundVisualProfile(
                SoundType type,
                Color color,
                float speed = 1f,
                float intensity = 1f,
                float decay = 1f)
            {
                soundType = type;
                waveColor = color;
                speedMultiplier = speed;
                intensityMultiplier = intensity;
                decayMultiplier = decay;
            }
        }

        [Header("Sound Type Profiles")]
        public List<SoundVisualProfile> profiles = new List<SoundVisualProfile>();

        private void Reset()
        {
            PopulateDefaults();
        }

        public void PopulateDefaults()
        {
            profiles = new List<SoundVisualProfile>
            {
                new SoundVisualProfile(SoundType.FootstepWalk, new Color(0.25f, 0.95f, 0.65f), speed: 0.9f, intensity: 0.8f, decay: 0.9f),
                new SoundVisualProfile(SoundType.FootstepRun, new Color(1.0f, 0.75f, 0.15f), speed: 1.1f, intensity: 1.3f, decay: 1.2f),
                new SoundVisualProfile(SoundType.Jump, new Color(0.3f, 0.85f, 1.0f), speed: 1.0f, intensity: 1.0f, decay: 1.0f),
                new SoundVisualProfile(SoundType.Land, new Color(0.2f, 0.95f, 1.0f), speed: 1.2f, intensity: 1.4f, decay: 1.3f),
                new SoundVisualProfile(SoundType.Interaction, new Color(0.9f, 0.9f, 1.0f), speed: 1.0f, intensity: 1.1f, decay: 1.1f),
                new SoundVisualProfile(SoundType.CameraPing, new Color(0.75f, 0.35f, 1.0f), speed: 1.4f, intensity: 1.8f, decay: 1.8f),
                new SoundVisualProfile(SoundType.Voice, new Color(0.2f, 0.65f, 1.0f), speed: 1.0f, intensity: 1.1f, decay: 1.2f),
                new SoundVisualProfile(SoundType.Impact, new Color(1.0f, 0.4f, 0.3f), speed: 1.2f, intensity: 1.5f, decay: 1.4f),
                new SoundVisualProfile(SoundType.Enemy, new Color(1.0f, 0.15f, 0.15f), speed: 1.1f, intensity: 2.0f, decay: 1.6f),
                new SoundVisualProfile(SoundType.Generic, new Color(0.85f, 0.9f, 0.95f), speed: 1.0f, intensity: 1.0f, decay: 1.0f),
                new SoundVisualProfile(SoundType.Custom, new Color(0.5f, 1.0f, 0.9f), speed: 1.0f, intensity: 1.0f, decay: 1.0f)
            };
        }

        public SoundVisualProfile GetProfile(SoundType type)
        {
            if (profiles == null || profiles.Count == 0)
            {
                PopulateDefaults();
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].soundType == type) return profiles[i];
            }

            return new SoundVisualProfile(type, new Color(0.8f, 0.9f, 1.0f));
        }
    }
}
