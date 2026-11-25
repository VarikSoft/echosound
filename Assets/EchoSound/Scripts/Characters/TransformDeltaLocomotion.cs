using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>
    /// Derives locomotion from how the transform moves. Suits anything moved externally:
    /// network players (only position needs replicating), the mimic, scripted characters.
    /// </summary>
    public sealed class TransformDeltaLocomotion : MonoBehaviour, ILocomotionSource
    {
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Velocity smoothing time; hides network jitter.")]
        [SerializeField, Min(0f)] private float smoothing = 0.08f;
        [Tooltip("Moves faster than this are treated as teleports.")]
        [SerializeField, Min(1f)] private float teleportSpeed = 50f;

        private Vector3 lastPosition;

        public Vector3 Velocity { get; private set; }
        public bool IsGrounded { get; private set; } = true;

        private void OnEnable()
        {
            lastPosition = transform.position;
            Velocity = Vector3.zero;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;

            Vector3 position = transform.position;
            Vector3 raw = (position - lastPosition) / dt;
            lastPosition = position;
            if (raw.sqrMagnitude > teleportSpeed * teleportSpeed) raw = Vector3.zero;

            float k = smoothing <= 0 ? 1 : 1 - Mathf.Exp(-dt / smoothing);
            Velocity = Vector3.Lerp(Velocity, raw, k);

            const float lift = 0.1f;
            IsGrounded = Physics.Raycast(position + Vector3.up * lift, Vector3.down,
                lift + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
