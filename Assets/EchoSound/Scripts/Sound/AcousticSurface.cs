using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>Assign a profile to a collider/renderer or their parent. Child overrides take precedence.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AcousticSurface : MonoBehaviour
    {
        public AcousticMaterial profile;
        private static readonly HashSet<AcousticSurface> active = new HashSet<AcousticSurface>();
        public static IEnumerable<AcousticSurface> Active => active;
        private Renderer[] renderers;
        private Vector4 appliedResponse = new Vector4(-1, -1, -1, -1);
        private MaterialPropertyBlock properties;
        public Renderer[] Renderers => renderers ?? (renderers = GetComponentsInChildren<Renderer>(true));

        private void OnEnable() { renderers = null; active.Add(this); UpdateResponse(true); }
        private void OnDisable() { active.Remove(this); ApplyResponse(new Vector4(1, 1, 0, 0)); }
        private void OnTransformChildrenChanged() { renderers = null; UpdateResponse(true); }
        private void OnValidate() { if (isActiveAndEnabled) UpdateResponse(true); }
        private void LateUpdate() => UpdateResponse(false);

        private void UpdateResponse(bool force)
        {
            Vector4 response = profile != null
                ? new Vector4(Mathf.Clamp01(profile.reflection), Mathf.Clamp(profile.echoPersistence, 0.05f, 1),
                    (int)profile.kind / 10f, Mathf.Clamp01(profile.hintStrength))
                : new Vector4(1, 1, 0, 0);
            if (force || response != appliedResponse) ApplyResponse(response);
        }

        private void ApplyResponse(Vector4 response)
        {
            appliedResponse = response;
            if (properties == null) properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in Renderers)
            {
                if (renderer == null || renderer.GetComponentInParent<AcousticSurface>() != this) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetVector("_AcousticSurfaceResponse", response);
                renderer.SetPropertyBlock(properties);
            }
        }

        public static AcousticSurface Find(Component component)
        {
            if (component == null) return null;
            var surface = component.GetComponentInParent<AcousticSurface>();
            return surface != null && surface.isActiveAndEnabled && surface.profile != null ? surface : null;
        }

        public static float ContactGain(Collider collider)
        {
            AcousticSurface surface = Find(collider);
            return surface != null ? Mathf.Clamp(surface.profile.contactLoudness, 0, 2) : 1;
        }
    }
}
