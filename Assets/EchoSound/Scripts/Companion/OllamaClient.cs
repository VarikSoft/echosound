using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>
    /// Minimal client for Ollama's /api/chat endpoint with tool calling.
    /// One request at a time: a new request or Cancel() aborts the one in flight.
    /// </summary>
    [Serializable]
    public sealed class OllamaClient
    {
        public string url = "http://localhost:11434/api/chat";
        [Tooltip("Must support tools. List installed models with: ollama list")]
        public string model = "qwen3.5:9b";
        [Range(0, 1)] public float temperature = 0.2f;
        [Tooltip("Sends think=false. Untick for models without a thinking mode if Ollama rejects the field.")]
        public bool disableThinking = true;
        [Min(5)] public int timeoutSeconds = 60;

        private UnityWebRequest pending;

        public JObject CreateRequest(JArray messages, JArray tools)
        {
            var request = new JObject
            {
                ["model"] = model,
                ["stream"] = false,
                // Keep the model loaded between orders; the first load takes several seconds.
                ["keep_alive"] = -1,
                ["options"] = new JObject { ["temperature"] = temperature },
                ["messages"] = messages
            };
            if (disableThinking) request["think"] = false;
            if (tools != null && tools.Count > 0) request["tools"] = tools;
            return request;
        }

        /// <summary>Loads the model into memory, so the first order is answered faster.</summary>
        public IEnumerator Warmup(Action<string> onError)
        {
            return Send(CreateRequest(new JArray(), null), reply => { }, onError);
        }

        public IEnumerator Send(JObject request, Action<JObject> onReply, Action<string> onError)
        {
            Cancel();
            var web = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            web.SetRequestHeader("Content-Type", "application/json");
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
                onError("Ollama returned invalid JSON: " + exception.Message);
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

        private string Describe(UnityWebRequest web)
        {
            if (web.responseCode == 0)
            {
                return $"Ollama is not reachable at {url} ({web.error}). Start it with 'ollama serve'.";
            }

            // Ollama explains failures in an "error" field, for example a missing model.
            string details = web.downloadHandler.text;
            try
            {
                details = (string)JObject.Parse(details)["error"] ?? details;
            }
            catch (JsonException)
            {
            }
            return $"Ollama error {web.responseCode}: {details}";
        }
    }
}
