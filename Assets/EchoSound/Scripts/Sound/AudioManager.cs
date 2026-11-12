using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace EchoSound.Sound
{
    /// <summary>
    /// Central audio manager that manages audio source pooling, 
    /// playback throttling, coordination with the acoustic SoundBus,
    /// and AudioMixer volume channels (Master, SFX, Voice, Ambience).
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

        [Header("Audio Mixer & Groups")]
        public AudioMixer masterMixer;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup voiceGroup;
        public AudioMixerGroup ambienceGroup;
        public AudioMixerGroup uiGroup;

        [Header("Mixer Exposed Parameters")]
        public string masterVolumeParam = "MasterVolume";
        public string sfxVolumeParam = "SFXVolume";
        public string voiceVolumeParam = "VoiceVolume";
        public string ambienceVolumeParam = "AmbienceVolume";

        [Header("Pool Configuration")]
        [SerializeField] private int initialPoolSize = 16;
        [SerializeField] private int maxPoolCapacity = 32;

        private readonly Stack<PooledAudioSource> pool = new Stack<PooledAudioSource>();
        private readonly List<PooledAudioSource> activeSources = new List<PooledAudioSource>();

        public int ActiveCount => activeSources.Count;
        public int AvailableCount => pool.Count;

        // Runtime Volume Properties (0.0 to 1.0 linear)
        public float MasterVolume { get; private set; } = 1f;
        public float SFXVolume { get; private set; } = 1f;
        public float VoiceVolume { get; private set; } = 1f;
        public float AmbienceVolume { get; private set; } = 1f;
        public bool IsMuted { get; private set; } = false;

        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnSFXVolumeChanged;
        public event Action<float> OnVoiceVolumeChanged;
        public event Action<float> OnAmbienceVolumeChanged;
        public event Action<bool> OnMuteChanged;

        private const string PrefsPrefix = "EchoSound_Audio_";

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

            LoadVolumePreferences();
            WarmPool(initialPoolSize);
        }

        /// <summary>
        /// Converts linear amplitude [0.0, 1.0] to decibels [-80 dB, 0 dB].
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Clamp(Mathf.Log10(linear) * 20f, -80f, 0f);
        }

        /// <summary>
        /// Converts decibels [-80 dB, 0 dB] to linear amplitude [0.0, 1.0].
        /// </summary>
        public static float DecibelsToLinear(float db)
        {
            if (db <= -80f) return 0f;
            return Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
        }

        public void SetMasterVolume(float linear)
        {
            MasterVolume = Mathf.Clamp01(linear);
            ApplyMixerVolume(masterVolumeParam, IsMuted ? 0f : MasterVolume);
            AudioListener.volume = IsMuted ? 0f : MasterVolume;
            PlayerPrefs.SetFloat(PrefsPrefix + "Master", MasterVolume);
            OnMasterVolumeChanged?.Invoke(MasterVolume);
        }

        public void SetSFXVolume(float linear)
        {
            SFXVolume = Mathf.Clamp01(linear);
            ApplyMixerVolume(sfxVolumeParam, SFXVolume);
            PlayerPrefs.SetFloat(PrefsPrefix + "SFX", SFXVolume);
            OnSFXVolumeChanged?.Invoke(SFXVolume);
        }

        public void SetVoiceVolume(float linear)
        {
            VoiceVolume = Mathf.Clamp01(linear);
            ApplyMixerVolume(voiceVolumeParam, VoiceVolume);
            PlayerPrefs.SetFloat(PrefsPrefix + "Voice", VoiceVolume);
            OnVoiceVolumeChanged?.Invoke(VoiceVolume);
        }

        public void SetAmbienceVolume(float linear)
        {
            AmbienceVolume = Mathf.Clamp01(linear);
            ApplyMixerVolume(ambienceVolumeParam, AmbienceVolume);
            PlayerPrefs.SetFloat(PrefsPrefix + "Ambience", AmbienceVolume);
            OnAmbienceVolumeChanged?.Invoke(AmbienceVolume);
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            ApplyMixerVolume(masterVolumeParam, IsMuted ? 0f : MasterVolume);
            AudioListener.volume = IsMuted ? 0f : MasterVolume;
            PlayerPrefs.SetInt(PrefsPrefix + "Muted", IsMuted ? 1 : 0);
            OnMuteChanged?.Invoke(IsMuted);
        }

        private void ApplyMixerVolume(string paramName, float linear)
        {
            if (masterMixer == null || string.IsNullOrEmpty(paramName)) return;
            float db = LinearToDecibels(linear);
            masterMixer.SetFloat(paramName, db);
        }

        private void LoadVolumePreferences()
        {
            MasterVolume = PlayerPrefs.GetFloat(PrefsPrefix + "Master", 1f);
            SFXVolume = PlayerPrefs.GetFloat(PrefsPrefix + "SFX", 1f);
            VoiceVolume = PlayerPrefs.GetFloat(PrefsPrefix + "Voice", 1f);
            AmbienceVolume = PlayerPrefs.GetFloat(PrefsPrefix + "Ambience", 1f);
            IsMuted = PlayerPrefs.GetInt(PrefsPrefix + "Muted", 0) == 1;

            SetMasterVolume(MasterVolume);
            SetSFXVolume(SFXVolume);
            SetVoiceVolume(VoiceVolume);
            SetAmbienceVolume(AmbienceVolume);
            if (IsMuted) SetMuted(true);
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

            AudioMixerGroup targetGroup = cue.mixerGroup != null ? cue.mixerGroup : sfxGroup;

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
                targetGroup,
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

            AudioMixerGroup targetGroup = cue.mixerGroup != null ? cue.mixerGroup : (uiGroup != null ? uiGroup : sfxGroup);

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
                targetGroup,
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
                sfxGroup,
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
