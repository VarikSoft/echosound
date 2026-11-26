using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// Casts a beam along its forward axis, bouncing off mirrors. A receiver at the end of the beam turns on.
    /// Anything solid in the way, including the player, blocks it.
    /// </summary>
    public sealed class LaserEmitter : MonoBehaviour
    {
        [Tooltip("Optional. Without it the laser is always on.")]
        public Signal power;
        [Min(1)] public float maxDistance = 40;
        [Range(0, 16)] public int maxBounces = 4;
        [Min(0.001f)] public float beamWidth = 0.03f;
        public Color beamColor = Color.red;
        [Tooltip("Optional. Empty means an unlit default material tinted with the beam colour.")]
        public Material beamMaterial;

        private LineRenderer beam;
        private readonly List<Vector3> points = new List<Vector3>();

        private void Awake()
        {
            // The emitter sets up its own line, so a prefab needs only this component.
            if (!TryGetComponent(out beam)) beam = gameObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.widthMultiplier = beamWidth;
            beam.startColor = beam.endColor = beamColor;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.sharedMaterial = beamMaterial != null ? beamMaterial : new Material(Shader.Find("Sprites/Default"));
        }

        private void Update()
        {
            points.Clear();
            if (power == null || power.IsOn) Trace();
            beam.positionCount = points.Count;
            for (int index = 0; index < points.Count; index++) beam.SetPosition(index, points[index]);
        }

        private void Trace()
        {
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            points.Add(origin);

            for (int bounce = 0; bounce <= maxBounces; bounce++)
            {
                // Start a little ahead so the beam does not hit the emitter itself or the mirror it just left.
                if (!Physics.Raycast(origin + direction * 0.05f, direction, out RaycastHit hit, maxDistance,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    points.Add(origin + direction * maxDistance);
                    return;
                }

                points.Add(hit.point);
                LaserReceiver receiver = hit.collider.GetComponentInParent<LaserReceiver>();
                if (receiver != null) receiver.Illuminate();
                if (hit.collider.GetComponentInParent<LaserMirror>() == null) return;

                direction = Vector3.Reflect(direction, hit.normal);
                origin = hit.point;
            }
        }
    }
}
