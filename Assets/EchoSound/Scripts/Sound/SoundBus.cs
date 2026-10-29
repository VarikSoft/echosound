using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Central event bus for emitting and distributing sound events across EchoSound.
    /// Supports both global broadcast delegates (for visual echolocation and debug tools)
    /// and spatially-filtered listener subscriptions (for AI and enemies).
    /// </summary>
    public static class SoundBus
    {
        /// <summary>
        /// Global event fired whenever any sound occurs in the world.
        /// Ideal for echolocation shader managers, global audio managers, and debug tools.
        /// </summary>
        public static event Action<SoundEvent> OnSoundEmitted;

        private static readonly List<ISoundListener> registeredListeners = new List<ISoundListener>();
        private static readonly List<ISoundListener> deadListenersBuffer = new List<ISoundListener>();

        /// <summary>Current count of actively registered spatial listeners.</summary>
        public static int ListenerCount => registeredListeners.Count;

        /// <summary>
        /// Registers a spatial listener to receive sound events within its hearing range.
        /// </summary>
        public static void RegisterListener(ISoundListener listener)
        {
            if (listener == null) return;
            if (!registeredListeners.Contains(listener))
            {
                registeredListeners.Add(listener);
            }
        }

        /// <summary>
        /// Unregisters a spatial listener.
        /// </summary>
        public static void UnregisterListener(ISoundListener listener)
        {
            if (listener == null) return;
            registeredListeners.Remove(listener);
        }

        /// <summary>
        /// Emits a sound event into the bus.
        /// Broadcasts to OnSoundEmitted and notifies all in-range registered listeners.
        /// </summary>
        public static void Emit(in SoundEvent soundEvent)
        {
            // 1. Notify global observers (Echolocation VFX, Debugger, etc.)
            try
            {
                OnSoundEmitted?.Invoke(soundEvent);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // 2. Notify spatial listeners (AI, Companion, etc.)
            deadListenersBuffer.Clear();

            for (int i = 0; i < registeredListeners.Count; i++)
            {
                ISoundListener listener = registeredListeners[i];

                // Check for destroyed Unity objects implementing ISoundListener
                if (listener is UnityEngine.Object unityObj && unityObj == null)
                {
                    deadListenersBuffer.Add(listener);
                    continue;
                }

                float distance = Vector3.Distance(soundEvent.Position, listener.ListenerPosition);
                float effectiveRadius = soundEvent.Radius * Mathf.Max(0.01f, listener.HearingSensitivity);

                if (distance <= effectiveRadius)
                {
                    try
                    {
                        listener.OnSoundHeard(soundEvent, distance);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            // Clean up any stale destroyed listeners
            if (deadListenersBuffer.Count > 0)
            {
                for (int i = 0; i < deadListenersBuffer.Count; i++)
                {
                    registeredListeners.Remove(deadListenersBuffer[i]);
                }
                deadListenersBuffer.Clear();
            }
        }

        /// <summary>
        /// Convenience helper to construct and emit a SoundEvent in one call.
        /// </summary>
        public static void Emit(
            Vector3 position,
            float radius,
            float intensity = 1f,
            SoundType type = SoundType.Generic,
            GameObject source = null,
            object tag = null)
        {
            Emit(new SoundEvent(position, radius, intensity, type, source, tag));
        }

        /// <summary>
        /// Resets all listeners and delegate subscriptions.
        /// Automatically called upon play mode state change or domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            OnSoundEmitted = null;
            registeredListeners.Clear();
            deadListenersBuffer.Clear();
        }
    }
}
