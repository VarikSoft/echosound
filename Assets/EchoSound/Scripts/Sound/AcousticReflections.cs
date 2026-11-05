using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Bounded first-bounce specular echoes. Samples up to 16 visible planes with 54 rays.
    /// The image-source method validates a reflection point and both unobstructed path segments.
    /// </summary>
    public sealed class AcousticReflections
    {
        private struct PlaneSample
        {
            public Collider Collider;
            public Vector3 Point, Normal;
            public float Reflection, Damping;
        }
        private readonly List<PlaneSample> planes = new List<PlaneSample>(16);
        private readonly AcousticField field;
        private readonly Vector3 origin;
        public int Count => planes.Count;

        public AcousticReflections(in SoundEvent sound, AcousticField acousticField, int obstacleMask)
        {
            field = acousticField;
            origin = sound.Position;
            bool hasSurfaces = false;
            foreach (var surface in AcousticSurface.Active)
                if (surface != null && surface.profile != null) { hasSurfaces = true; break; }
            if (!hasSurfaces) return;

            var hits = new RaycastHit[32];
            var source = sound.Source != null ? sound.Source.transform : null;
            Vector3[] axes = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int ray = 0; ray < 54 && planes.Count < 16; ray++)
            {
                Vector3 direction;
                if (ray < 6) direction = axes[ray];
                else
                {
                    float y = 1 - 2 * ((ray - 6 + 0.5f) / 48f);
                    float radius = Mathf.Sqrt(1 - y * y);
                    float angle = (ray - 6) * 2.39996323f;
                    direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }
                int count = Physics.RaycastNonAlloc(origin, direction, hits, field.Range, obstacleMask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) continue;
                int closest = -1;
                float nearest = float.MaxValue;
                for (int hit = 0; hit < count; hit++)
                {
                    Collider collider = hits[hit].collider;
                    if (collider is CharacterController || (source != null && collider.transform.IsChildOf(source))) continue;
                    if (hits[hit].distance < nearest) { nearest = hits[hit].distance; closest = hit; }
                }
                if (closest < 0) continue;
                RaycastHit contact = hits[closest];
                if (!(contact.collider is BoxCollider) && !(contact.collider is MeshCollider mesh && mesh.convex)) continue;
                AcousticSurface surface = AcousticSurface.Find(contact.collider);
                if (surface == null || surface.profile.reflection <= 0) continue;
                bool duplicate = planes.Exists(p => p.Collider == contact.collider &&
                    Vector3.Dot(p.Normal, contact.normal) > 0.999f && Mathf.Abs(Vector3.Dot(p.Point - contact.point, p.Normal)) < 0.02f);
                if (duplicate) continue;
                planes.Add(new PlaneSample { Collider = contact.collider, Point = contact.point, Normal = contact.normal,
                    Reflection = Mathf.Clamp01(surface.profile.reflection), Damping = Mathf.Max(0, surface.profile.reflectionDamping) });
            }
        }

        /// <summary>Returns the strongest valid sampled echo. Direct sound is handled separately.</summary>
        public bool TryGetEcho(Vector3 listener, out float pathLength, out float pressure)
        {
            pathLength = AcousticField.Unreachable;
            pressure = 0;
            foreach (PlaneSample plane in planes)
            {
                if (plane.Collider == null || !plane.Collider.enabled || !plane.Collider.gameObject.activeInHierarchy) continue;
                if (Vector3.Dot(listener - plane.Point, plane.Normal) <= 0.01f) continue;
                Vector3 mirroredSource = origin - 2 * Vector3.Dot(origin - plane.Point, plane.Normal) * plane.Normal;
                Vector3 line = listener - mirroredSource;
                float denominator = Vector3.Dot(line, plane.Normal);
                if (Mathf.Abs(denominator) < 0.0001f) continue;
                float fraction = Vector3.Dot(plane.Point - mirroredSource, plane.Normal) / denominator;
                if (fraction <= 0 || fraction >= 1) continue;
                Vector3 bounce = mirroredSource + line * fraction;
                // Reject a plane extension outside the actual collider face.
                if ((plane.Collider.ClosestPoint(bounce + plane.Normal * 0.01f) - bounce).sqrMagnitude > 0.0004f) continue;
                Vector3 visibleBounce = bounce + plane.Normal * 0.04f;
                if (!field.IsClear(origin, visibleBounce) || !field.IsClear(visibleBounce, listener)) continue;
                float distance = Vector3.Distance(origin, bounce) + Vector3.Distance(bounce, listener);
                if (distance > field.Range) continue;
                float gain = plane.Reflection * Mathf.Exp(-plane.Damping * distance) * AcousticField.Pressure(distance, distance);
                if (gain <= pressure) continue;
                pressure = gain;
                pathLength = distance;
            }
            return pressure > 0;
        }
    }
}
