using System;
using System.Collections.Generic;
using EchoSound.Sound;
using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>
    /// Which footstep sounds a character makes on each acoustic material.
    /// Follows the AcousticSurface rule: ground without a profile is unmarked and uses the fallback.
    /// </summary>
    [CreateAssetMenu(menuName = "EchoSound/Footsteps/Footstep sound bank", fileName = "FootstepBank")]
    public sealed class FootstepSoundBank : ScriptableObject
    {
        [Serializable]
        public struct SurfaceSounds
        {
            public AcousticMaterial surface;
            public FootstepSoundSet sounds;
        }

        [Tooltip("Unmarked ground, unlisted materials, and any kind a surface set leaves empty.")]
        public FootstepSoundSet fallback;
        public List<SurfaceSounds> surfaces = new List<SurfaceSounds>();

        /// <summary>The set to play for this surface and contact kind, or null when nothing is assigned.</summary>
        public FootstepSoundSet Resolve(AcousticMaterial surface, FootstepKind kind)
        {
            if (surface != null)
                foreach (var entry in surfaces)
                    if (entry.surface == surface && entry.sounds != null && entry.sounds.Has(kind))
                        return entry.sounds;
            return fallback != null && fallback.Has(kind) ? fallback : null;
        }
    }
}
