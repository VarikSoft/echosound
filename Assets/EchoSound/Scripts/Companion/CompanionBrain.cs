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
    /// Uses bundled local inference for conversation and exact shortcuts for common orders.
    /// Flow: order, one constrained plan, validated actions (walking first), factual outcome.
    /// A new order from the curator interrupts the current one.
    /// </summary>
    [RequireComponent(typeof(CompanionAgent), typeof(CompanionPerception), typeof(Interactor))]
    public sealed class CompanionBrain : MonoBehaviour
    {
        public const string DefaultPrompt =
@"You are a teammate in a dark exploration game. The curator is the player.
Interpret the player's latest words using only Current perception and the available actions.

Orders need actions. Never describe an action instead of selecting it.
Stop/wait/stay means wait_here. Follow me means follow_curator (continuous following).
Come here/return to me means go_to with target curator (walk once and stop).
Object actions walk to their target automatically; do not add go_to before open_door or close_door.
For multi-step orders, select the steps in order. After game results, do not repeat completed steps.
Use only listed target ids. If several objects fit equally well, ask which one, with no actions.
Directions (front, behind, left, right) are relative to the curator, not you.

Questions about the world are not orders: answer from perception, with no actions.
Example: ""Is the door behind me open?"" -> actions: [], reply describing that door's actual state.
Example: ""Open the door behind me"" -> open_door on the listed door behind the curator.
Respect negations: ""Don't open it, wait"" -> wait_here only.
For small talk, answer briefly with no actions. Never invent objects or successful outcomes.
Reply in the language of the latest player order, in one short sentence. Do not speak object ids.";

        public LocalChatClient chat = new LocalChatClient();
        [TextArea(6, 30)] public string systemPrompt = DefaultPrompt;
        [Tooltip("Messages kept from earlier orders, so the companion remembers the conversation.")]
        [Range(4, 60)] public int historyLimit = 8;
        public bool logToConsole = true;

        /// <summary>The curator's order as the companion received it.</summary>
        public event Action<string> OrderReceived;
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
        public string LastCommandRoute { get; private set; }
        public bool FreeDialogue { get; private set; }
        private const string DialoguePreference = "EchoSound.FreeDialogue";

        public void SetFreeDialogue(bool enabled, bool remember = true)
        {
            if (!enabled)
            {
                if (LastCommandRoute != null && LastCommandRoute.StartsWith("model")) Interrupt();
                LocalAiRuntime.Instance.StopChat();
            }
            FreeDialogue = enabled;
            if (remember)
            {
                PlayerPrefs.SetInt(DialoguePreference, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private readonly List<JObject> history = new List<JObject>();
        private List<CompanionTool> fixedTools;
        // The functions offered in the latest request; the LLM's calls are looked up here.
        private readonly List<CompanionTool> offeredTools = new List<CompanionTool>();
        private readonly List<JToken> pendingCalls = new List<JToken>();

        private void Awake()
        {
            Body = GetComponent<CompanionAgent>();
            Perception = GetComponent<CompanionPerception>();
            Hands = GetComponent<Interactor>();
            fixedTools = CreateFixedTools();
            FreeDialogue = PlayerPrefs.GetInt(DialoguePreference, 0) == 1;
        }

        private void Start()
        {
            _ = LocalAiRuntime.Instance;
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

        /// <summary>Entry point for the curator's words, from speech recognition or typed chat.</summary>
        public void Command(string order)
        {
            order = order?.Trim();
            if (string.IsNullOrEmpty(order)) return;
            if (order.Length > 500) order = order.Substring(0, 500);

            if (logToConsole) Debug.Log("[Companion] Order: " + order, this);
            OrderReceived?.Invoke(order);

            // The curator's latest word wins over whatever the companion was doing.
            Interrupt();
            bool split = QuickCompanionCommand.TrySplitFollowup(order, out string first, out string followup);
            if (split && !FreeDialogue && !QuickDoorCommand.TryParse(first, out _)) split = false;
            history.Add(new JObject { ["role"] = "user", ["content"] = split ? first : order });
            TrimHistory();
            IsBusy = true;
            LastCommandRoute = "direct";
            if (split)
            {
                if (!QuickDoorCommand.TryParse(first, out _)) LastCommandRoute = "model+direct";
                StartCoroutine(HandleFollowup(first, followup));
            }
            else if (QuickCompanionCommand.TryParse(order, out string action, out string reply))
                StartCoroutine(HandleQuick(action, reply));
            else if (QuickDoorCommand.TryParse(order, out var doorRequest)) StartCoroutine(HandleDoor(order, doorRequest));
            else
            {
                if (FreeDialogue)
                {
                    LastCommandRoute = "model";
                    StartCoroutine(Handle(order));
                }
                else
                {
                    LastCommandRoute = "clarify";
                    string answer = LocalReply(order,
                        "Please say: follow me, stop, come here, open or close the door. You can add left/right or in front/behind me.",
                        "Powiedz: chodź za mną, stój, chodź tutaj, otwórz lub zamknij drzwi. Możesz dodać kierunek.",
                        "Скажи: «за мной», «стой», «ко мне», «открой/закрой дверь». Можно уточнить: слева, справа, передо мной или за мной.");
                    history.Add(new JObject { ["role"] = "assistant", ["content"] = answer });
                    Spoke?.Invoke(answer);
                    IsBusy = false;
                    SetStatus("");
                }
            }
        }

        private IEnumerator HandleFollowup(string first, string followup)
        {
            if (QuickDoorCommand.TryParse(first, out var doorRequest)) yield return HandleDoor(first, doorRequest, false);
            else yield return Handle(first, false);
            history.Add(new JObject { ["role"] = "user", ["content"] = followup });
            IsBusy = true;
            QuickCompanionCommand.TryParse(followup, out string action, out string reply);
            yield return HandleQuick(action, reply);
        }

        private IEnumerator HandleDoor(string order, QuickDoorCommand.Request request, bool finishOrder = true)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Transform player = Body.curator;
            Door aimed = null;
            if (player != null)
            {
                Camera camera = player.GetComponentInChildren<Camera>();
                Vector3 origin = camera != null ? camera.transform.position : player.position + Vector3.up * 1.6f;
                Vector3 forward = camera != null ? camera.transform.forward : player.forward;
                if (Physics.Raycast(origin, forward, out RaycastHit hit, Perception.radius, ~0, QueryTriggerInteraction.Ignore))
                    aimed = hit.collider.GetComponentInParent<Door>();
            }
            Door door = QuickDoorCommand.Resolve(Perception.Perceived(), player, request, aimed, out var resolution);
            string report;
            switch (resolution)
            {
                case QuickDoorCommand.Resolution.Ambiguous:
                    report = LocalReply(order, "Which door? Look at it or say left/right.", "Które drzwi? Spójrz na nie lub powiedz lewo/prawo.", "Какую дверь? Посмотри на неё или уточни: слева или справа.");
                    break;
                case QuickDoorCommand.Resolution.Missing:
                    report = LocalReply(order, "I don't know a door in that direction.", "Nie znam drzwi w tym kierunku.", "В этом направлении нет известной мне двери.");
                    break;
                case QuickDoorCommand.Resolution.NoPlayer:
                    report = LocalReply(order, "I can't locate you to choose a door.", "Nie mogę cię znaleźć, żeby wybrać drzwi.", "Не вижу тебя и не могу определить нужную дверь.");
                    break;
                case QuickDoorCommand.Resolution.AlreadyDone:
                    report = DoorStateReply(order, door);
                    break;
                default:
                    string name = request.Action.Verb() + "_door";
                    UseObjectTool tool = UseObjectTool.CreateFor(new[] { door }).Find(candidate => candidate.Name == name);
                    SetStatus("acting");
                    Spoke?.Invoke(LocalReply(order, "Okay.", "Dobrze.", "Хорошо."));
                    string outcome = null;
                    if (logToConsole) Debug.Log($"[Door timing] dispatch={timer.Elapsed.TotalSeconds:0.000}s action={name} target={door.id} route=direct", this);
                    yield return tool.Run(this, new JObject { ["target"] = door.id }, result => outcome = result);
                    if (logToConsole) Debug.Log($"[Companion] {name} target={door.id} -> {outcome}", this);
                    // Preserve the real object result for subsequent conversation, including failures.
                    history.Add(new JObject { ["role"] = "assistant", ["content"] = outcome });
                    if (tool.Succeeded) report = LocalReply(order, "Done.", "Gotowe.", "Готово.");
                    else if (outcome != null && outcome.Contains("standing in the doorway"))
                        report = LocalReply(order, "The doorway is blocked. Move away so I can close it.", "Przejście jest zajęte. Odsuń się od drzwi.", "В проёме кто-то стоит. Отойди, чтобы я мог закрыть дверь.");
                    else if (outcome != null && outcome.Contains("reach"))
                        report = LocalReply(order, "I can't find a route to that door.", "Nie mogę dotrzeć do tych drzwi.", "Не могу добраться до этой двери.");
                    else report = LocalReply(order, "I couldn't use that door.", "Nie udało się użyć tych drzwi.", "Не удалось открыть или закрыть эту дверь.");
                    break;
            }
            if (logToConsole) Debug.Log($"[Companion] Door resolution={resolution}: {report}", this);
            history.Add(new JObject { ["role"] = "assistant", ["content"] = report });
            Spoke?.Invoke(report);
            if (finishOrder) IsBusy = false;
            SetStatus("");
        }

        private static string DoorStateReply(string order, Door door)
        {
            if (door.IsBusy) return door.IsOpen
                ? LocalReply(order, "The door is already opening.", "Drzwi już się otwierają.", "Дверь уже открывается.")
                : LocalReply(order, "The door is already closing.", "Drzwi już się zamykają.", "Дверь уже закрывается.");
            return door.IsOpen
                ? LocalReply(order, "The door is already open.", "Drzwi są już otwarte.", "Дверь уже открыта.")
                : LocalReply(order, "The door is already closed.", "Drzwi są już zamknięte.", "Дверь уже закрыта.");
        }

        private IEnumerator HandleQuick(string action, string reply)
        {
            CompanionTool tool = fixedTools.Find(candidate => candidate.Name == action);
            // A missing player must never produce a false "following" acknowledgement.
            if (action != "wait_here" && Body.curator == null)
            {
                ReportFailure("There is no player to follow.");
                IsBusy = false;
                yield break;
            }
            Spoke?.Invoke(reply);
            string outcome = null;
            yield return tool.Run(this, new JObject { ["target"] = "curator" }, result => outcome = result);
            if (action == "go_to" && outcome != "Arrived next to the curator.") Spoke?.Invoke(outcome);
            history.Add(new JObject { ["role"] = "assistant", ["content"] = outcome });
            IsBusy = false;
            SetStatus("");
        }

        /// <summary>Stops the current order. Following the curator continues; a walk in progress stops.</summary>
        public void Interrupt()
        {
            if (!IsBusy) return;
            StopAllCoroutines();
            chat.Cancel();

            // Complete every outstanding call, including calls queued after the interrupted one.
            foreach (JToken call in pendingCalls)
                history.Add(ToolMessage((string)call["id"], (string)call["function"]?["name"], "Cancelled by a new order."));
            pendingCalls.Clear();
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

        private IEnumerator Handle(string order, bool finishOrder = true)
        {
            SetStatus("thinking");
            JObject reply = null;
            string error = null;
            yield return chat.Send(BuildRequest(true), result => reply = result, problem => error = problem);
            if (error != null) ReportFailure(error);
            else if (reply?["message"] is JObject message)
            {
                history.Add(message);
                var calls = message["tool_calls"] as JArray;
                if (calls == null || calls.Count == 0)
                {
                    string text = ((string)message["content"])?.Trim();
                    if (!string.IsNullOrEmpty(text)) Spoke?.Invoke(text);
                }
                else
                {
                    pendingCalls.AddRange(calls);
                    bool succeeded = true;
                    // Execute exactly one bounded plan. Never ask the model to invent another
                    // action after an outcome; that caused extra actions and delayed replies.
                    Spoke?.Invoke(LocalReply(order, "Okay.", "Dobrze.", "Хорошо."));
                    foreach (JToken call in calls)
                    {
                        string name = (string)call["function"]?["name"] ?? "";
                        JObject arguments = ReadArguments(call["function"]?["arguments"]);
                        CompanionTool tool = offeredTools.Find(candidate => candidate.Name == name);
                        string outcome = "Invalid action or target.";
                        bool done = false;
                        if (tool != null && ValidArguments(tool.Parameters(this), arguments))
                        {
                            SetStatus("acting");
                            yield return tool.Run(this, arguments, result => outcome = result);
                            done = tool.Succeeded;
                        }
                        succeeded &= done;
                        if (logToConsole) Debug.Log($"[Companion] {name} {arguments.ToString(Formatting.None)} -> {outcome}", this);
                        history.Add(ToolMessage((string)call["id"], name, outcome));
                        pendingCalls.Remove(call);
                    }
                    string report = succeeded
                        ? LocalReply(order, "Done.", "Gotowe.", "Готово.")
                        : LocalReply(order, "I couldn't complete every action.", "Nie udało mi się wykonać wszystkich poleceń.", "Не удалось выполнить все действия.");
                    history.Add(new JObject { ["role"] = "assistant", ["content"] = report });
                    Spoke?.Invoke(report);
                }
            }
            if (finishOrder) IsBusy = false;
            SetStatus("");
        }

        private static string LocalReply(string order, string english, string polish, string russian)
        {
            foreach (char letter in order) if (letter >= '\u0400' && letter <= '\u04ff') return russian;
            foreach (char letter in order) if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".IndexOf(letter) >= 0) return polish;
            return english;
        }

        private JObject BuildRequest(bool allowTools)
        {
            TrimHistory();
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
            return chat.CreateRequest(messages, definitions);
        }

        // OpenAI-compatible calls carry arguments as JSON strings.
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

        public static bool ValidArguments(JObject schema, JObject arguments)
        {
            if (schema["required"] is JArray required)
                foreach (JToken key in required)
                    if (arguments[(string)key] == null) return false;
            foreach (JProperty argument in arguments.Properties())
            {
                JToken property = schema["properties"]?[argument.Name];
                if (property == null || argument.Value.Type != JTokenType.String) return false;
                if (property["enum"] is JArray allowed && !Contains(allowed, argument.Value)) return false;
            }
            return true;
        }

        private static bool Contains(JArray values, JToken value)
        {
            foreach (JToken candidate in values) if (JToken.DeepEquals(candidate, value)) return true;
            return false;
        }

        private static JObject ToolMessage(string id, string name, string content) =>
            new JObject { ["role"] = "tool", ["tool_call_id"] = id, ["name"] = name, ["content"] = content };

        // Drops whole old orders, so a function result never loses the call it answers.
        private void TrimHistory()
        {
            while (history.Count > historyLimit || HistoryCharacters() > 2500)
            {
                int nextOrder = history.FindIndex(1, message => (string)message["role"] == "user");
                if (nextOrder < 0) break;
                history.RemoveRange(0, nextOrder);
            }
        }

        private int HistoryCharacters()
        {
            int count = 0;
            foreach (JObject message in history) count += message.ToString(Formatting.None).Length;
            return count;
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
            chat.Cancel();
        }
    }
}
