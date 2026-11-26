using System;
using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;
using EchoSound.Combat;

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
        [Tooltip("When enabled, uses WASD and Arrow keys exclusively, ignoring any connected CAD controllers (e.g. 3Dconnexion SpaceMouse) or virtual joysticks.")]
        public bool useKeyboardOnly = true;

        [Tooltip("Allow movement input from connected joysticks/gamepads if useKeyboardOnly is false.")]
        public bool enableGamepad = false;

        [Tooltip("Deadzone threshold for joystick axis input to prevent controller drift.")]
        [Range(0.05f, 0.5f)] public float joystickDeadzone = 0.35f;

        [Header("Camera")]
        public Camera view;
        public float sensitivity = 2f;

        private CharacterController controller;
        private PlayerSoundEmitter soundEmitter;
        private StaminaComponent stamina;
        private HealthComponent health;
        private float cameraPitch;
        private float verticalSpeed;
        private bool hasPauseMenu;

        public StaminaComponent Stamina => stamina != null ? stamina : (stamina = GetComponent<StaminaComponent>());
        public HealthComponent Health => health != null ? health : (health = GetComponent<HealthComponent>());

        public void SetSensitivity(float value)
        {
            sensitivity = Mathf.Clamp(value, 0.2f, 10f);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            soundEmitter = GetComponent<PlayerSoundEmitter>();
            stamina = GetComponent<StaminaComponent>();
            health = GetComponent<HealthComponent>();
            hasPauseMenu = GetComponent("PauseMenuController") != null;
            sensitivity = PlayerPrefs.GetFloat("EchoSound_MouseSensitivity", sensitivity);

            if (view == null)
            {
                Debug.LogError("Assign a camera to the player prefab.", this);
                enabled = false;
                return;
            }

            // Auto-detect 3Dconnexion / virtual joystick emulators that pollute axis 0 & 1
            string[] joyNames = Input.GetJoystickNames();
            if (joyNames != null)
            {
                foreach (string joy in joyNames)
                {
                    if (string.IsNullOrEmpty(joy)) continue;
                    if (joy.IndexOf("SpaceMouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        joy.IndexOf("3Dconnexion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        joy.IndexOf("3Dx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        joy.IndexOf("vJoy", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        useKeyboardOnly = true;
                        enableGamepad = false;
                        Debug.Log($"[FirstPersonPlayer] Detected 3D device '{joy}'. Keyboard-only mode enforced to prevent drift.");
                        break;
                    }
                }
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
            if (Time.timeScale <= 0f) return;
            if (!hasPauseMenu && Input.GetKeyDown(KeyCode.Escape))
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
            bool wantsToRun = Input.GetKey(KeyCode.LeftShift);
            bool isMoving = input.sqrMagnitude > 0.01f;

            bool isRunning = wantsToRun && isMoving && (stamina == null || stamina.CanSprint);
            float speed = isRunning ? runSpeed : walkSpeed;

            if (isRunning && stamina != null)
            {
                stamina.ConsumeSprint(Time.deltaTime);
            }

            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

            // A small downward velocity keeps the CharacterController grounded.
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
            {
                if (stamina == null || stamina.TryConsumeJump())
                {
                    verticalSpeed = jumpSpeed;
                }
            }
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private Vector2 GetMovementInput()
        {
            float h = 0f;
            float v = 0f;

            // Direct keyboard keys
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            // Only query joystick axes if keyboard is not active AND keyboard-only is false AND gamepad is enabled
            if (!useKeyboardOnly && enableGamepad && Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
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
