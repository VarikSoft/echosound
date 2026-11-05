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
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1);
            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

            // A small downward velocity keeps the CharacterController grounded.
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space)) verticalSpeed = jumpSpeed;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
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
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable() => SetCursorLocked(false);
    }
}
