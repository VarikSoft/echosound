using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Immutable payload representing a single sound event in the game world.
    /// Emitted whenever an entity or mechanic produces noise.
    /// </summary>
    public readonly struct SoundEvent
    {
        /// <summary>World position where the sound originated.</summary>
        public readonly Vector3 Position;

        /// <summary>Maximum radius (in meters) the sound reaches for hearing and echolocation.</summary>
        public readonly float Radius;

        /// <summary>Relative source pressure when emitted; attenuated received pressure in listener callbacks.</summary>
        public readonly float Intensity;

        /// <summary>Category of the sound.</summary>
        public readonly SoundType Type;

        /// <summary>The GameObject that generated this sound, or null for ambient/environmental sources.</summary>
        public readonly GameObject Source;

        /// <summary>Time (in seconds, Time.time) when the sound was emitted.</summary>
        public readonly float Timestamp;

        /// <summary>Optional arbitrary context or metadata (e.g. surface material, camera identifier).</summary>
        public readonly object Tag;

        /// <summary>True for the separately delayed first-bounce echo delivered to a listener.</summary>
        public readonly bool IsReflection;

        public SoundEvent(
            Vector3 position,
            float radius,
            float intensity = 1f,
            SoundType type = SoundType.Generic,
            GameObject source = null,
            object tag = null)
        {
            Position = position;
            Radius = Mathf.Max(0.01f, radius);
            Intensity = Mathf.Max(0f, intensity);
            Type = type;
            Source = source;
            Timestamp = Time.time;
            Tag = tag;
            IsReflection = false;
        }

        private SoundEvent(in SoundEvent original, float intensity, bool isReflection)
        {
            Position = original.Position;
            Radius = original.Radius;
            Intensity = intensity;
            Type = original.Type;
            Source = original.Source;
            Timestamp = original.Timestamp;
            Tag = original.Tag;
            IsReflection = isReflection;
        }

        /// <summary>Preserves origin/time/context while reporting received pressure to a listener.</summary>
        public SoundEvent WithIntensity(float intensity) => new SoundEvent(this, Mathf.Max(0f, intensity), IsReflection);

        public SoundEvent WithReflection(float intensity) => new SoundEvent(this, Mathf.Max(0f, intensity), true);

        /// <summary>
        /// Checks whether a target point in world space is within hearing / illumination radius.
        /// </summary>
        public bool IsInRange(Vector3 targetPosition)
        {
            return (targetPosition - Position).sqrMagnitude <= Radius * Radius;
        }

        /// <summary>
        /// Calculates linear attenuation (1.0 at origin down to 0.0 at max radius).
        /// Returns 0.0 if the target is outside of the radius.
        /// </summary>
        public float GetLinearAttenuation(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(Position, targetPosition);
            if (distance >= Radius) return 0f;
            return (1f - (distance / Radius)) * Intensity;
        }

        public override string ToString()
        {
            return $"SoundEvent[{Type} at {Position}, Radius={Radius:F1}m, Intensity={Intensity:F2}, Source={(Source != null ? Source.name : "null")}]";
        }
    }
}
