using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Companion
{
    /// <summary>
    /// Listens to the curator and turns orders into function calls through a local LLM (Ollama).
    /// Flow: order, LLM picks functions, functions run (walking first), results go back to the LLM,
    /// repeat until it answers in words. A new order from the curator interrupts the current one.
    /// </summary>
    [RequireComponent(typeof(CompanionAgent), typeof(CompanionPerception), typeof(Interactor))]
    public sealed class CompanionBrain : MonoBehaviour
    {
        public const string DefaultPrompt =
@"You are the AI companion, a game set in a pitch-dark facility where only sound reveals the surroundings.
The player is your curator. You serve the curator: you act only on the curator's orders, and only through the provided functions.

How to act:
- An order (open, close, go, come, follow, stop, wait) means: call the matching function right away. Do not ask for confirmation.
- If an order has several steps, call the functions one after another until every step is done. Never claim you are doing something without calling its function.
- Functions that use an object walk to it by themselves, so do not call go_to before them.
- Your perception lists every object you know about, with its position relative to the curator. The curator's words ""in front of me"", ""behind me"", ""on my left"", ""on my right"" and ""next to me"" refer to those positions.
- If an order fits several objects equally well, ask which one is meant. Otherwise act.
- Questions and small talk: answer in words, without functions.
- Use only ids from your perception. Never invent objects or results.
- A new message from the curator cancels what you were doing.

How to speak:
- Reply in the curator's language, in one short sentence, like a teammate on the radio.
- Never say object ids such as door_1 aloud. Say ""the door in front of you"", ""the door behind you"" and so on.
- After functions finish, report the outcome.";

        public OllamaClient ollama = new OllamaClient();
        [TextArea(6, 30)] public string systemPrompt = DefaultPrompt;
        [Tooltip("Function rounds per order; after the last one the LLM must answer in words.")]
        [Range(1, 8)] public int maxToolRounds = 4;
        [Tooltip("Messages kept from earlier orders, so the companion remembers the conversation.")]
        [Range(4, 60)] public int historyLimit = 24;
        public bool logToConsole = true;

        /// <summary>The companion's words: shown in the chat now, spoken by TTS later.</summary>
        public event Action<string> Spoke;
        /// <summary>Short progress text, for example "thinking"; empty when idle.</summary>
        public event Action<string> StatusChanged;
        /// <summary>Connection or model problems the player should see.</summary>
        public event Action<string> Failed;

        public CompanionAgent Body { get; private set; }
        public CompanionPerception Perception { get; private set; }
        /// <summary>Uses objects through the same rules as the player's hands.</summary>
        public Interactor Hands { get; private set; }
        public bool IsBusy { get; private set; }

        private readonly List<JObject> history = new List<JObject>();
        private List<CompanionTool> fixedTools;
        // The functions offered in the latest request; the LLM's calls are looked up here.
        private readonly List<CompanionTool> offeredTools = new List<CompanionTool>();
        private string runningTool;

        private void Awake()
        {
            Body = GetComponent<CompanionAgent>();
            Perception = GetComponent<CompanionPerception>();
            Hands = GetComponent<Interactor>();
            fixedTools = CreateFixedTools();
        }

        private void Start()
        {
            StartCoroutine(ollama.Warmup(ReportFailure));
        }

        // Abilities that do not depend on objects. Object functions such as open_door come from UseObjectTool.
        private static List<CompanionTool> CreateFixedTools()
        {
            return new List<CompanionTool>
            {
                new FollowCuratorTool(),
                new WaitHereTool(),
                new GoToTool()
            };
        }

        /// <summary>Entry point for the curator's words: typed chat today, speech-to-text later.</summary>
        public void Command(string order)
        {
            order = order?.Trim();
            if (string.IsNullOrEmpty(order)) return;

            // The curator's latest word wins over whatever the companion was doing.
            Interrupt();
            history.Add(new JObject { ["role"] = "user", ["content"] = order });
            TrimHistory();
            IsBusy = true;
            StartCoroutine(Handle(order));
        }

        /// <summary>Stops the current order. Following the curator continues; a walk in progress stops.</summary>
        public void Interrupt()
        {
            // When idle, the only coroutine may be the warm-up, which is worth keeping.
            if (!IsBusy) return;
            StopAllCoroutines();
            ollama.Cancel();

            if (runningTool != null)
            {
                history.Add(ToolMessage(runningTool, "Interrupted by a new message from the curator."));
                runningTool = null;
            }
            if (Body.IsWalking) Body.Halt("waiting (interrupted on the way: " + Body.Activity + ")");
            IsBusy = false;
            SetStatus("");
        }

        /// <summary>Forgets the conversation, for example when a new level replaces every object.</summary>
        public void ResetConversation()
        {
            Interrupt();
            history.Clear();
        }

        [ContextMenu("Reset system prompt")]
        private void ResetPrompt() => systemPrompt = DefaultPrompt;

        private IEnumerator Handle(string order)
        {
            for (int round = 0; round <= maxToolRounds; round++)
            {
                bool allowTools = round < maxToolRounds;
                SetStatus("thinking");

                JObject reply = null;
                string error = null;
                yield return ollama.Send(BuildRequest(allowTools), result => reply = result, problem => error = problem);
                if (error != null)
                {
                    ReportFailure(error);
                    break;
                }
                // The request was cancelled by a newer one.
                if (reply == null) break;

                if (!(reply["message"] is JObject message))
                {
                    ReportFailure("Ollama reply has no message: " + reply.ToString(Formatting.None));
                    break;
                }
                message.Remove("thinking");
                history.Add(message);

                string text = ((string)message["content"])?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    if (logToConsole) Debug.Log("[Companion] " + text, this);
                    Spoke?.Invoke(text);
                }

                var calls = message["tool_calls"] as JArray;
                if (calls == null || calls.Count == 0 || !allowTools) break;

                foreach (JToken call in calls)
                {
                    string name = (string)call["function"]?["name"] ?? "";
                    JObject arguments = ReadArguments(call["function"]?["arguments"]);
                    CompanionTool tool = offeredTools.Find(candidate => candidate.Name == name);
                    string outcome = $"Unknown function '{name}'.";

                    if (tool != null)
                    {
                        SetStatus(name + " " + arguments.ToString(Formatting.None));
                        runningTool = name;
                        outcome = "Done.";
                        yield return tool.Run(this, arguments, result => outcome = result);
                        runningTool = null;
                    }
                    if (logToConsole) Debug.Log($"[Companion] {name} {arguments.ToString(Formatting.None)} -> {outcome}", this);

                    // A 9B model tends to stop after the first step of "do X and then Y", so remind it of the order.
                    history.Add(ToolMessage(name, outcome +
                        $" The curator's order was: \"{order}\". If a step of it is not done yet, call the next function now; otherwise report."));
                }
            }

            IsBusy = false;
            SetStatus("");
        }

        private JObject BuildRequest(bool allowTools)
        {
            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    // Perception is rebuilt for every request, so distances and states are never stale.
                    ["content"] = systemPrompt + "\n\nCurrent perception:\n" + Perception.Describe()
                }
            };
            foreach (JObject message in history) messages.Add(message);

            JArray definitions = null;
            if (allowTools)
            {
                // Object functions follow perception: no door nearby means no open_door to call.
                offeredTools.Clear();
                offeredTools.AddRange(fixedTools);
                offeredTools.AddRange(UseObjectTool.CreateFor(Perception.Perceived()));

                definitions = new JArray();
                foreach (CompanionTool tool in offeredTools) definitions.Add(tool.Definition(this));
            }
            return ollama.CreateRequest(messages, definitions);
        }

        // Ollama sends arguments as an object; some models put a JSON string there instead.
        private static JObject ReadArguments(JToken token)
        {
            if (token is JObject arguments) return arguments;
            if (token != null && token.Type == JTokenType.String)
            {
                try
                {
                    return JObject.Parse((string)token);
                }
                catch (JsonException)
                {
                }
            }
            return new JObject();
        }

        private static JObject ToolMessage(string name, string content)
        {
            return new JObject { ["role"] = "tool", ["tool_name"] = name, ["content"] = content };
        }

        // Drops whole old orders, so a function result never loses the call it answers.
        private void TrimHistory()
        {
            while (history.Count > historyLimit)
            {
                int nextOrder = history.FindIndex(1, message => (string)message["role"] == "user");
                if (nextOrder < 0) break;
                history.RemoveRange(0, nextOrder);
            }
        }

        private void SetStatus(string status) => StatusChanged?.Invoke(status);

        private void ReportFailure(string error)
        {
            Debug.LogWarning("[Companion] " + error, this);
            Failed?.Invoke(error);
        }

        private void OnDisable()
        {
            Interrupt();
            ollama.Cancel();
        }
    }
}
