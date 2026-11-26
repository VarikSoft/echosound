using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// On while someone stands on it (the player, the companion) or something heavy lies on it (a crate).
    /// People are found by position rather than by colliders, because the companion has no collider.
    /// </summary>
    [RequireComponent(typeof(Signal))]
    public sealed class PressurePlate : MonoBehaviour
    {
        [Tooltip("Plate size on its local X and Z axes.")]
        public Vector2 size = new Vector2(1.5f, 1.5f);
        [Tooltip("Rigidbodies lighter than this do not press the plate.")]
        [Min(0)] public float minimumMass = 5;
        [Tooltip("The visible top, sunk while pressed.")]
        public Transform top;
        [Min(0)] public float pressDepth = 0.04f;

        private const float SenseHeight = 1;
        private static readonly Collider[] hits = new Collider[16];

        private Signal output;
        private Vector3 topRestPosition;

        public Signal Output => output != null ? output : output = GetComponent<Signal>();

        private void Awake()
        {
            if (top != null) topRestPosition = top.localPosition;
        }

        private void FixedUpdate()
        {
            bool pressed = IsPressed();
            Output.Set(pressed);
            if (top != null) top.localPosition = topRestPosition + (pressed ? Vector3.down * pressDepth : Vector3.zero);
        }

        private bool IsPressed()
        {
            foreach (Interactor someone in Interactor.Active)
            {
                if (Contains(someone.transform.position)) return true;
            }

            Vector3 centre = transform.TransformPoint(new Vector3(0, SenseHeight * 0.5f, 0));
            var halfExtents = new Vector3(size.x * 0.5f, SenseHeight * 0.5f, size.y * 0.5f);
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Rigidbody body = hits[index].attachedRigidbody;
                if (body != null && !body.isKinematic && body.mass >= minimumMass) return true;
            }
            return false;
        }

        private bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f
                && local.y > -0.3f && local.y < SenseHeight;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, SenseHeight * 0.5f, 0), new Vector3(size.x, SenseHeight, size.y));
        }
    }
}
