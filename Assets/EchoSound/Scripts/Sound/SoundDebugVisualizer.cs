using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Debug visualizer that renders expanding sound pulses in the Unity Scene view using Gizmos.
    /// Useful for observing noise propagation, hearing ranges, and echolocation pulse timing.
    /// </summary>
    [AddComponentMenu("EchoSound/Sound/Sound Debug Visualizer")]
    public class SoundDebugVisualizer : MonoBehaviour
    {
        [Tooltip("Lifetime of a visual pulse in seconds.")]
        [Range(0.2f, 3f)]
        public float pulseLifetime = 1.0f;

        [Tooltip("Enable or disable pulse drawing in Scene view.")]
        public bool showGizmos = true;

        [Tooltip("Also log emitted sound events to the Unity console.")]
        public bool logToConsole = false;

        private class Pulse
        {
            public Vector3 Position;
            public float MaxRadius;
            public SoundType Type;
            public float StartTime;
            public Color BaseColor;
        }

        private readonly List<Pulse> activePulses = new List<Pulse>();

        private void OnEnable()
        {
            SoundBus.OnSoundEmitted += HandleSoundEmitted;
        }

        private void OnDisable()
        {
            SoundBus.OnSoundEmitted -= HandleSoundEmitted;
            activePulses.Clear();
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = activePulses.Count - 1; i >= 0; i--)
            {
                if (now - activePulses[i].StartTime > pulseLifetime)
                {
                    activePulses.RemoveAt(i);
                }
            }
        }

        private void HandleSoundEmitted(SoundEvent sound)
        {
            if (logToConsole)
            {
                Debug.Log($"<color=#38BDF8>[SoundBus]</color> {sound}", sound.Source);
            }

            Color color = GetColorForSoundType(sound.Type);
            activePulses.Add(new Pulse
            {
                Position = sound.Position,
                MaxRadius = sound.Radius,
                Type = sound.Type,
                StartTime = Time.time,
                BaseColor = color
            });
        }

        private static Color GetColorForSoundType(SoundType type)
        {
            return type switch
            {
                SoundType.FootstepWalk => new Color(0.3f, 0.9f, 0.4f), // Soft green
                SoundType.FootstepRun => new Color(1f, 0.75f, 0.1f),  // Amber / orange
                SoundType.Jump or SoundType.Land => new Color(0.2f, 0.8f, 1f), // Cyan
                SoundType.CameraPing => new Color(0.85f, 0.4f, 1f),    // Purple
                SoundType.Voice => new Color(0.2f, 0.6f, 1f),         // Blue
                SoundType.Impact => new Color(1f, 0.3f, 0.3f),        // Red
                SoundType.Enemy => new Color(0.9f, 0.1f, 0.1f),       // Crimson
                _ => Color.white
            };
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            float now = Application.isPlaying ? Time.time : 0f;

            for (int i = 0; i < activePulses.Count; i++)
            {
                Pulse pulse = activePulses[i];
                float elapsed = now - pulse.StartTime;
                float progress = Mathf.Clamp01(elapsed / pulseLifetime);
                float currentRadius = Mathf.Lerp(0.2f, pulse.MaxRadius, progress);
                float alpha = 1f - progress;

                Color c = pulse.BaseColor;
                c.a = alpha * 0.75f;
                Gizmos.color = c;

                Gizmos.DrawWireSphere(pulse.Position, currentRadius);
            }
        }
    }
}
