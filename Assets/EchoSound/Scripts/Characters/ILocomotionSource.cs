using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>Anything that moves a character: local input, network replication or AI.</summary>
    public interface ILocomotionSource
    {
        /// <summary>World-space velocity in metres per second.</summary>
        Vector3 Velocity { get; }
        bool IsGrounded { get; }
    }
}
