using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Interface for any game entity or system capable of detecting sound events.
    /// Can be registered directly with the SoundBus for automatic spatial range checks.
    /// </summary>
    public interface ISoundListener
    {
        /// <summary>World position of the listener (e.g. ears, head, sensor).</summary>
        Vector3 ListenerPosition { get; }

        /// <summary>
        /// Hearing sensitivity multiplier (1.0 = normal hearing, >1.0 = sensitive, <1.0 = impaired).
        /// </summary>
        float HearingSensitivity { get; }

        /// <summary>
        /// Called by SoundBus when a sound event reaches this listener.
        /// </summary>
        /// <param name="soundEvent">The emitted sound event.</param>
        /// <param name="distance">Distance in meters from the sound origin to ListenerPosition.</param>
        void OnSoundHeard(in SoundEvent soundEvent, float distance);
    }
}
