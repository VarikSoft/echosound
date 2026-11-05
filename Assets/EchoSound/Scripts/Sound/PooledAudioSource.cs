using System;
using System.Collections;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Wrapper for pooled AudioSource instances. Manages follow-targets,
    /// tracking active playback, and returning to the pool upon completion.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioSource : MonoBehaviour
    {
        private AudioSource audioSource;
        private Transform followTarget;
        private Vector3 followOffset;
        private Action<PooledAudioSource> onRelease;
        private Coroutine returnCoroutine;
        private bool isPlaying;

        public AudioSource Source => audioSource != null ? audioSource : (audioSource = GetComponent<AudioSource>());
        public bool IsPlaying => isPlaying;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void LateUpdate()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + followOffset;
            }
        }

        /// <summary>
        /// Configures and begins playing an audio clip with the given settings.
        /// </summary>
        public void Play(
            AudioClip clip,
            float volume,
            float pitch,
            float spatialBlend,
            AudioRolloffMode rolloffMode,
            float minDistance,
            float maxDistance,
            UnityEngine.Audio.AudioMixerGroup mixerGroup,
            Transform parentOrFollow,
            Action<PooledAudioSource> releaseCallback)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            onRelease = releaseCallback;
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.pitch = pitch;
            audioSource.spatialBlend = spatialBlend;
            audioSource.rolloffMode = rolloffMode;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.outputAudioMixerGroup = mixerGroup;
            audioSource.loop = false;

            if (parentOrFollow != null)
            {
                followTarget = parentOrFollow;
                followOffset = transform.position - followTarget.position;
            }
            else
            {
                followTarget = null;
            }

            audioSource.Play();
            isPlaying = true;

            if (returnCoroutine != null) StopCoroutine(returnCoroutine);

            // Compute expected duration taking pitch into account
            float duration = clip != null && pitch > 0f ? (clip.length / pitch) + 0.1f : 0.1f;
            returnCoroutine = StartCoroutine(WaitAndRelease(duration));
        }

        /// <summary>
        /// Immediately stops playback and returns this source to the pool.
        /// </summary>
        public void StopAndRelease()
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            audioSource.clip = null;
            followTarget = null;
            isPlaying = false;

            onRelease?.Invoke(this);
            onRelease = null;
        }

        private IEnumerator WaitAndRelease(float duration)
        {
            yield return new WaitForSeconds(duration);
            StopAndRelease();
        }
    }
}
