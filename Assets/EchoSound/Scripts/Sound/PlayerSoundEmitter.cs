using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Tracks player character movement (footsteps, jumping, landing) and emits sound events into SoundBus.
    /// Works with any CharacterController-based player movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("EchoSound/Sound/Player Sound Emitter")]
    public class PlayerSoundEmitter : MonoBehaviour
    {
        [Header("Footsteps")]
        [Tooltip("Hearing/illumination radius for walking footsteps in meters.")]
        public float walkRadius = 14f;

        [Tooltip("Hearing/illumination radius for running footsteps in meters.")]
        public float runRadius = 24f;

        [Tooltip("Distance in meters traveled between footsteps while walking.")]
        public float walkStepDistance = 2.0f;

        [Tooltip("Distance in meters traveled between footsteps while running.")]
        public float runStepDistance = 2.4f;

        [Header("Jumping & Landing")]
        [Tooltip("Hearing/illumination radius when jumping.")]
        public float jumpRadius = 16f;

        [Tooltip("Hearing/illumination radius when landing.")]
        public float landRadius = 22f;

        [Tooltip("Minimum fall distance/speed required to trigger a landing sound.")]
        public float minimumLandVelocity = 3f;

        private CharacterController controller;
        private Vector3 lastPosition;
        private float distanceTravelled;
        private bool wasGrounded;
        private float peakVerticalVelocity;
        private bool wasMoving;
        private float nextFootstepTime;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            lastPosition = transform.position;
            wasGrounded = controller.isGrounded;
        }

        /// <summary>
        /// Resets tracked movement state. Call this after teleporting the player
        /// to prevent a phantom footstep from the large position delta.
        /// </summary>
        public void ResetTracking()
        {
            lastPosition = transform.position;
            distanceTravelled = 0f;
            peakVerticalVelocity = 0f;
            wasGrounded = controller != null && controller.isGrounded;
            wasMoving = false;
            nextFootstepTime = 0;
        }

        private void Update()
        {
            if (controller == null) return;

            bool isGrounded = controller.isGrounded;
            bool movedThisFrame = false;

            // Detect Jump Takeoff (space pressed while on ground)
            if (isGrounded && Input.GetKeyDown(KeyCode.Space))
            {
                SoundBus.Emit(transform.position, jumpRadius, 1f, SoundType.Jump, gameObject);
            }

            // Detect Landing
            if (!wasGrounded && isGrounded)
            {
                if (Mathf.Abs(peakVerticalVelocity) >= minimumLandVelocity)
                {
                    float intensity = Mathf.Clamp01(Mathf.Abs(peakVerticalVelocity) / 10f);
                    SoundBus.Emit(transform.position, landRadius * Mathf.Max(0.5f, intensity), intensity, SoundType.Land, gameObject);
                }
                peakVerticalVelocity = 0f;
            }

            if (!isGrounded)
            {
                // Track downward velocity during fall
                if (controller.velocity.y < peakVerticalVelocity)
                {
                    peakVerticalVelocity = controller.velocity.y;
                }
            }
            else
            {
                // Track horizontal movement for footsteps
                Vector3 horizontalDelta = transform.position - lastPosition;
                horizontalDelta.y = 0;
                float stepLength = horizontalDelta.magnitude;

                // Ignore huge position jumps caused by teleports or lag spikes.
                if (stepLength > 0.001f && stepLength < 5f)
                {
                    movedThisFrame = true;
                    bool isRunning = Input.GetKey(KeyCode.LeftShift);
                    float targetStepDistance = isRunning ? runStepDistance : walkStepDistance;
                    // First contact reveals a route as movement begins. Later contacts follow stride length.
                    if (!wasMoving) distanceTravelled = targetStepDistance;
                    distanceTravelled += stepLength;

                    if (distanceTravelled >= targetStepDistance && Time.time >= nextFootstepTime)
                    {
                        distanceTravelled -= targetStepDistance;
                        nextFootstepTime = Time.time + 0.18f;
                        float radius = isRunning ? runRadius : walkRadius;
                        SoundType type = isRunning ? SoundType.FootstepRun : SoundType.FootstepWalk;
                        float intensity = isRunning ? 1.0f : 0.6f;

                        SoundBus.Emit(transform.position, radius, intensity, type, gameObject);
                    }
                }
            }

            lastPosition = transform.position;
            wasGrounded = isGrounded;
            wasMoving = movedThisFrame;
        }
    }
}
