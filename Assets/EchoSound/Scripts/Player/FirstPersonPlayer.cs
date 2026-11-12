using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;

namespace EchoSound
{
    /// <summary>Player controls, sound emission, and camera echolocation integration.</summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerSoundEmitter))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 4.5f;
        public float runSpeed = 7.5f;
        public float jumpSpeed = 5f;

        [Header("Input Options")]
        [Tooltip("Allow movement input from connected joysticks/gamepads in addition to keyboard.")]
        public bool enableGamepad = true;
        [Tooltip("Deadzone threshold for joystick axis input to prevent controller drift.")]
        [Range(0.05f, 0.5f)] public float joystickDeadzone = 0.25f;

        [Header("Camera")]
        public Camera view;
        public float sensitivity = 2f;

        private CharacterController controller;
        private PlayerSoundEmitter soundEmitter;
        private float cameraPitch;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            soundEmitter = GetComponent<PlayerSoundEmitter>();

            if (view == null)
            {
                Debug.LogError("Assign a camera to the player prefab.", this);
                enabled = false;
                return;
            }

            var voice = GetComponent<PushToTalkVoice>();
            if (voice == null) voice = gameObject.AddComponent<PushToTalkVoice>();
            if (voice.mouth == null) voice.mouth = view.transform;

            // Ensure Echolocation effect is active on the player's view camera
            if (view.GetComponent<EcholocationEffect>() == null)
            {
                view.gameObject.AddComponent<EcholocationEffect>();
            }

            // Ensure EcholocationManager is present in the scene
            if (EcholocationManager.Instance == null)
            {
                var mgrGo = new GameObject("EcholocationManager");
                mgrGo.AddComponent<EcholocationManager>();
            }

            SetCursorLocked(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;

            UpdateCamera();
            UpdateMovement();
        }

        private void UpdateCamera()
        {
            float horizontalLook = Input.GetAxisRaw("Mouse X") * sensitivity;
            float verticalLook = Input.GetAxisRaw("Mouse Y") * sensitivity;
            transform.Rotate(0, horizontalLook, 0);
            cameraPitch = Mathf.Clamp(cameraPitch - verticalLook, -85, 85);
            view.transform.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
        }

        private void UpdateMovement()
        {
            Vector2 input = GetMovementInput();
            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

            // A small downward velocity keeps the CharacterController grounded.
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space)) verticalSpeed = jumpSpeed;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private Vector2 GetMovementInput()
        {
            float h = 0f;
            float v = 0f;

            // Check digital keyboard input first
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            // If no keyboard input was detected and gamepad input is enabled, query axes with deadzone filtering
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f) && enableGamepad)
            {
                float rawH = Input.GetAxisRaw("Horizontal");
                float rawV = Input.GetAxisRaw("Vertical");

                if (Mathf.Abs(rawH) > joystickDeadzone)
                {
                    h = Mathf.Sign(rawH) * Mathf.InverseLerp(joystickDeadzone, 1f, Mathf.Abs(rawH));
                }

                if (Mathf.Abs(rawV) > joystickDeadzone)
                {
                    v = Mathf.Sign(rawV) * Mathf.InverseLerp(joystickDeadzone, 1f, Mathf.Abs(rawV));
                }
            }

            Vector2 result = new Vector2(h, v);
            return Vector2.ClampMagnitude(result, 1f);
        }

        public void Teleport(Vector3 position)
        {
            // Disable the controller while teleporting so collision resolution does not resist the move.
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = 0;

            if (soundEmitter == null) soundEmitter = GetComponent<PlayerSoundEmitter>();
            if (soundEmitter != null) soundEmitter.ResetTracking();
            var avatar = GetComponentInChildren<ExplorerAvatar>();
            if (avatar != null) avatar.ResetMotion();
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable() => SetCursorLocked(false);
    }
}
