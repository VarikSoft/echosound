using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    public static class SoundBus
    {
        public static event Action<SoundEvent> OnSoundEmitted;
        public static event Action<SoundEvent, AcousticField> OnSoundPropagated;
        public static int ObstacleMask { get; set; } = Physics.DefaultRaycastLayers;
        private static readonly List<ISoundListener> listeners = new List<ISoundListener>();
        private static readonly List<Wave> waves = new List<Wave>();
        private static SoundPropagationDriver driver;
        private sealed class Wave
        {
            public SoundEvent Sound;
            public AcousticField Field;
            public ISoundListener[] Targets;
            public ISoundListener[] ReflectionTargets;
            public AcousticReflections Reflections;
        }

        public static int ListenerCount => listeners.Count;
        public static void RegisterListener(ISoundListener listener)
        {
            if (listener != null && !listeners.Contains(listener)) listeners.Add(listener);
        }
        public static void UnregisterListener(ISoundListener listener)
        {
            listeners.Remove(listener);
            foreach (var wave in waves)
            {
                for (int i = 0; i < wave.Targets.Length; i++)
                    if (ReferenceEquals(wave.Targets[i], listener)) wave.Targets[i] = null;
                if (wave.ReflectionTargets == null) continue;
                for (int i = 0; i < wave.ReflectionTargets.Length; i++)
                    if (ReferenceEquals(wave.ReflectionTargets[i], listener)) wave.ReflectionTargets[i] = null;
            }
        }

        public static void Emit(in SoundEvent soundEvent)
        {
            if (soundEvent.Intensity <= 0f) return;
            if (Application.isPlaying && driver == null)
            {
                var go = new GameObject("Sound propagation");
                UnityEngine.Object.DontDestroyOnLoad(go);
                driver = go.AddComponent<SoundPropagationDriver>();
            }
            Physics.SyncTransforms();
            listeners.RemoveAll(l => l == null || (l is UnityEngine.Object obj && obj == null));
            float range = soundEvent.Radius;
            foreach (var listener in listeners) range = Mathf.Max(range, soundEvent.Radius * listener.HearingSensitivity);
            var field = listeners.Count > 0 || OnSoundPropagated != null
                ? new AcousticField(soundEvent, range, ObstacleMask) : null;
            if (listeners.Count > 0)
            {
                if (waves.Count >= 64) waves.RemoveAt(0);
                var reflections = new AcousticReflections(soundEvent, field, ObstacleMask);
                waves.Add(new Wave { Sound = soundEvent, Field = field, Targets = listeners.ToArray(), Reflections = reflections,
                    ReflectionTargets = reflections.Count > 0 ? listeners.ToArray() : null });
            }
            // One failing observer cannot starve the remaining subscribers.
            if (OnSoundEmitted != null)
                foreach (Action<SoundEvent> handler in OnSoundEmitted.GetInvocationList())
                    try { handler(soundEvent); } catch (Exception e) { Debug.LogException(e); }
            if (OnSoundPropagated != null)
                foreach (Action<SoundEvent, AcousticField> handler in OnSoundPropagated.GetInvocationList())
                    try { handler(soundEvent, field); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Advances physical arrival in seconds. Public for deterministic editor checks.</summary>
        public static void Advance(float now)
        {
            if (waves.Count == 0) return;
            // Collect first so callbacks may safely emit, reset or unregister.
            var deliveries = new List<(ISoundListener listener, SoundEvent sound, float distance)>();
            for (int w = waves.Count - 1; w >= 0; w--)
            {
                var wave = waves[w];
                float travelled = (now - wave.Sound.Timestamp) * AcousticField.SpeedOfSound;
                bool pending = false;
                for (int i = 0; i < wave.Targets.Length; i++)
                {
                    var listener = wave.Targets[i];
                    if (listener == null || (listener is UnityEngine.Object obj && obj == null)) continue;
                    float direct = Vector3.Distance(wave.Sound.Position, listener.ListenerPosition);
                    if (direct > travelled) { pending = true; continue; }
                    float distance = wave.Field.PathDistance(listener.ListenerPosition);
                    if (distance > travelled) { pending = true; continue; }
                    wave.Targets[i] = null;
                    float sensitivity = Mathf.Max(0f, listener.HearingSensitivity);
                    float received = wave.Sound.Intensity * AcousticField.Pressure(distance, direct);
                    float threshold = AcousticField.Pressure(wave.Sound.Radius, wave.Sound.Radius);
                    if (distance <= wave.Sound.Radius * sensitivity && received * sensitivity >= threshold)
                        deliveries.Add((listener, wave.Sound.WithIntensity(received), distance));
                }
                if (wave.ReflectionTargets != null)
                {
                    for (int i = 0; i < wave.ReflectionTargets.Length; i++)
                    {
                        var listener = wave.ReflectionTargets[i];
                        if (listener == null || (listener is UnityEngine.Object obj && obj == null)) continue;
                        if (!wave.Reflections.TryGetEcho(listener.ListenerPosition, out float distance, out float pressure))
                        { wave.ReflectionTargets[i] = null; continue; }
                        if (distance > travelled) { pending = true; continue; }
                        wave.ReflectionTargets[i] = null;
                        float sensitivity = Mathf.Max(0, listener.HearingSensitivity);
                        float received = wave.Sound.Intensity * pressure;
                        float threshold = AcousticField.Pressure(wave.Sound.Radius, wave.Sound.Radius);
                        if (distance <= wave.Sound.Radius * sensitivity && received * sensitivity >= threshold)
                            deliveries.Add((listener, wave.Sound.WithReflection(received), distance));
                    }
                }
                if (!pending || travelled > wave.Field.Range + AcousticField.SpeedOfSound * 0.1f) waves.RemoveAt(w);
            }
            foreach (var delivery in deliveries)
            {
                if (!listeners.Contains(delivery.listener)) continue;
                try { delivery.listener.OnSoundHeard(delivery.sound, delivery.distance); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Emit(Vector3 position, float radius, float intensity = 1f,
            SoundType type = SoundType.Generic, GameObject source = null, object tag = null)
            => Emit(new SoundEvent(position, radius, intensity, type, source, tag));

        public static void ClearPending() => waves.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            OnSoundEmitted = null;
            OnSoundPropagated = null;
            listeners.Clear();
            waves.Clear();
            ObstacleMask = Physics.DefaultRaycastLayers;
            if (driver != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(driver.gameObject);
                else UnityEngine.Object.DestroyImmediate(driver.gameObject);
            }
            driver = null;
        }
    }
}
