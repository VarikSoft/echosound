using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>Reply-only conversation. Streamed words never enter the gameplay planner.</summary>
    public sealed class CompanionConversationClient
    {
        private UnityWebRequest pending;

        public static JObject CreateRequest(string order, JArray recent, string facts, bool detailed = false, string context = null)
        {
            string language = CompanionLanguage.Name(CompanionLanguage.Detect(order));
            string prompt = "You are Echo, a friendly teammate in a co-op horror game. "
                + $"Reply in {language}, " + (detailed ? "in one or two short sentences. " : "in one short sentence, about 5-8 words. ")
                + "Answer the latest message specifically; use earlier turns for follow-ups. "
                + "Do not repeat your last reply. Don't invent sightings, completed actions or player facts. "
                + "Output only your spoken words, without labels, JSON or emoji.";
            if (!string.IsNullOrEmpty(facts)) prompt += "\nConfirmed player facts: " + facts;
            if (!string.IsNullOrEmpty(context)) prompt += "\nGame state: " + context;
            var messages = new JArray(new JObject { ["role"] = "system", ["content"] = prompt });
            if (recent != null) foreach (JToken message in recent) messages.Add(message.DeepClone());
            messages.Add(new JObject { ["role"] = "user", ["content"] = order });
            return new JObject
            {
                ["messages"] = messages, ["stream"] = true, ["cache_prompt"] = true,
                ["temperature"] = 0.7, ["top_p"] = 0.8, ["top_k"] = 20, ["min_p"] = 0,
                ["presence_penalty"] = 1.5, ["repeat_penalty"] = 1.0,
                ["max_tokens"] = detailed ? 80 : 48,
                ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false }
            };
        }

        public static string ReadReply(string text)
        {
            text = text.Trim();
            if (text.Length == 0 || text.Length > 600 || text.IndexOfAny(new[] { '<', '>', '{', '}', '[', ']' }) >= 0)
                throw new JsonException("Invalid conversational text.");
            return text;
        }

        public IEnumerator Send(JObject request, Action<string> onReply, Action<string> onError, Action<string> onProgress = null, bool detailed = false)
        {
            Cancel();
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            if (!runtime.ChatReady) { onError("The language model is not ready."); yield break; }
            request["stream"] = true;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double firstText = -1;
            string previous = "";
            var stream = new ReplyStream(detailed ? 2 : 1);
            var web = new UnityWebRequest(runtime.ChatUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.ToString(Formatting.None))),
                downloadHandler = stream, timeout = 5
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SetRequestHeader("Authorization", "Bearer " + runtime.ApiKey);
            pending = web;
            var operation = web.SendWebRequest();
            double budget = detailed ? 3.5 : 1.9;
            while (true)
            {
                if (pending != web) yield break;
                string text = stream.Text;
                if (text != previous && !string.IsNullOrWhiteSpace(text))
                {
                    if (firstText < 0) firstText = timer.Elapsed.TotalSeconds;
                    previous = text;
                    onProgress?.Invoke(text);
                }
                if (operation.isDone || stream.Complete || stream.Error != null || timer.Elapsed.TotalSeconds >= budget) break;
                yield return null;
            }
            bool validEnd = stream.Complete || (operation.isDone && stream.Stopped);
            string answer = validEnd ? stream.Text : stream.CompletedSentences;
            bool success = stream.Error == null && !string.IsNullOrWhiteSpace(answer)
                && (validEnd || stream.CompletedSentences.Length > 0);
            pending = null;
            if (!operation.isDone) web.Abort();
            web.Dispose();
            string reply = null;
            if (success)
            {
                try { reply = ReadReply(answer); }
                catch (JsonException) { success = false; }
            }
            Debug.Log($"[Dialogue timing] first-text={firstText:0.000}s complete={timer.Elapsed.TotalSeconds:0.000}s success={success}");
            if (success) onReply(reply);
            else onError("Conversation did not produce a complete reply.");
        }

        public void Cancel()
        {
            if (pending == null) return;
            UnityWebRequest web = pending;
            pending = null;
            web.Abort(); web.Dispose();
        }

        /// <summary>Incremental SSE and UTF-8 decoder. Stops after actual sentence boundaries, never mid-word.</summary>
        public sealed class ReplyStream : DownloadHandlerScript
        {
            private readonly Decoder decoder = new UTF8Encoding(false, true).GetDecoder();
            private readonly char[] characters = new char[8192];
            private readonly StringBuilder line = new StringBuilder();
            private readonly StringBuilder text = new StringBuilder();
            private readonly int sentenceLimit;
            private int sentences, lastSentence, received;
            private bool punctuation;
            public string Text => text.ToString().Trim();
            public string CompletedSentences => text.ToString(0, lastSentence).Trim();
            public bool Complete { get; private set; }
            public bool Stopped { get; private set; }
            public string Error { get; private set; }

            public ReplyStream(int sentenceLimit) : base(new byte[8192]) { this.sentenceLimit = sentenceLimit; }
            protected override bool ReceiveData(byte[] data, int length) { Feed(data, length); return Error == null; }

            public void Feed(byte[] data, int length)
            {
                if (Complete || Error != null || data == null || length == 0) return;
                received += length;
                if (received > 65536) { Error = "Conversation stream too large."; return; }
                int count;
                try { count = decoder.GetChars(data, 0, length, characters, 0, false); }
                catch (DecoderFallbackException) { Error = "Invalid conversation encoding."; return; }
                for (int i = 0; i < count && !Complete && Error == null; i++)
                {
                    char value = characters[i];
                    if (value == '\n') { ReadLine(line.ToString().TrimEnd('\r')); line.Clear(); }
                    else line.Append(value);
                }
            }

            private void ReadLine(string value)
            {
                if (!value.StartsWith("data:", StringComparison.Ordinal)) return;
                value = value.Substring(5).TrimStart();
                if (value == "[DONE]") return;
                try
                {
                    JObject chunk = JObject.Parse(value);
                    if (chunk["error"] != null) { Error = "Conversation server error."; return; }
                    JToken choice = chunk["choices"] is JArray choices && choices.Count > 0 ? choices[0] : null;
                    if (choice == null) return;
                    Stopped |= (string)choice["finish_reason"] == "stop";
                    string addition = (string)choice["delta"]?["content"];
                    if (addition == null) return;
                    foreach (char letter in addition)
                    {
                        // Allow closing quotes and ?! to arrive before completing the sentence.
                        if (sentences >= sentenceLimit && letter is not '.' and not '!' and not '?' and not '"' and not '»' and not '”')
                        { Complete = true; break; }
                        text.Append(letter);
                        if (letter is '.' or '!' or '?')
                        {
                            if (!punctuation && text.Length >= 4) sentences++;
                            punctuation = true;
                            lastSentence = text.Length;
                        }
                        else if (!char.IsWhiteSpace(letter) && letter is not '"' and not '»' and not '”') punctuation = false;
                        else if (punctuation) lastSentence = text.Length;
                        if (text.Length > 600) { Error = "Conversation reply too long."; break; }
                    }
                }
                catch (JsonException) { Error = "Malformed conversation stream."; }
            }
        }
    }
}
