using System;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Configurable registry mapping AcousticMaterialKind surfaces to specific SoundCues
    /// for footsteps, jumps, landings, and physical collisions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSurfaceAudioProfile", menuName = "EchoSound/Audio/Surface Audio Profile")]
    public class SurfaceAudioProfile : ScriptableObject
    {
        [Serializable]
        public struct SurfaceSoundSet
        {
            public AcousticMaterialKind material;
            public SoundCue walkCue;
            public SoundCue runCue;
            public SoundCue jumpCue;
            public SoundCue landCue;
            public SoundCue impactCue;
        }

        [Header("Material-Specific Cues")]
        public SurfaceSoundSet[] surfaceSets = Array.Empty<SurfaceSoundSet>();

        [Header("Default / Fallback Cues")]
        [Tooltip("Fallback sound cue for walking footsteps.")]
        public SoundCue defaultWalkCue;

        [Tooltip("Fallback sound cue for running footsteps.")]
        public SoundCue defaultRunCue;

        [Tooltip("Fallback sound cue for jumping.")]
        public SoundCue defaultJumpCue;

        [Tooltip("Fallback sound cue for landing.")]
        public SoundCue defaultLandCue;

        [Tooltip("Fallback sound cue for physical impacts.")]
        public SoundCue defaultImpactCue;

        /// <summary>
        /// Retrieves the footstep SoundCue for a given surface material and movement mode.
        /// </summary>
        public SoundCue GetFootstepCue(AcousticMaterialKind kind, bool isRunning)
        {
            if (surfaceSets != null)
            {
                for (int i = 0; i < surfaceSets.Length; i++)
                {
                    if (surfaceSets[i].material == kind)
                    {
                        SoundCue cue = isRunning ? surfaceSets[i].runCue : surfaceSets[i].walkCue;
                        if (cue != null) return cue;
                    }
                }
            }
            return isRunning ? defaultRunCue : defaultWalkCue;
        }

        /// <summary>
        /// Retrieves the jump takeoff SoundCue for a given surface material.
        /// </summary>
        public SoundCue GetJumpCue(AcousticMaterialKind kind)
        {
            if (surfaceSets != null)
            {
                for (int i = 0; i < surfaceSets.Length; i++)
                {
                    if (surfaceSets[i].material == kind && surfaceSets[i].jumpCue != null)
                        return surfaceSets[i].jumpCue;
                }
            }
            return defaultJumpCue;
        }

        /// <summary>
        /// Retrieves the landing SoundCue for a given surface material.
        /// </summary>
        public SoundCue GetLandCue(AcousticMaterialKind kind)
        {
            if (surfaceSets != null)
            {
                for (int i = 0; i < surfaceSets.Length; i++)
                {
                    if (surfaceSets[i].material == kind && surfaceSets[i].landCue != null)
                        return surfaceSets[i].landCue;
                }
            }
            return defaultLandCue;
        }

        /// <summary>
        /// Retrieves the collision impact SoundCue for a given surface material.
        /// </summary>
        public SoundCue GetImpactCue(AcousticMaterialKind kind)
        {
            if (surfaceSets != null)
            {
                for (int i = 0; i < surfaceSets.Length; i++)
                {
                    if (surfaceSets[i].material == kind && surfaceSets[i].impactCue != null)
                        return surfaceSets[i].impactCue;
                }
            }
            return defaultImpactCue;
        }
    }
}
