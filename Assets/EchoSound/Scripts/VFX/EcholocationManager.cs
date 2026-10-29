using System;
using System.Collections.Generic;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.VFX
{
    /// <summary>
    /// Central manager for echolocation visual waves. Subscribes to SoundBus.OnSoundEmitted,
    /// tracks expanding wave pulses, and uploads pulse data to GPU global shader properties.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("EchoSound/VFX/Echolocation Manager")]
    public class EcholocationManager : MonoBehaviour
    {
        public const int MaxPulses = 16;

        public static EcholocationManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Visual configuration asset. If null, built-in defaults are used.")]
        public EcholocationConfig config;

        [Header("Debug")]
        [Tooltip("Log pulse emissions to the console.")]
        public bool logPulses = false;

        private struct ActivePulse
        {
            public Vector3 Origin;
            public float StartTime;
            public float MaxRadius;
            public float Speed;
            public float Intensity;
            public float DecayDuration;
            public float WaveWidth;
            public Color Color;
            public float TotalLifetime;
            public bool IsActive;
        }

        private readonly ActivePulse[] pulsePool = new ActivePulse[MaxPulses];
        private int nextPulseIndex = 0;

        // Cached shader property IDs
        private static readonly int PropPulseCount = Shader.PropertyToID("_EchoPulseCount");
        private static readonly int PropOrigins = Shader.PropertyToID("_EchoPulseOrigins");
        private static readonly int PropParams = Shader.PropertyToID("_EchoPulseParams");
        private static readonly int PropColors = Shader.PropertyToID("_EchoPulseColors");
        private static readonly int PropGridScale = Shader.PropertyToID("_EchoGridScale");
        private static readonly int PropGridIntensity = Shader.PropertyToID("_EchoGridIntensity");
        private static readonly int PropEdgeIntensity = Shader.PropertyToID("_EchoEdgeIntensity");
        private static readonly int PropAmbientBrightness = Shader.PropertyToID("_EchoAmbientBrightness");
        private static readonly int PropVoidColor = Shader.PropertyToID("_EchoVoidColor");
        private static readonly int PropGlobalWaveWidth = Shader.PropertyToID("_EchoWaveWidth");

        // GPU upload buffers (allocated once to avoid GC in Update)
        private readonly Vector4[] gpuOrigins = new Vector4[MaxPulses];
        private readonly Vector4[] gpuParams = new Vector4[MaxPulses];
        private readonly Vector4[] gpuColors = new Vector4[MaxPulses];

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureConfig();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureConfig();
            SoundBus.OnSoundEmitted += HandleSoundEmitted;
            ClearAllPulses();
        }

        private void OnDisable()
        {
            SoundBus.OnSoundEmitted -= HandleSoundEmitted;
            ClearAllPulses();
            UploadToGPU(0);
            if (Instance == this) Instance = null;
        }

        private void EnsureConfig()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EcholocationConfig>();
                config.PopulateDefaults();
            }
        }

        private void Update()
        {
            EnsureConfig();
            float now = Application.isPlaying ? Time.time : (float)DateTime.Now.TimeOfDay.TotalSeconds;

            int activeCount = 0;

            for (int i = 0; i < MaxPulses; i++)
            {
                if (!pulsePool[i].IsActive) continue;

                float elapsed = now - pulsePool[i].StartTime;
                if (elapsed > pulsePool[i].TotalLifetime || elapsed < 0)
                {
                    pulsePool[i].IsActive = false;
                    continue;
                }

                // Pack GPU data
                ref var pulse = ref pulsePool[i];
                gpuOrigins[activeCount] = new Vector4(pulse.Origin.x, pulse.Origin.y, pulse.Origin.z, pulse.MaxRadius);
                gpuParams[activeCount] = new Vector4(pulse.StartTime, pulse.Speed, pulse.Intensity, pulse.DecayDuration);
                gpuColors[activeCount] = new Vector4(pulse.Color.r, pulse.Color.g, pulse.Color.b, pulse.WaveWidth);
                activeCount++;
            }

            UploadToGPU(activeCount);
        }

        private void UploadToGPU(int activeCount)
        {
            Shader.SetGlobalInt(PropPulseCount, activeCount);

            if (activeCount > 0)
            {
                Shader.SetGlobalVectorArray(PropOrigins, gpuOrigins);
                Shader.SetGlobalVectorArray(PropParams, gpuParams);
                Shader.SetGlobalVectorArray(PropColors, gpuColors);
            }

            if (config != null)
            {
                Shader.SetGlobalFloat(PropGridScale, config.gridScale);
                Shader.SetGlobalFloat(PropGridIntensity, config.gridIntensity);
                Shader.SetGlobalFloat(PropEdgeIntensity, config.edgeHighlightIntensity);
                Shader.SetGlobalFloat(PropAmbientBrightness, config.ambientBrightness);
                Shader.SetGlobalColor(PropVoidColor, config.voidColor);
                Shader.SetGlobalFloat(PropGlobalWaveWidth, config.waveFrontWidth);
            }
        }

        public void HandleSoundEmitted(SoundEvent sound)
        {
            EnsureConfig();
            var profile = config.GetProfile(sound.Type);

            float speed = Mathf.Max(1f, config.waveSpeed * profile.speedMultiplier);
            float intensity = sound.Intensity * profile.intensityMultiplier;
            float decay = Mathf.Max(0.1f, config.trailDecayDuration * profile.decayMultiplier);
            float totalLifetime = (sound.Radius / speed) + decay;

            float now = Application.isPlaying ? Time.time : (float)DateTime.Now.TimeOfDay.TotalSeconds;

            pulsePool[nextPulseIndex] = new ActivePulse
            {
                Origin = sound.Position,
                StartTime = now,
                MaxRadius = sound.Radius,
                Speed = speed,
                Intensity = intensity,
                DecayDuration = decay,
                WaveWidth = config.waveFrontWidth,
                Color = profile.waveColor,
                TotalLifetime = totalLifetime,
                IsActive = true
            };

            nextPulseIndex = (nextPulseIndex + 1) % MaxPulses;

            if (logPulses)
            {
                Debug.Log($"<color=#38BDF8>[EcholocationManager]</color> Spawned pulse at {sound.Position}, Radius={sound.Radius:F1}m, Color={profile.waveColor}");
            }
        }

        public void ClearAllPulses()
        {
            for (int i = 0; i < MaxPulses; i++)
            {
                pulsePool[i].IsActive = false;
                gpuOrigins[i] = Vector4.zero;
                gpuParams[i] = Vector4.zero;
                gpuColors[i] = Vector4.zero;
            }
        }

        [ContextMenu("Emit Test Pulse (Center)")]
        public void EmitTestPulse()
        {
            Vector3 origin = transform.position;
            SoundBus.Emit(origin, 20f, 1.2f, SoundType.CameraPing);
        }
    }
}
