using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound
{
    /// <summary>
    /// The player's hands: the use key acts on the object under the crosshair,
    /// through the same Interactable.TryUse rules the companion follows.
    /// </summary>
    [RequireComponent(typeof(Interactor))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public Camera view;
        public KeyCode useKey = KeyCode.E;
        [Tooltip("How far the crosshair looks for objects. The object's own use distance still applies.")]
        [Min(0.5f)] public float rayLength = 4f;

        private Interactor interactor;

        private void Awake()
        {
            interactor = GetComponent<Interactor>();
            if (view == null) view = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            // An unlocked cursor means the player is typing or using UI.
            if (view == null || Cursor.lockState != CursorLockMode.Locked || !Input.GetKeyDown(useKey)) return;

            Transform eye = view.transform;
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, rayLength, ~0, QueryTriggerInteraction.Ignore)) return;

            Interactable item = hit.collider.GetComponentInParent<Interactable>();
            if (item == null) return;

            InteractionAction action = item.PrimaryAction;
            if (!item.TryUse(interactor, action, out string problem))
            {
                // There is no HUD yet, so the reason goes to the console.
                Debug.Log($"Cannot {action.Verb()} {item.id}: {problem}.", item);
            }
        }
    }
}
