using UnityEngine;
using UnityEngine.Rendering;

namespace EchoSound.VFX
{
    /// <summary>Slow, soft mist below a pit rim. Its shader reveals it only during an echo in gameplay.</summary>
    [ExecuteAlways, RequireComponent(typeof(ParticleSystem))]
    public sealed class AbyssMist : MonoBehaviour
    {
        public Material mistMaterial;
        public Vector2 area = new Vector2(16, 16);
        [Range(1, 8)] public float particlesPerSecond = 3.5f;
        [Tooltip("Inspection-only visibility; automatically disabled in Play mode.")]
        public bool previewInEditor = true;
        private ParticleSystemRenderer particlesRenderer;
        private MaterialPropertyBlock properties;

        [ContextMenu("Apply mist settings")]
        public void Configure()
        {
            var particles = GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 18; main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8, 14);
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(3, 4.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 64; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = Color.white; main.gravityModifier = 0;
            var emission = particles.emission; emission.enabled = true; emission.rateOverTime = particlesPerSecond;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, 0.6f, area.y); shape.position = new Vector3(0, -2.8f, 0);
            var velocity = particles.velocityOverLifetime; velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var noise = particles.noise; noise.enabled = true; noise.strength = 0.15f;
            noise.frequency = 0.18f; noise.scrollSpeed = 0.1f; noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.7f, 0.2f), new GradientAlphaKey(0.5f, 0.65f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            particlesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particlesRenderer.sortMode = ParticleSystemSortMode.Distance;
            particlesRenderer.sharedMaterial = mistMaterial;
            particlesRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particlesRenderer.receiveShadows = false;
            ApplyPreview();
            if (Application.isPlaying) particles.Play();
        }

        private void OnEnable() => ApplyPreview();
        private void LateUpdate() => ApplyPreview();
        private void ApplyPreview()
        {
            if (particlesRenderer == null) particlesRenderer = GetComponent<ParticleSystemRenderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            particlesRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_InspectionPreview", !Application.isPlaying && previewInEditor ? 0.16f : 0);
            particlesRenderer.SetPropertyBlock(properties);
        }
    }
}
