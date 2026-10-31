using System;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Editor.Tests
{
    public static class VoiceChecks
    {
        [MenuItem("EchoSound/Checks/Validate voice activity")]
        public static void Validate()
        {
            var activity = new VoiceActivity();
            var pcm = new float[480];
            activity.Process(pcm, 0.02f, -48, -18);
            Require(!activity.IsSpeaking && activity.Level == 0, "Silence must not open the gate.");
            for (int i = 0; i < pcm.Length; i++) pcm[i] = 0.3f;
            activity.Process(pcm, 0.02f, -48, -18);
            Require(!activity.IsSpeaking, "DC input must not be mistaken for voice.");
            for (int i = 0; i < pcm.Length; i++) pcm[i] = i % 2 == 0 ? 0.3f : -0.3f;
            activity.Process(pcm, 0.02f, -48, -18, 2);
            Require(!activity.IsSpeaking, "Opposite channel DC offsets must not open the voice gate.");
            Fill(pcm, 0.001f);
            for (int i = 0; i < 10; i++) activity.Process(pcm, 0.02f, -48, -18);
            Require(!activity.IsSpeaking, "Noise below the gate must remain silent.");
            Fill(pcm, 0.02f);
            for (int i = 0; i < 10; i++) activity.Process(pcm, 0.02f, -48, -18);
            float quiet = activity.Level;
            Require(activity.IsSpeaking && quiet > 0, "Quiet speech must open the gate.");
            Fill(pcm, 0.2f);
            for (int i = 0; i < 10; i++) activity.Process(pcm, 0.02f, -48, -18);
            Require(activity.Level > quiet + 0.25f, "Louder speech must produce a stronger level.");
            Array.Clear(pcm, 0, pcm.Length);
            activity.Process(pcm, 0.02f, -48, -18);
            Require(activity.IsSpeaking, "Short pauses must not chatter the gate.");
            for (int i = 0; i < 50; i++) activity.Process(pcm, 0.02f, -48, -18);
            Require(!activity.IsSpeaking && activity.Level < 0.001f, "Silence must close the gate and release the meter.");
            Fill(pcm, 0.2f);
            activity.Process(pcm, 0.02f, -48, -18);
            activity.Reset();
            Require(!activity.IsSpeaking && activity.Level == 0, "PTT release must clear the detector immediately.");
            var go = new GameObject("Voice lifecycle check");
            try
            {
                var voice = go.AddComponent<PushToTalkVoice>();
                Require(!voice.IsRecording && voice.State == PushToTalkVoice.CaptureState.Idle, "Adding the component must not open the microphone.");
                voice.StopCapture();
                voice.enabled = false;
                Require(!voice.IsRecording, "Stopping an idle recorder must be safe.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("Voice checks: all tests passed (synthetic PCM; microphone was not opened).");
        }

        private static void Fill(float[] pcm, float amplitude)
        {
            for (int i = 0; i < pcm.Length; i++) pcm[i] = amplitude * Mathf.Sin(2 * Mathf.PI * 200 * i / 24000f);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Voice check: " + message);
        }
    }
}
