using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>Shared default clips. Characters override individual slots on their CharacterAnimator.</summary>
    [CreateAssetMenu(menuName = "EchoSound/Character Animation Set", fileName = "CharacterAnimations")]
    public sealed class CharacterAnimationSet : ScriptableObject
    {
        public CharacterAnimationClips clips = new CharacterAnimationClips();
    }
}
