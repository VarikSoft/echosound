using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Central audio manager that manages audio source pooling, 
    /// playback throttling, and coordination with the acoustic SoundBus.
    /// </summary>
    [AddComponentMenu("EchoSound/Sound/Audio Manager")]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AudioManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("[AudioManager]");
                        instance = go.AddComponent<AudioManager>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        [Header("Pool Configuration")]
        [SerializeField] private int initialPoolSize = 16;
        [SerializeField] private int maxPoolCapacity = 32;

        private readonly Stack<PooledAudioSource> pool = new Stack<PooledAudioSource>();
        private readonly List<PooledAudioSource> activeSources = new List<PooledAudioSource>();

        public int ActiveCount => activeSources.Count;
        public int AvailableCount => pool.Count;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            WarmPool(initialPoolSize);
        }

        /// <summary>
        /// Warms the pool with pre-instantiated audio sources.
        /// </summary>
        public void WarmPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (pool.Count + activeSources.Count >= maxPoolCapacity) break;
                pool.Push(CreateSourceInstance());
            }
        }

        /// <summary>
        /// Plays a configured SoundCue in 3D world space.
        /// </summary>
        public AudioSource Play(SoundCue cue, Vector3 position, Transform followTarget = null)
        {
            if (cue == null) return null;

            float now = Application.isPlaying ? Time.time : 0f;
            if (!cue.CanPlay(now)) return null;

            var (clip, volume, pitch) = cue.EvaluatePlayback();
            if (clip == null) return null;

            // Emit to acoustic bus if requested
            if (cue.emitToSoundBus)
            {
                GameObject sourceObj = followTarget != null ? followTarget.gameObject : null;
                SoundBus.Emit(position, cue.busRadius, cue.busIntensity, cue.soundType, sourceObj);
            }

            PooledAudioSource pooled = GetSource();
            pooled.transform.position = position;
            pooled.Play(
                clip,
                volume,
                pitch,
                cue.spatialBlend,
                cue.rolloffMode,
                cue.minDistance,
                cue.maxDistance,
                cue.mixerGroup,
                followTarget,
                ReleaseSource);

            return pooled.Source;
        }

        /// <summary>
        /// Plays a configured SoundCue in 2D (non-spatialized, UI or ambient).
        /// </summary>
        public AudioSource Play2D(SoundCue cue)
        {
            if (cue == null) return null;

            float now = Application.isPlaying ? Time.time : 0f;
            if (!cue.CanPlay(now)) return null;

            var (clip, volume, pitch) = cue.EvaluatePlayback();
            if (clip == null) return null;

            PooledAudioSource pooled = GetSource();
            pooled.transform.position = Vector3.zero;
            pooled.Play(
                clip,
                volume,
                pitch,
                0f, // 2D
                AudioRolloffMode.Logarithmic,
                1f,
                500f,
                cue.mixerGroup,
                null,
                ReleaseSource);

            return pooled.Source;
        }

        /// <summary>
        /// Plays an ad-hoc AudioClip at a position.
        /// </summary>
        public AudioSource PlayClip(
            AudioClip clip,
            Vector3 position,
            float volume = 1f,
            float pitch = 1f,
            float spatialBlend = 1f,
            float minDistance = 1f,
            float maxDistance = 20f,
            Transform followTarget = null)
        {
            if (clip == null) return null;

            PooledAudioSource pooled = GetSource();
            pooled.transform.position = position;
            pooled.Play(
                clip,
                volume,
                pitch,
                spatialBlend,
                AudioRolloffMode.Logarithmic,
                minDistance,
                maxDistance,
                null,
                followTarget,
                ReleaseSource);

            return pooled.Source;
        }

        /// <summary>
        /// Stops all currently active audio sources and returns them to the pool.
        /// </summary>
        public void StopAll()
        {
            for (int i = activeSources.Count - 1; i >= 0; i--)
            {
                activeSources[i].StopAndRelease();
            }
        }

        /// <summary>
        /// Cleans up pool and resets state (useful for tests and scene unloads).
        /// </summary>
        public void ResetState()
        {
            StopAll();
            while (pool.Count > 0)
            {
                var s = pool.Pop();
                if (s != null)
                {
                    if (Application.isPlaying) Destroy(s.gameObject);
                    else DestroyImmediate(s.gameObject);
                }
            }
            activeSources.Clear();
        }

        private PooledAudioSource GetSource()
        {
            PooledAudioSource source;
            if (pool.Count > 0)
            {
                source = pool.Pop();
            }
            else if (activeSources.Count < maxPoolCapacity)
            {
                source = CreateSourceInstance();
            }
            else
            {
                // Pool exhausted: steal oldest active source
                source = activeSources[0];
                source.StopAndRelease();
                // Source is now in pool, pop it
                source = pool.Pop();
            }

            source.gameObject.SetActive(true);
            activeSources.Add(source);
            return source;
        }

        private void ReleaseSource(PooledAudioSource source)
        {
            if (source == null) return;

            activeSources.Remove(source);
            source.gameObject.SetActive(false);

            if (!pool.Contains(source))
            {
                pool.Push(source);
            }
        }

        private PooledAudioSource CreateSourceInstance()
        {
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<PooledAudioSource>();
            go.SetActive(false);
            return src;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
