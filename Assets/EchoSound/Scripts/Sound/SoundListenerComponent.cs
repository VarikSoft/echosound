using System;
using UnityEngine;
using UnityEngine.Events;

namespace EchoSound.Sound
{
    /// <summary>
    /// Base listener component for entities that perceive sounds (enemies, companion, sensors).
    /// Automatically manages its registration with the SoundBus and triggers UnityEvents or virtual callbacks.
    /// </summary>
    [AddComponentMenu("EchoSound/Sound/Sound Listener")]
    public class SoundListenerComponent : MonoBehaviour, ISoundListener
    {
        [Header("Hearing Settings")]
        [Tooltip("Hearing sensitivity multiplier (1.0 = normal, >1.0 = sensitive, <1.0 = impaired).")]
        [Range(0.1f, 3f)]
        public float hearingSensitivity = 1f;

        [Tooltip("Transform representing the entity's ears/sensors. If null, this GameObject's transform is used.")]
        public Transform earsTransform;

        [Header("Events")]
        [Tooltip("Optional UnityEvent invoked when this listener hears a sound.")]
        public SoundUnityEvent onSoundHeard;

        public Vector3 ListenerPosition => earsTransform != null ? earsTransform.position : transform.position;
        public float HearingSensitivity => hearingSensitivity;

        protected virtual void OnEnable()
        {
            SoundBus.RegisterListener(this);
        }

        protected virtual void OnDisable()
        {
            SoundBus.UnregisterListener(this);
        }

        /// <summary>
        /// Invoked when a sound event is within hearing range.
        /// Override in subclass or listen via onSoundHeard UnityEvent.
        /// </summary>
        public virtual void OnSoundHeard(in SoundEvent soundEvent, float distance)
        {
            onSoundHeard?.Invoke(soundEvent);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f);
            Gizmos.DrawWireSphere(ListenerPosition, 0.5f);
        }
    }

    [Serializable]
    public class SoundUnityEvent : UnityEvent<SoundEvent> { }
}
