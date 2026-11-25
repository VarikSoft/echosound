using UnityEngine;

namespace EchoSound.Characters
{
    public enum FootstepKind { Walk, Run, Jump, Land }

    /// <summary>A foot (or both feet) touching or leaving the ground.</summary>
    public readonly struct FootstepEvent
    {
        public readonly FootstepKind Kind;
        /// <summary>World position of the contact, normally the foot bone.</summary>
        public readonly Vector3 Position;
        /// <summary>0..1 strength of the contact.</summary>
        public readonly float Intensity;
        /// <summary>Downward speed at touchdown in m/s; zero for anything but Land.</summary>
        public readonly float ImpactSpeed;

        public FootstepEvent(FootstepKind kind, Vector3 position, float intensity, float impactSpeed = 0)
        {
            Kind = kind;
            Position = position;
            Intensity = intensity;
            ImpactSpeed = impactSpeed;
        }
    }
}
