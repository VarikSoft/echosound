using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>OpenAI-compatible chat over loopback to the server owned by the game.</summary>
    [Serializable]
    public sealed class LocalChatClient
    {
        [Range(0, 1)] public float temperature = 0;
        [Min(5)] public int timeoutSeconds = 60;
        private UnityWebRequest pending;

        public JObject CreateRequest(JArray messages, JArray tools)
        {
            // Small models are more reliable with a constrained plan than a native tool-call template.
            // Keep the brain's tool protocol internal and present compact JSON plans to the model.
            var wireMessages = new JArray();
            string replyLanguage = "the same language as the player's latest order";
            foreach (JObject message in messages)
                if ((string)message["role"] == "user")
                    replyLanguage = CompanionLanguage.Name(CompanionLanguage.Detect((string)message["content"] ?? ""));
            foreach (JObject message in messages)
            {
                string role = (string)message["role"];
                string content = (string)message["content"] ?? "";
                if (role == "tool") { role = "user"; content = "Game result for " + (string)message["name"] + ": " + content; }
                if (message["tool_calls"] is JArray calls)
                {
                    var actions = new JArray();
                    foreach (JToken call in calls)
                        actions.Add(new JObject { ["name"] = call["function"]["name"], ["arguments"] = JObject.Parse((string)call["function"]["arguments"]) });
                    content = new JObject { ["reply"] = content, ["actions"] = actions }.ToString(Formatting.None);
                }
                if (role == "system") content += "\nReturn a JSON plan with reply and actions. For an order, actions must contain the function(s); text alone does nothing. " +
                    "For a question or chat, actions is empty. For completed actions, report the game results without repeating them. " +
                    "Come here / podejdź do mnie means go_to curator, not following. " +
                    "Direction glossary relative to the curator: przede mną / z przodu = in front; za mną / z tyłu = behind; " +
                    "po lewej = on the left; po prawej = on the right. " +
                    "The reply field must be in " + replyLanguage + ". Available actions: " + (tools?.ToString(Formatting.None) ?? "none");
                wireMessages.Add(new JObject { ["role"] = role, ["content"] = content });
            }
            var choices = new JArray();
            if (tools != null)
                foreach (JToken tool in tools)
                {
                    JObject parameters = (JObject)tool["function"]["parameters"].DeepClone();
                    parameters["additionalProperties"] = false;
                    choices.Add(new JObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false,
                        ["properties"] = new JObject
                        {
                            ["name"] = new JObject { ["type"] = "string", ["enum"] = new JArray((string)tool["function"]["name"]) },
                            ["arguments"] = parameters
                        },
                        ["required"] = new JArray("name", "arguments")
                    });
                }
            var schema = new JObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["actions"] = new JObject
                    {
                        ["type"] = "array", ["maxItems"] = choices.Count == 0 ? 0 : 4,
                        ["items"] = choices.Count == 0 ? new JObject { ["type"] = "object" } : new JObject { ["anyOf"] = choices }
                    },
                    ["reply"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray("actions", "reply")
            };
            var request = new JObject
            {
                ["model"] = "local-companion", ["stream"] = false,
                ["temperature"] = temperature, ["max_tokens"] = 192,
                ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false },
                ["messages"] = wireMessages,
                ["response_format"] = new JObject { ["type"] = "json_schema", ["json_schema"] = new JObject { ["name"] = "companion_plan", ["strict"] = true, ["schema"] = schema } }
            };
            return request;
        }

        public static JObject ReadPlan(string content)
        {
            var plan = JObject.Parse(content);
            if (!(plan["actions"] is JArray actions) || actions.Count > 4 || plan["reply"]?.Type != JTokenType.String)
                throw new JsonException("Invalid companion plan.");
            var message = new JObject { ["role"] = "assistant", ["content"] = plan["reply"] };
            var calls = new JArray();
            foreach (JToken action in actions)
            {
                if (action["name"]?.Type != JTokenType.String || !(action["arguments"] is JObject arguments))
                    throw new JsonException("Invalid action.");
                calls.Add(new JObject
                {
                    ["id"] = Guid.NewGuid().ToString("N"), ["type"] = "function",
                    ["function"] = new JObject { ["name"] = action["name"], ["arguments"] = arguments.ToString(Formatting.None) }
                });
            }
            if (calls.Count > 0) message["tool_calls"] = calls;
            return message;
        }

        public IEnumerator Send(JObject request, Action<JObject> onReply, Action<string> onError)
        {
            Cancel();
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            runtime.EnsureChatStarted();
            while (runtime.ChatStarting) yield return null;
            if (!runtime.ChatReady) { onError(runtime.ChatError); yield break; }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var web = new UnityWebRequest(runtime.ChatUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(), timeout = timeoutSeconds
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SetRequestHeader("Authorization", "Bearer " + runtime.ApiKey);
            pending = web;
            yield return web.SendWebRequest();
            if (pending != web) yield break;
            pending = null;
            bool success = web.result == UnityWebRequest.Result.Success;
            string body = web.downloadHandler.text;
            web.Dispose();
            if (!success)
            {
                Debug.LogWarning("[Local AI] Chat request failed: " + body);
                onError("I couldn't process that. Try a shorter order, or use follow, stop or come here.");
                yield break;
            }
            JObject reply;
            try
            {
                var response = JObject.Parse(body);
                Debug.Log($"[Voice timing] model={timer.Elapsed.TotalSeconds:0.000}s prompt_tokens={response["usage"]?["prompt_tokens"]} reply_tokens={response["usage"]?["completion_tokens"]}");
                if ((string)response["choices"]?[0]?["finish_reason"] == "length")
                    throw new JsonException("The response exceeded its token budget.");
                string content = (string)response["choices"]?[0]?["message"]?["content"];
                if (content == null) throw new JsonException("No chat message.");
                reply = ReadPlan(content);
            }
            catch (JsonException exception)
            {
                Debug.LogWarning("[Local AI] " + exception.Message);
                onError("I didn't understand. Please give one short instruction.");
                yield break;
            }
            onReply(new JObject { ["message"] = reply });
        }

        public void Cancel()
        {
            if (pending == null) return;
            pending.Abort(); pending.Dispose(); pending = null;
        }
    }
}
