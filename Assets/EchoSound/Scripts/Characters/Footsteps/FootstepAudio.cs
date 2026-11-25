using EchoSound.Sound;
using UnityEngine;

namespace EchoSound.Characters
{
    /// <summary>
    /// Plays footstep sounds for any <see cref="IFootstepSource"/>, chosen by the acoustic material underfoot.
    /// Loudness follows the surface's Contact Loudness, like the gameplay sound events.
    /// </summary>
    public sealed class FootstepAudio : MonoBehaviour
    {
        [Tooltip("This character's footstep sounds; give another character a different bank.")]
        [SerializeField] private FootstepSoundBank bank;
        [Tooltip("Optional. A 3D source is created when empty.")]
        [SerializeField] private AudioSource output;
        [SerializeField, Min(0.1f)] private float maxDistance = 25f;

        private IFootstepSource source;
        private readonly int[] lastClip = { -1, -1, -1, -1 };

        public FootstepSoundBank Bank
        {
            get => bank;
            set => bank = value;
        }

        private void Awake()
        {
            if (output != null) return;
            output = gameObject.AddComponent<AudioSource>();
            output.playOnAwake = false;
            output.spatialBlend = 1;
            output.rolloffMode = AudioRolloffMode.Logarithmic;
            output.minDistance = 1;
            output.maxDistance = maxDistance;
        }

        private void OnEnable()
        {
            source = GetComponentInParent<IFootstepSource>();
            if (source == null) source = GetComponentInChildren<IFootstepSource>();
            if (source != null) source.Footstep += Play;
            else Debug.LogWarning("FootstepAudio found no IFootstepSource.", this);
        }

        private void OnDisable()
        {
            if (source != null) source.Footstep -= Play;
        }

        private void Play(FootstepEvent step)
        {
            if (bank == null) return;
            // The character itself must not count as ground; transform.root may be a level container.
            Transform character = source is Component component ? component.transform : transform;
            AcousticSurface ground = AcousticSurface.FindGround(step.Position, character);
            FootstepSoundSet set = bank.Resolve(ground != null ? ground.profile : null, step.Kind);
            if (set == null) return;

            AudioClip clip = Pick(set.Clips(step.Kind), (int)step.Kind);
            if (clip == null) return;
            output.pitch = 1 + Random.Range(-set.pitchVariation, set.pitchVariation);
            output.PlayOneShot(clip, set.volume * step.Intensity * AcousticSurface.ContactGain(ground));
        }

        // Random clip, never the same one twice in a row for a kind.
        private AudioClip Pick(AudioClip[] clips, int kind)
        {
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClip[kind]) index = (index + 1) % clips.Length;
            lastClip[kind] = index;
            return clips[index];
        }
    }
}
