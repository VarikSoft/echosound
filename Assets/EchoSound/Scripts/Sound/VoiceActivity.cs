using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>DC-resistant RMS metering, a hysteretic noise gate and a release envelope.</summary>
    public sealed class VoiceActivity
    {
        public float Decibels { get; private set; } = -100f;
        public float Level { get; private set; }
        public bool IsSpeaking { get; private set; }
        private float holdRemaining;

        public void Process(float[] pcm, float duration, float gateDb, float fullLevelDb, int channels = 1)
        {
            if (pcm == null || pcm.Length == 0) { Reset(); return; }
            channels = Mathf.Clamp(channels, 1, pcm.Length);
            double variance = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                double sum = 0, squares = 0;
                int count = 0;
                for (int i = channel; i < pcm.Length; i += channels)
                {
                    float sample = float.IsNaN(pcm[i]) || float.IsInfinity(pcm[i]) ? 0f : pcm[i];
                    sum += sample;
                    squares += sample * sample;
                    count++;
                }
                double mean = sum / count;
                variance += System.Math.Max(0, squares / count - mean * mean);
            }
            float rms = Mathf.Sqrt((float)(variance / channels));
            Decibels = 20f * Mathf.Log10(Mathf.Max(0.00001f, rms));
            float threshold = IsSpeaking ? gateDb - 5f : gateDb;
            if (Decibels >= threshold) holdRemaining = 0.12f;
            else holdRemaining = Mathf.Max(0, holdRemaining - Mathf.Max(0, duration));
            IsSpeaking = holdRemaining > 0;
            float target = IsSpeaking ? Mathf.InverseLerp(gateDb, Mathf.Max(gateDb + 6f, fullLevelDb), Decibels) : 0f;
            float rate = target > Level ? 35f : 9f;
            Level = Mathf.Lerp(Level, target, 1f - Mathf.Exp(-rate * Mathf.Max(0, duration)));
        }

        public void Reset()
        {
            Decibels = -100f;
            Level = 0f;
            IsSpeaking = false;
            holdRemaining = 0;
        }
    }
}
