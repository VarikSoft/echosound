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
        private const int WhisperSampleRate = 16000;

        // Sound descriptions rather than speech, such as [BLANK_AUDIO], (music) or *coughs*.
        private static readonly Regex SoundDescriptions = new Regex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*");

        [Tooltip("The server's address. Its model is chosen when starting whisper-server.")]
        public string url = "http://127.0.0.1:8178/inference";
        [Tooltip("Languages the curator speaks, as lowercase English names, main language first. " +
            "Short phrases get mistaken for a similar language, so anything else is read again in the main language.")]
        public string[] languages = { "polish", "english" };
        [Min(5)] public int timeoutSeconds = 30;

        private UnityWebRequest pending;

        public IEnumerator Transcribe(float[] mono, int sampleRate, Action<string> onText, Action<string> onError)
        {
            Cancel();
            byte[] wav = EncodeWav(Resample(mono, sampleRate, WhisperSampleRate), WhisperSampleRate);

            JObject reply = null;
            string error = null;
            Action<JObject> onReply = result => reply = result;
            Action<string> onProblem = problem => error = problem;

            yield return Send(wav, languages.Length == 1 ? languages[0] : "auto", onReply, onProblem);
            if (reply != null && languages.Length > 1 && !string.IsNullOrWhiteSpace((string)reply["text"])
                && Array.IndexOf(languages, (string)reply["language"]) < 0)
            {
                reply = null;
                yield return Send(wav, languages[0], onReply, onProblem);
            }

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

        private IEnumerator Send(byte[] wav, string language, Action<JObject> onReply, Action<string> onError)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("language", language),
                // Unlike plain json, it says which language Whisper heard.
                new MultipartFormDataSection("response_format", "verbose_json"),
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
                return $"Whisper is not reachable at {url} ({web.error}). " +
                    "Start whisper-server with the command from the README.";
            }
            return $"Whisper error {web.responseCode}: {web.downloadHandler.text}";
        }
    }
}
