using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Interaction
{
    /// <summary>
    /// Marks whoever uses objects: the player, the companion, later other players.
    /// Every one of them goes through Interactable.TryUse, so the same rules apply to all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Interactor : MonoBehaviour
    {
        [Tooltip("Who is acting, for logs and messages, for example Player or Companion.")]
        public string displayName = "";

        private static readonly List<Interactor> active = new List<Interactor>();
        /// <summary>Everyone who can act, for example to find who stands on a pressure plate.</summary>
        public static IReadOnlyList<Interactor> Active => active;

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}
