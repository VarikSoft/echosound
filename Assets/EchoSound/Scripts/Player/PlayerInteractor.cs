using System.Collections.Generic;
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
        public KeyCode useKey = KeyCode.Mouse0;
        [Tooltip("How far the crosshair looks for objects. The object's own use distance still applies.")]
        [Min(0.5f)] public float rayLength = 4f;

        private Interactor interactor;

        private void Awake()
        {
            interactor = GetComponent<Interactor>();
            if (view == null) view = GetComponentInChildren<Camera>();
        }

        // The object under the crosshair this frame, or null.
        private Interactable aimed;

        private void Update()
        {
            // An unlocked cursor means the player is typing or using UI.
            aimed = view != null && Cursor.lockState == CursorLockMode.Locked ? FindAimed() : null;
            if (aimed == null || !Input.GetKeyDown(useKey)) return;

            InteractionAction action = aimed.PrimaryAction;
            if (!aimed.TryUse(interactor, action, out string problem))
            {
                Debug.Log($"Cannot {action.Verb()} {aimed.id}: {problem}.", aimed);
            }
        }

        private Interactable FindAimed()
        {
            Transform eye = view.transform;
            // Triggers count, so an object can have a generous invisible use zone around its small parts.
            int count = Physics.RaycastNonAlloc(eye.position, eye.forward, hits, rayLength, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, 0, count, HitDistance);
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index].collider;
                if (hit.transform.IsChildOf(transform)) continue; // Our own body.
                Interactable item = hit.GetComponentInParent<Interactable>();
                if (item != null) return item;
                // Other trigger volumes do not block the view; walls and floors do.
                if (!hit.isTrigger) return null;
            }
            return null;
        }

        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static readonly IComparer<RaycastHit> HitDistance =
            Comparer<RaycastHit>.Create((first, second) => first.distance.CompareTo(second.distance));

        // Minimal HUD: a crosshair dot, and what the use key would do to the aimed object.
        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.Label(new Rect(centre.x - 4, centre.y - 10, 12, 20), "•");
            if (aimed == null) return;

            string hint = aimed.IsInReach(transform.position)
                ? $"{useKey}: {aimed.PrimaryAction.Verb()} {aimed.Kind} ({aimed.State})"
                : $"{aimed.Kind}: too far";
            GUI.Label(new Rect(centre.x - 150, centre.y + 14, 300, 22), hint,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter });
        }
    }
}
