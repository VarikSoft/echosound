using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Companion
{
    /// <summary>
    /// The curator's voice as orders: collects a phrase while push-to-talk is held,
    /// sends it to speech recognition on release and gives the text to the companion.
    /// </summary>
    public sealed class CuratorVoice : MonoBehaviour
    {
        public CompanionBrain companion;
        public PushToTalkVoice microphone;
        public WhisperClient whisper = new WhisperClient();
        [Tooltip("Less speech than this is an accidental press. Whisper invents text for silence, and every order interrupts the companion.")]
        [Min(0)] public float minSpeechSeconds = 0.2f;
        [Min(1)] public int maxPhraseSeconds = 20;

        /// <summary>Short progress text; empty when idle.</summary>
        public event Action<string> StatusChanged;
        /// <summary>Speech recognition problems the player should see.</summary>
        public event Action<string> Failed;

        private readonly List<float> phrase = new List<float>();
        private int phraseRate;
        private float speechSeconds;
        private bool wasRecording;
        private Coroutine transcription;

        private void OnEnable()
        {
            if (microphone != null) microphone.PcmFrameCaptured += OnFrame;
        }

        private void OnDisable()
        {
            if (microphone != null) microphone.PcmFrameCaptured -= OnFrame;
            StopAllCoroutines();
            transcription = null;
            whisper.Cancel();
            wasRecording = false;
            ClearPhrase();
        }

        // The microphone reuses its buffer, so the frame is copied here.
        private void OnFrame(float[] pcm, int sampleRate, int channels)
        {
            phraseRate = sampleRate;
            int frames = pcm.Length / channels;
            if (microphone.IsSpeaking) speechSeconds += (float)frames / sampleRate;

            int room = Mathf.Min(frames, maxPhraseSeconds * sampleRate - phrase.Count);
            for (int frame = 0; frame < room; frame++)
            {
                float sum = 0;
                for (int channel = 0; channel < channels; channel++) sum += pcm[frame * channels + channel];
                phrase.Add(sum / channels);
            }
        }

        private void Update()
        {
            if (microphone == null) return;
            if (microphone.IsRecording)
            {
                wasRecording = true;
                return;
            }
            if (!wasRecording) return;
            wasRecording = false;

            // Only releasing the key sends the phrase. A lost microphone, Esc or switching away drops it.
            bool released = microphone.State != PushToTalkVoice.CaptureState.Unavailable && Application.isFocused
                && Cursor.lockState == CursorLockMode.Locked && !Input.GetKey(KeyCode.Escape);
            if (released && speechSeconds >= minSpeechSeconds) Submit(phrase.ToArray(), phraseRate);
            ClearPhrase();
        }

        /// <summary>Transcribes a finished phrase and gives it to the companion. The newest phrase wins.</summary>
        public void Submit(float[] mono, int sampleRate)
        {
            if (transcription != null) StopCoroutine(transcription);
            transcription = StartCoroutine(Transcribe(mono, sampleRate));
        }

        private IEnumerator Transcribe(float[] mono, int sampleRate)
        {
            StatusChanged?.Invoke("transcribing");
            string text = null;
            string error = null;
            yield return whisper.Transcribe(mono, sampleRate, result => text = result, problem => error = problem);
            transcription = null;
            StatusChanged?.Invoke("");

            if (error != null)
            {
                Debug.LogWarning("[Companion] " + error, this);
                Failed?.Invoke(error);
                yield break;
            }
            text = WhisperClient.CleanTranscript(text);
            if (text.Length > 0 && companion != null) companion.Command(text);
        }

        private void ClearPhrase()
        {
            phrase.Clear();
            speechSeconds = 0;
        }
    }
}
