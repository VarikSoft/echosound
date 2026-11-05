using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.VFX
{
    /// <summary>Bounded visual sphere pulses; gameplay hearing remains on the acoustic sound bus.</summary>
    [ExecuteAlways]
    [AddComponentMenu("EchoSound/VFX/Echolocation Manager")]
    public class EcholocationManager : MonoBehaviour
    {
        public const int MaxPulses = 16;
        public static EcholocationManager Instance { get; private set; }
        public EcholocationConfig config;
        public bool logPulses;
        private struct Pulse
        {
            public Vector3 Origin;
            public float Start, Radius, Speed, Intensity, Decay, Width;
            public Color Color;
            public bool Active;
        }
        private readonly Pulse[] pulses = new Pulse[MaxPulses];
        private readonly Vector4[] origins = new Vector4[MaxPulses];
        private readonly Vector4[] parameters = new Vector4[MaxPulses];
        private readonly Vector4[] colors = new Vector4[MaxPulses];
        private int next;
        private EcholocationConfig ownedConfig;
        private static float Now => Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        private void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            EnsureConfig();
            SoundBus.OnSoundEmitted += HandleSoundEmitted;
            ClearAllPulses();
        }
        private void OnDisable()
        {
            SoundBus.OnSoundEmitted -= HandleSoundEmitted;
            if (Instance != this) return;
            ClearAllPulses();
            Instance = null;
            if (ownedConfig != null) DestroyImmediate(ownedConfig);
            ownedConfig = null;
        }
        private void EnsureConfig()
        {
            if (config != null) return;
            ownedConfig = ScriptableObject.CreateInstance<EcholocationConfig>();
            ownedConfig.hideFlags = HideFlags.HideAndDontSave;
            ownedConfig.PopulateDefaults();
            config = ownedConfig;
        }
        private void Update()
        {
            EnsureConfig();
            int count = 0;
            float now = Now;
            for (int i = 0; i < MaxPulses; i++)
            {
                var p = pulses[i];
                if (!p.Active) continue;
                float elapsed = now - p.Start;
                if (elapsed < 0 || elapsed > p.Radius / p.Speed + p.Decay)
                { pulses[i].Active = false; continue; }
                origins[count] = new Vector4(p.Origin.x, p.Origin.y, p.Origin.z, p.Radius);
                parameters[count] = new Vector4(p.Start, p.Speed, p.Intensity, p.Decay);
                colors[count] = new Vector4(p.Color.r, p.Color.g, p.Color.b, p.Width);
                count++;
            }
            Shader.SetGlobalFloat("_EchoTime", now);
            Shader.SetGlobalInt("_EchoPulseCount", count);
            Shader.SetGlobalVectorArray("_EchoPulseOrigins", origins);
            Shader.SetGlobalVectorArray("_EchoPulseParams", parameters);
            Shader.SetGlobalVectorArray("_EchoPulseColors", colors);
            Shader.SetGlobalFloat("_EchoGridScale", config.gridScale);
            Shader.SetGlobalFloat("_EchoGridIntensity", config.gridIntensity);
            Shader.SetGlobalFloat("_EchoEdgeIntensity", config.edgeHighlightIntensity);
            Shader.SetGlobalFloat("_EchoSurfaceTint", config.surfaceTint);
            Shader.SetGlobalFloat("_EchoFresnelIntensity", config.fresnelIntensity);
            Shader.SetGlobalFloat("_EchoCeilingVisibility", config.ceilingVisibility);
            Shader.SetGlobalVector("_EchoContactStyle", new Vector4(config.floorVisibility,config.nearFloorVisibility,
                config.contactIntensity,config.contactVariation));
        }
        public void HandleSoundEmitted(SoundEvent sound)
        {
            if (sound.Intensity <= 0) return;
            EnsureConfig();
            var profile = config.GetProfile(sound.Type);
            bool footstep = sound.Type == SoundType.FootstepWalk || sound.Type == SoundType.FootstepRun;
            float radius = VisualRadius(sound.Radius, sound.Intensity) * (profile.rangeMultiplier > 0 ? profile.rangeMultiplier : 1f);
            float intensity = sound.Intensity * profile.intensityMultiplier;
            if (footstep)
            {
                radius = Mathf.Max(radius, config.minimumFootstepVisualRadius);
                intensity = Mathf.Max(intensity, config.minimumFootstepVisualIntensity);
            }
            pulses[next] = new Pulse
            {
                Origin = sound.Position, Start = Now,
                Radius = radius,
                Speed = Mathf.Max(1, config.waveSpeed),
                Intensity = intensity,
                Decay = Mathf.Max(0.1f, config.trailDecayDuration * profile.decayMultiplier),
                Width = Mathf.Max(0.05f, config.waveFrontWidth),
                Color = profile.waveColor, Active = true
            };
            next = (next + 1) % MaxPulses;
            if (logPulses) Debug.Log($"Echo sphere: {sound.Type}, radius {VisualRadius(sound.Radius, sound.Intensity):F1}m", this);
        }
        public static float VisualRadius(float sourceRange, float loudness)
            => loudness <= 0 ? 0 : Mathf.Max(0.1f, sourceRange) * Mathf.Sqrt(Mathf.Clamp(loudness, 0f, 4f));

        /// <summary>Checks current surface memory without revealing materials ahead of an arriving pulse.</summary>
        public float SurfaceVisibility(Vector3 position, AcousticMaterial material, float sampleTime = -1)
        {
            if (material == null || !isActiveAndEnabled) return 0;
            float now = sampleTime < 0 ? Now : sampleTime;
            float visible = 0;
            foreach (Pulse pulse in pulses)
            {
                if (!pulse.Active) continue;
                float distance = Vector3.Distance(position, pulse.Origin);
                if (distance >= pulse.Radius) continue;
                float age = now - pulse.Start - distance / pulse.Speed;
                float duration = pulse.Decay * Mathf.Clamp(material.echoPersistence, 0.35f, 1);
                if (age <= 0 || age >= duration) continue;
                float progress = age / duration;
                float onset = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age * pulse.Speed / (pulse.Width * 0.16f)));
                float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(pulse.Radius * 0.8f, pulse.Radius, distance));
                float visualReflection = material.reflection <= 0 ? 0 : Mathf.Lerp(0.25f, 1, Mathf.Clamp01(material.reflection));
                visible += pulse.Intensity * visualReflection * (1-progress) * (1-progress)
                    * Mathf.Exp(-progress * 1.5f) * onset * edge;
            }
            return visible;
        }

        public void ClearAllPulses()
        {
            for (int i = 0; i < MaxPulses; i++) pulses[i].Active = false;
            next = 0;
            Shader.SetGlobalInt("_EchoPulseCount", 0);
        }
        [ContextMenu("Emit Test Pulse (Center)")]
        public void EmitTestPulse() => SoundBus.Emit(transform.position, 20, 1.2f, SoundType.CameraPing);
    }
}
