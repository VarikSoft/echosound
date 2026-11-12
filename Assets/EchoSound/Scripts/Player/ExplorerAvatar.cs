using UnityEngine;
using UnityEngine.Rendering;
using EchoSound.Sound;

namespace EchoSound
{
    /// <summary>Drives the in-place rig from actual controller motion, including blocked movement.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ExplorerAvatar : MonoBehaviour
    {
        public Animator animator;
        public Renderer headRenderer;
        public Transform head;
        public Camera ownerCamera;
        public bool driveFromPlayer = true;

        private CharacterController controller;
        private FirstPersonPlayer player;
        private PushToTalkVoice voice;
        private Vector3 previousPosition;
        private bool grounded = true;
        private float airTime;
        private ShadowCastingMode originalShadows;
        private bool headHidden;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Rising = Animator.StringToHash("Rising");
        private static readonly int Land = Animator.StringToHash("Land");

        private void Awake()
        {
            controller = GetComponentInParent<CharacterController>();
            player = GetComponentInParent<FirstPersonPlayer>();
            voice = GetComponentInParent<PushToTalkVoice>();
            if (ownerCamera == null && player != null) ownerCamera = player.view;
            ResetMotion();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            Camera.onPostRender -= AfterCamera;
            RestoreHead();
        }

        public void ResetMotion()
        {
            previousPosition = transform.position;
            airTime = 0;
            grounded = true;
            if (animator == null || !animator.isInitialized) return;
            animator.SetFloat(Speed, 0);
            animator.SetBool(Grounded, true);
            animator.SetBool(Rising, false);
            animator.ResetTrigger(Land);
            animator.Play("Locomotion", 0, 0);
        }

        private void Update()
        {
            if (!driveFromPlayer || controller == null || animator == null || Time.deltaTime <= 0) return;
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            if (delta.sqrMagnitude > 25) { ResetMotion(); return; }
            float speed = new Vector2(delta.x, delta.z).magnitude / Time.deltaTime;
            // Animator thresholds use the prefab's walk/run speeds; preserve them when movement is tuned.
            if (player != null)
                speed = speed <= player.walkSpeed
                    ? speed / Mathf.Max(0.01f, player.walkSpeed) * 4.5f
                    : 4.5f + (speed - player.walkSpeed) / Mathf.Max(0.01f, player.runSpeed - player.walkSpeed) * 3;
            bool onGround = controller.isGrounded;
            if (!onGround) airTime += Time.deltaTime;
            if (onGround && !grounded && airTime > 0.12f) animator.SetTrigger(Land);
            if (onGround) airTime = 0;
            animator.SetFloat(Speed, speed, 0.12f, Time.deltaTime);
            animator.SetBool(Grounded, onGround);
            animator.SetBool(Rising, controller.velocity.y > 0.1f);
            grounded = onGround;
        }

        private void LateUpdate()
        {
            if (head == null || !driveFromPlayer || ownerCamera == null) return;
            if (voice == null && player != null) voice = player.GetComponent<PushToTalkVoice>();
            float pitch = Mathf.DeltaAngle(0, ownerCamera.transform.localEulerAngles.x);
            float speaking = voice != null ? voice.Level : 0;
            head.localRotation *= Quaternion.Euler(Mathf.Clamp(pitch * 0.45f, -25, 30)
                + Mathf.Sin(Time.time * 16) * speaking * 3, 0, 0);
        }

        // Hide only this owner's helmet for its camera. Other views retain the whole character.
        private void BeforeCamera(Camera camera)
        {
            if (camera != ownerCamera || headRenderer == null) return;
            originalShadows = headRenderer.shadowCastingMode;
            headRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            headHidden = true;
        }

        private void AfterCamera(Camera camera) { if (camera == ownerCamera) RestoreHead(); }
        private void RestoreHead()
        {
            if (headHidden && headRenderer != null) headRenderer.shadowCastingMode = originalShadows;
            headHidden = false;
        }
    }
}
