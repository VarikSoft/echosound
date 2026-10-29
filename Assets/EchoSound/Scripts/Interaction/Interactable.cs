using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Interaction
{
    /// <summary>
    /// A scene object that the player and the companion use through one entry point, TryUse,
    /// so both follow the same rules. Subclasses describe their state in plain words for the companion's LLM.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> active = new List<Interactable>();
        public static IReadOnlyList<Interactable> Active => active;

        [Tooltip("Short unique id used in commands, for example door_3.")]
        public string id = "";
        [Tooltip("Where the object is, in words. Helps the LLM tell similar objects apart.")]
        public string locationHint = "";
        [Tooltip("Largest horizontal distance from the object's pivot at which it can be used.")]
        [Min(0.5f)] public float useDistance = 2f;

        /// <summary>One word, for example "door".</summary>
        public abstract string Kind { get; }
        /// <summary>Current state in words, for example "closed".</summary>
        public abstract string State { get; }
        /// <summary>Actions this kind of object supports. Whether one works right now is decided by CanUse.</summary>
        public abstract IReadOnlyList<InteractionAction> Actions { get; }
        /// <summary>The action behind a single "use" key, for example closing an open door.</summary>
        public virtual InteractionAction PrimaryAction => Actions[0];
        /// <summary>True while the object is still reacting to a use, for example a sliding door.</summary>
        public virtual bool IsBusy => false;

        /// <summary>Floor points from which an agent can use the object.</summary>
        public virtual IEnumerable<Vector3> StandPoints
        {
            get { yield return transform.position; }
        }

        public bool IsInReach(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            // The height limit stops use from a pit below or a walkway above.
            return Mathf.Abs(offset.y) < 2f && new Vector2(offset.x, offset.z).magnitude <= useDistance;
        }

        /// <summary>Whether the action is possible, distance aside. The companion asks before walking over.</summary>
        public bool CanUse(InteractionAction action, out string problem)
        {
            if (!Supports(action))
            {
                problem = $"you cannot {action.Verb()} a {Kind}";
                return false;
            }
            return CanPerform(action, out problem);
        }

        /// <summary>The only way to use an object, for the player and the companion alike.</summary>
        public bool TryUse(Interactor user, InteractionAction action, out string problem)
        {
            if (!IsInReach(user.transform.position))
            {
                problem = "it is too far away";
                return false;
            }
            if (!CanUse(action, out problem)) return false;

            // Later: emit the use sound here, so enemies hear every user in the same way.
            Perform(user, action);
            return true;
        }

        /// <summary>Object-specific rules, for example "already open" or "something is in the doorway".</summary>
        protected virtual bool CanPerform(InteractionAction action, out string problem)
        {
            problem = null;
            return true;
        }

        protected abstract void Perform(Interactor user, InteractionAction action);

        private bool Supports(InteractionAction action)
        {
            foreach (InteractionAction supported in Actions)
            {
                if (supported == action) return true;
            }
            return false;
        }

        protected virtual void OnEnable() => active.Add(this);
        protected virtual void OnDisable() => active.Remove(this);
    }
}
