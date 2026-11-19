using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>
    /// Minimal client for whisper.cpp's whisper-server: a finished phrase goes in, its text comes out.
    /// One request at a time: a new request or Cancel() aborts the one in flight.
    /// </summary>
    [Serializable]
    public sealed class WhisperClient
    {
        // Values are stored in PlayerPrefs and scenes; keep them when the list changes. 1 was Russian, now unsupported.
        public enum SpeechLanguage { System = 0, Polish = 2, English = 3, Auto = 4 }
        private const int WhisperSampleRate = 16000;
        private static readonly string[] Supported = { "polish", "english" };

        // Sound descriptions rather than speech, such as [BLANK_AUDIO], (music) or *coughs*.
        private static readonly Regex SoundDescriptions = new Regex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*");

        [Tooltip("The server's address. Its model is chosen when starting whisper-server.")]
        public string url = "http://127.0.0.1:8178/inference";
        [Tooltip("Fixed language avoids language detection on each short order. Auto accepts Polish and English.")]
        public SpeechLanguage language = SpeechLanguage.System;
        [Tooltip("Legacy setting. A single Polish or English entry is preserved when Language is System; otherwise the system language decides.")]
        public string[] languages = { "polish", "english" };
        [Min(5)] public int timeoutSeconds = 30;

        private UnityWebRequest pending;
        public double LastTranscriptionSeconds { get; private set; }
        /// <summary>The language of the last transcript: the fixed language, or what Auto heard.</summary>
        public string LastLanguage { get; private set; }

        public string RecognitionLanguage => ResolveLanguage(language, Application.systemLanguage, languages);

        public static string ResolveLanguage(SpeechLanguage selected, SystemLanguage system, string[] legacy)
        {
            switch (selected)
            {
                case SpeechLanguage.Polish: return "polish";
                case SpeechLanguage.English: return "english";
                case SpeechLanguage.Auto: return "auto";
            }
            if (legacy != null && legacy.Length == 1 && Array.IndexOf(Supported, legacy[0]) >= 0) return legacy[0];
            return SystemDefault(system);
        }

        /// <summary>Polish on a Polish system, English everywhere else.</summary>
        public static string SystemDefault(SystemLanguage system) => system == SystemLanguage.Polish ? "polish" : "english";

        /// <summary>Whether Auto's detected language is one the companion understands.</summary>
        public static bool IsSupported(string whisperLanguage) => Array.IndexOf(Supported, whisperLanguage) >= 0;

        public IEnumerator Transcribe(float[] mono, int sampleRate, Action<string> onText, Action<string> onError)
        {
            Cancel();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            float[] samples = PadForRecognition(Resample(mono, sampleRate, WhisperSampleRate));
            byte[] wav = EncodeWav(samples, WhisperSampleRate);

            JObject reply = null;
            string error = null;
            Action<JObject> onReply = result => reply = result;
            Action<string> onProblem = problem => error = problem;

            string selected = RecognitionLanguage;
            bool auto = selected == "auto";
            int audioContext = AudioContext(samples.Length, WhisperSampleRate);
            int requests = 1;
            // Only Auto needs the detected language, which verbose_json reports.
            yield return Send(wav, selected, audioContext, auto, onReply, onProblem);
            string heard = auto ? (string)reply?["language"] : selected;
            // Whisper sometimes hears a short Polish phrase as another language. Read it again in the
            // language last heard, so a Cyrillic or Czech transcript never reaches the companion.
            if (auto && error == null && reply != null && !string.IsNullOrWhiteSpace((string)reply["text"]) && !IsSupported(heard))
            {
                heard = IsSupported(LastLanguage) ? LastLanguage : SystemDefault(Application.systemLanguage);
                reply = null;
                requests = 2;
                yield return Send(wav, heard, audioContext, false, onReply, onProblem);
            }
            if (error == null && reply != null && IsSupported(heard)) LastLanguage = heard;
            LastTranscriptionSeconds = timer.Elapsed.TotalSeconds;
            Debug.Log($"[Voice timing] recognition={LastTranscriptionSeconds:0.000}s audio={(double)mono.Length / sampleRate:0.00}s language={selected} heard={heard} requests={requests}");

            if (error != null)
            {
                onError(error);
                yield break;
            }
            // The request was cancelled by a newer one.
            if (reply == null) yield break;

            // The server reports some problems, such as an unreadable file, as JSON without text.
            string text = (string)reply["text"];
            if (text == null) onError("Whisper returned no text: " + reply.ToString(Formatting.None));
            else onText(text);
        }

        // Whisper normally encodes a padded 30-second window even for a one-second PTT phrase.
        // Retain at least ten seconds of context and two seconds of margin for longer phrases.
        public static int AudioContext(int sampleCount, int sampleRate)
        {
            double seconds = (double)sampleCount / sampleRate;
            return Math.Min(1500, Math.Max(512, (int)Math.Ceiling((seconds + 2) * 50 / 128) * 128));
        }

        // Supply boundary context without waiting or recording extra time. VAD removes this
        // padding; it also keeps short first/last words away from the model's window boundary.
        public static float[] PadForRecognition(float[] mono)
        {
            var padded = new float[mono.Length + 2 * WhisperSampleRate];
            Array.Copy(mono, 0, padded, WhisperSampleRate, mono.Length);
            return padded;
        }

        private IEnumerator Send(byte[] wav, string language, int audioContext, bool reportLanguage, Action<JObject> onReply, Action<string> onError)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("language", language),
                new MultipartFormDataSection("response_format", reportLanguage ? "verbose_json" : "json"),
                new MultipartFormDataSection("temperature", "0"),
                new MultipartFormDataSection("temperature_inc", "0"),
                new MultipartFormDataSection("best_of", "1"),
                new MultipartFormDataSection("beam_size", "1"),
                new MultipartFormDataSection("no_timestamps", "true"),
                new MultipartFormDataSection("token_timestamps", "false"),
                new MultipartFormDataSection("audio_ctx", audioContext.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                // Recognition vocabulary, not an instruction to convert uncertain audio into a command.
                new MultipartFormDataSection("prompt", LanguageHint(language)),
                new MultipartFormFileSection("file", wav, "phrase.wav", "audio/wav")
            };
            UnityWebRequest web = UnityWebRequest.Post(url, form);
            web.timeout = timeoutSeconds;
            pending = web;

            yield return web.SendWebRequest();
            // Cancel() already aborted and disposed this request.
            if (pending != web) yield break;
            pending = null;

            bool success = web.result == UnityWebRequest.Result.Success;
            string body = web.downloadHandler.text;
            string error = success ? null : Describe(web);
            web.Dispose();

            if (!success)
            {
                onError(error);
                yield break;
            }

            JObject reply;
            try
            {
                reply = JObject.Parse(body);
            }
            catch (JsonException exception)
            {
                onError("Whisper returned invalid JSON: " + exception.Message);
                yield break;
            }
            onReply(reply);
        }

        private static string LanguageHint(string language)
        {
            switch (language)
            {
                case "english": return "A conversation. Stop. Come here. Follow me. Open the door. Close the door.";
                case "polish": return "Rozmowa po polsku. Stój. Chodź do mnie. Idź za mną. Otwórz drzwi. Zamknij drzwi.";
                default: return "";
            }
        }

        public void Cancel()
        {
            if (pending == null) return;
            pending.Abort();
            pending.Dispose();
            pending = null;
        }

        /// <summary>Removes what is not the curator's speech. An empty result means nothing was said.</summary>
        public static string CleanTranscript(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string clean = SoundDescriptions.Replace(text, " ").Trim();
            foreach (char character in clean)
            {
                if (char.IsLetterOrDigit(character)) return clean;
            }
            return "";
        }

        /// <summary>Averages the span of input behind each output sample, weighting partly covered samples.</summary>
        public static float[] Resample(float[] samples, int fromRate, int toRate)
        {
            if (fromRate == toRate) return samples;
            double step = (double)fromRate / toRate;
            var result = new float[(int)(samples.Length / step)];
            for (int i = 0; i < result.Length; i++)
            {
                double start = i * step;
                double end = start + step;
                double sum = 0;
                for (int j = (int)start; j < end && j < samples.Length; j++)
                {
                    sum += samples[j] * (Math.Min(end, j + 1) - Math.Max(start, j));
                }
                result[i] = (float)(sum / step);
            }
            return result;
        }

        /// <summary>A mono, 16-bit PCM WAV file.</summary>
        public static byte[] EncodeWav(float[] samples, int sampleRate)
        {
            const int headerSize = 44;
            const int bytesPerSample = 2;
            int dataSize = samples.Length * bytesPerSample;
            using (var stream = new MemoryStream(headerSize + dataSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(headerSize - 8 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); // Format block size.
                writer.Write((short)1); // PCM.
                writer.Write((short)1); // Mono.
                writer.Write(sampleRate);
                writer.Write(sampleRate * bytesPerSample);
                writer.Write((short)bytesPerSample);
                writer.Write((short)(bytesPerSample * 8));
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                foreach (float sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1, 1) * short.MaxValue));
                return stream.ToArray();
            }
        }

        private string Describe(UnityWebRequest web)
        {
            if (web.responseCode == 0)
            {
                return "Voice recognition is unavailable. Press Enter to type an order.";
            }
            return $"Whisper error {web.responseCode}: {web.downloadHandler.text}";
        }
    }
}
