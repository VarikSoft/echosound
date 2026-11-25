using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>Locomotion of a locally simulated CharacterController (the local player).</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterControllerLocomotion : MonoBehaviour, ILocomotionSource
    {
        private CharacterController controller;

        public Vector3 Velocity => controller.velocity;
        public bool IsGrounded => controller.isGrounded;

        private void Awake() => controller = GetComponent<CharacterController>();
    }
}
