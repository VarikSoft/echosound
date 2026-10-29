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
    }
}
