using System;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Local PTT capture. The microphone is opened only while V is held in focused gameplay.
    /// No loopback, recording to disk or network transport. Voice drives the shared sound bus.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("EchoSound/Sound/Push To Talk Voice")]
    public sealed class PushToTalkVoice : MonoBehaviour
    {
        public KeyCode talkKey = KeyCode.V;
        [Tooltip("Empty uses the OS default microphone. Otherwise use an exact Microphone.devices name.")]
        public string microphoneDevice = "";
        public Transform mouth;
        [Range(-70f, -15f)] public float noiseGateDb = -48f;
        [Range(-35f, -3f)] public float fullLevelDb = -18f;
        [Min(1f)] public float quietRadius = 10f;
        [Min(1f)] public float loudRadius = 26f;
        [Range(0.2f, 0.8f)] public float pulseInterval = 0.35f;
        public bool showIndicator = true;

        public enum CaptureState { Idle, Starting, Listening, Speaking, Unavailable }
        public CaptureState State { get; private set; }
        public bool IsRecording => recording != null;
        public float Level => activity.Level;
        public string Error { get; private set; }

        /// <summary>
        /// Optional local PCM consumer: interleaved samples, sample rate, channel count.
        /// The buffer is reused; copy it during the callback if it must be retained.
        /// Invoked only while PTT is held, including quiet frames for continuous timing.
        /// </summary>
        public event Action<float[], int, int> PcmFrameCaptured;

        private readonly VoiceActivity activity = new VoiceActivity();
        private AudioClip recording;
        private string activeDevice;
        private float[] frame;
        private int frameSamples, readPosition, lastPosition;
        private float startTime, lastDataTime, nextPulse;
        private bool wasHeld, focused = true;
        private GUIStyle labelStyle;

        private void Update()
        {
            bool held = focused && Application.isFocused && Cursor.lockState == CursorLockMode.Locked
                && Input.GetKey(talkKey) && !Input.GetKey(KeyCode.Escape) && Time.timeScale > 0f;
            if (!held)
            {
                if (wasHeld || IsRecording) StopCapture();
                wasHeld = false;
                return;
            }
            if (!wasHeld) BeginCapture();
            wasHeld = true;
            if (recording == null) return;
            try { ReadMicrophone(); }
            catch (Exception exception) { Fail("Microphone disconnected. Check input settings and press V again.", exception); }
        }

        private void BeginCapture()
        {
            StopCapture();
            Error = null;
            try
            {
                string[] devices = Microphone.devices;
                if (devices.Length == 0) { Fail("No microphone. Check Windows input and microphone access."); return; }
                activeDevice = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
                if (activeDevice != null && Array.IndexOf(devices, activeDevice) < 0)
                { Fail("Selected microphone is unavailable. Check Push To Talk Voice settings."); return; }
                Microphone.GetDeviceCaps(activeDevice, out int minRate, out int maxRate);
                int sampleRate = maxRate == 0 ? 24000 : Mathf.Clamp(24000, Mathf.Max(1, minRate), maxRate);
                recording = Microphone.Start(activeDevice, true, 2, sampleRate);
                if (recording == null) { Fail("Cannot open microphone. Allow microphone access in Windows settings."); return; }
                frameSamples = Mathf.Max(1, recording.frequency / 50); // 20 ms per channel
                frame = new float[frameSamples * recording.channels];
                readPosition = lastPosition = 0;
                startTime = lastDataTime = Time.realtimeSinceStartup;
                nextPulse = Time.time;
                State = CaptureState.Starting;
            }
            catch (Exception exception) { Fail("Cannot open microphone. Check input settings and press V again.", exception); }
        }

        private void ReadMicrophone()
        {
            float now = Time.realtimeSinceStartup;
            int position = Microphone.GetPosition(activeDevice);
            if (position < 0 || !Microphone.IsRecording(activeDevice))
            {
                if (now - startTime > 2f) Fail("Microphone did not start. Check access and press V again.");
                return;
            }
            if (position == lastPosition)
            {
                if (now - lastDataTime > 2f) Fail("No microphone data. Check the device and press V again.");
                return;
            }
            // After a stalled frame, skip old audio rather than emitting a burst of stale speech.
            if (now - lastDataTime > 0.25f)
                readPosition = (position - frameSamples + recording.samples) % recording.samples;
            lastPosition = position;
            lastDataTime = now;
            int available = (position - readPosition + recording.samples) % recording.samples;
            if (available > frameSamples * 12)
            {
                readPosition = (position - frameSamples * 12 + recording.samples) % recording.samples;
                available = frameSamples * 12;
            }
            while (available >= frameSamples)
            {
                if (!recording.GetData(frame, readPosition)) { Fail("Cannot read microphone samples."); return; }
                readPosition = (readPosition + frameSamples) % recording.samples;
                available -= frameSamples;
                activity.Process(frame, (float)frameSamples / recording.frequency, noiseGateDb, fullLevelDb, recording.channels);
                if (PcmFrameCaptured != null)
                    foreach (Action<float[], int, int> consumer in PcmFrameCaptured.GetInvocationList())
                        try { consumer(frame, recording.frequency, recording.channels); }
                        catch (Exception exception) { Debug.LogException(exception); }
            }
            State = activity.IsSpeaking ? CaptureState.Speaking : CaptureState.Listening;
            if (activity.IsSpeaking && activity.Level > 0.025f && Time.time >= nextPulse)
            {
                nextPulse = Time.time + Mathf.Max(0.2f, pulseInterval);
                float radius = Mathf.Lerp(quietRadius, Mathf.Max(quietRadius, loudRadius), activity.Level);
                Vector3 origin = mouth != null ? mouth.position : transform.position + Vector3.up * 1.6f;
                SoundBus.Emit(origin, radius, Mathf.Lerp(0.45f, 1.35f, activity.Level), SoundType.Voice, gameObject);
            }
        }

        private void Fail(string message, Exception exception = null)
        {
            StopCapture();
            Error = message;
            State = CaptureState.Unavailable;
            Debug.LogWarning("Push to talk: " + message + (exception == null ? "" : " " + exception.Message), this);
        }

        public void StopCapture()
        {
            if (recording != null)
            {
                try { Microphone.End(activeDevice); } catch (Exception) { /* Device may have been removed. */ }
                Destroy(recording);
            }
            recording = null;
            frame = null;
            activity.Reset();
            if (State != CaptureState.Unavailable) State = CaptureState.Idle;
        }

        private void OnDisable() { StopCapture(); wasHeld = false; }
        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) { StopCapture(); wasHeld = false; }
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) { StopCapture(); wasHeld = false; }
        }

        private void OnGUI()
        {
            if (!showIndicator || !Application.isPlaying) return;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            string label = State == CaptureState.Unavailable ? "MIC UNAVAILABLE · " + Error :
                State == CaptureState.Starting ? "MIC · CONNECTING" :
                State == CaptureState.Speaking ? "MIC · SPEAKING" :
                State == CaptureState.Listening ? "MIC · LISTENING" : $"HOLD {talkKey} · VOICE";
            Color old = GUI.color;
            var rect = new Rect(24, Screen.height - 61, State == CaptureState.Unavailable ? Mathf.Min(680, Screen.width - 48) : 210, 37);
            GUI.color = new Color(0.015f, 0.025f, 0.035f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            Color accent = State == CaptureState.Unavailable ? new Color(1, 0.45f, 0.3f) : new Color(0.4f, 0.9f, 0.85f);
            GUI.color = State == CaptureState.Idle ? new Color(0.5f, 0.57f, 0.6f) : accent;
            GUI.Label(new Rect(rect.x + 12, rect.y + 2, rect.width - 20, 27), label, labelStyle);
            if (IsRecording)
            {
                GUI.color = accent;
                GUI.DrawTexture(new Rect(rect.x + 12, rect.y + 31, (rect.width - 24) * activity.Level, 2), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
