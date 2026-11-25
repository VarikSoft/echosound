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

        /// <summary>
        /// When true, another component decides when contacts happen (e.g. animation-synced footsteps)
        /// and calls EmitFootstep / EmitJump / EmitLanding; built-in distance timing is skipped.
        /// </summary>
        public bool ExternalTiming { get; set; }

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
            if (ExternalTiming)
            {
                // Keep tracking fresh so switching back does not emit a phantom contact.
                ResetTracking();
                return;
            }

            bool isGrounded = controller.isGrounded;
            bool movedThisFrame = false;

            // Detect Jump Takeoff (space pressed while on ground)
            if (isGrounded && Input.GetKeyDown(KeyCode.Space))
            {
                EmitJump();
            }

            // Detect Landing
            if (!wasGrounded && isGrounded)
            {
                EmitLanding(Mathf.Abs(peakVerticalVelocity));
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
                        EmitFootstep(isRunning);
                    }
                }
            }

            lastPosition = transform.position;
            wasGrounded = isGrounded;
            wasMoving = movedThisFrame;
        }

        public void EmitFootstep(bool running)
        {
            if (running) EmitContact(runRadius, 1.0f, SoundType.FootstepRun);
            else EmitContact(walkRadius, 0.6f, SoundType.FootstepWalk);
        }

        public void EmitJump() => EmitContact(jumpRadius, 1f, SoundType.Jump);

        /// <param name="fallSpeed">Downward speed at touchdown, m/s. Soft landings stay silent.</param>
        public void EmitLanding(float fallSpeed)
        {
            if (fallSpeed < minimumLandVelocity) return;
            float intensity = Mathf.Clamp01(fallSpeed / 10f);
            EmitContact(landRadius * Mathf.Max(0.5f, intensity), intensity, SoundType.Land);
        }

        private void EmitContact(float radius, float intensity, SoundType type)
        {
            Vector3 feet = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
            float gain = AcousticSurface.ContactGain(AcousticSurface.FindGround(feet, transform));
            SoundBus.Emit(transform.position, radius, intensity * gain, type, gameObject);
        }
    }
}
