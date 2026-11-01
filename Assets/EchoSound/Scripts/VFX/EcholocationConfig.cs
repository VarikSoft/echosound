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
        [Tooltip("Visual sphere speed in meters per second. Gameplay hearing uses its own acoustic timing.")]
        [Range(5f, 60f)] public float waveSpeed = 12f;

        [Tooltip("World-space width of the bright spherical wavefront in meters.")]
        [Range(0.1f, 3f)]
        public float waveFrontWidth = 0.65f;

        [Tooltip("How long (in seconds) geometry remains dimly revealed behind the passing wave.")]
        [Range(0.2f, 4f)]
        public float trailDecayDuration = 1.35f;

        [Tooltip("Minimum visual reach of a footstep, enough to scan a 12m room from its entrance. Does not change hearing.")]
        [Range(0f, 50f)] public float minimumFootstepVisualRadius = 32f;
        [Tooltip("Minimum visual strength of a non-silent footstep. Contact acoustics and enemy hearing remain unchanged.")]
        [Range(0f, 1f)] public float minimumFootstepVisualIntensity = 0.85f;

        [Header("Surface Detail & Outlines")]
        [Tooltip("Grid spacing in meters.")]
        [Range(0.2f, 5f)]
        public float gridScale = 1.0f;

        [Tooltip("Brightness of the technical world-space grid overlay.")]
        [Range(0f, 2f)]
        public float gridIntensity = 0f;

        [Tooltip("Emissive surface fill behind the front. Lower values emphasize contours and Fresnel.")]
        [Range(0f, 1f)] public float surfaceTint = 0.4f;
        [Range(0f, 3f)] public float fresnelIntensity = 1.4f;

        [Tooltip("Edge detection strength on geometric corners and silhouettes.")]
        [Range(0f, 3f)]
        public float edgeHighlightIntensity = 2.2f;

        [Header("Surface Contact")]
        [Tooltip("Ground afterglow brightness relative to walls and objects.")]
        [Range(0f, 1f)] public float floorVisibility = 0.45f;
        [Tooltip("Ground afterglow near the player's feet.")]
        [Range(0f, 1f)] public float nearFloorVisibility = 0.2f;
        [Tooltip("Ceiling response relative to walls, including contours and contact flashes.")]
        [Range(0f, 1f)] public float ceilingVisibility = 0.12f;
        [Tooltip("Short contour/shape response when the wave touches a surface.")]
        [Range(0f, 3f)] public float contactIntensity = 1.4f;
        [Tooltip("World-anchored variation of the contact response; does not alter the fading trail.")]
        [Range(0f, 1f)] public float contactVariation = 0.6f;

        [Header("Atmosphere")]
        [Tooltip("Very faint baseline ambient visibility so pitch black isn't completely disorienting.")]
        [Range(0f, 0.1f)]
        public float ambientBrightness = 0f;

        [Tooltip("Color of the distant void/sky.")]
        public Color voidColor = Color.black;

        [System.Serializable]
        public struct SoundVisualProfile
        {
            public SoundType soundType;
            public Color waveColor;
            [HideInInspector] public float speedMultiplier; // legacy serialized value; propagation speed is shared
            [Range(0.2f, 3f)] public float intensityMultiplier;
            [Range(0.2f, 3f)] public float decayMultiplier;
            [Tooltip("Visual reach multiplier; does not change enemy hearing range.")]
            [Range(0.5f, 2f)] public float rangeMultiplier;

            public SoundVisualProfile(
                SoundType type,
                Color color,
                float speed = 1f,
                float intensity = 1f,
                float decay = 1f,
                float range = 1f)
            {
                soundType = type;
                waveColor = color;
                speedMultiplier = speed;
                intensityMultiplier = intensity;
                decayMultiplier = decay;
                rangeMultiplier = range;
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
                new SoundVisualProfile(SoundType.FootstepWalk, new Color(0.25f, 0.95f, 0.65f), intensity: 1.9f, decay: 1.8f, range: 1.5f),
                new SoundVisualProfile(SoundType.FootstepRun, new Color(1f, 0.75f, 0.15f), intensity: 2.3f, decay: 1.65f, range: 1.2f),
                new SoundVisualProfile(SoundType.Jump, new Color(0.3f, 0.85f, 1.0f), speed: 1.0f, intensity: 1.0f, decay: 1.0f),
                new SoundVisualProfile(SoundType.Land, new Color(0.2f, 0.95f, 1.0f), speed: 1.2f, intensity: 1.4f, decay: 1.3f),
                new SoundVisualProfile(SoundType.Interaction, new Color(0.9f, 0.9f, 1.0f), speed: 1.0f, intensity: 1.1f, decay: 1.1f),
                new SoundVisualProfile(SoundType.CameraPing, new Color(0.75f, 0.35f, 1.0f), speed: 1.4f, intensity: 1.8f, decay: 1.8f),
                new SoundVisualProfile(SoundType.Voice, new Color(0.2f, 0.65f, 1f), intensity: 1.1f, decay: 0.85f),
                new SoundVisualProfile(SoundType.Impact, new Color(1.0f, 0.4f, 0.3f), speed: 1.2f, intensity: 1.5f, decay: 1.4f),
                new SoundVisualProfile(SoundType.Enemy, new Color(1.0f, 0.15f, 0.15f), speed: 1.1f, intensity: 2.0f, decay: 1.6f),
                new SoundVisualProfile(SoundType.Generic, new Color(0.85f, 0.9f, 0.95f), speed: 1.0f, intensity: 1.0f, decay: 1.0f),
                new SoundVisualProfile(SoundType.Custom, new Color(0.5f, 1.0f, 0.9f), speed: 1.0f, intensity: 1.0f, decay: 1.0f)
            };
            // Original mint/amber/blue palette, with softer saturation and emission.
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                profile.waveColor = Color.Lerp(profile.waveColor, Color.white, 0.18f) * 0.78f;
                profile.waveColor.a = 1f;
                profiles[i] = profile;
            }
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
