using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>Footstep clips for one kind of ground. A random clip of the matching kind plays per contact.</summary>
    [CreateAssetMenu(menuName = "EchoSound/Footsteps/Footstep sound set", fileName = "FootstepSounds")]
    public sealed class FootstepSoundSet : ScriptableObject
    {
        public AudioClip[] walk = new AudioClip[0];
        public AudioClip[] run = new AudioClip[0];
        public AudioClip[] jump = new AudioClip[0];
        public AudioClip[] land = new AudioClip[0];

        [Range(0, 1)] public float volume = 0.8f;
        [Tooltip("Random pitch offset per step, e.g. 0.06 = +/-6%.")]
        [Range(0, 0.3f)] public float pitchVariation = 0.06f;

        public AudioClip[] Clips(FootstepKind kind) => kind switch
        {
            FootstepKind.Walk => walk,
            FootstepKind.Run => run,
            FootstepKind.Jump => jump,
            _ => land
        };

        public bool Has(FootstepKind kind)
        {
            var clips = Clips(kind);
            return clips != null && clips.Length > 0;
        }
    }
}
