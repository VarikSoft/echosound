using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        /// <summary>Actual generated text while the current reply is being written; empty clears it.</summary>
        public event Action<string> ReplyProgress;
        /// <summary>Short progress text, for example "thinking"; empty when idle.</summary>
        public event Action<string> StatusChanged;
        /// <summary>Connection or model problems the player should see.</summary>
        public event Action<string> Failed;

        public CompanionAgent Body { get; private set; }
        public CompanionPerception Perception { get; private set; }
        /// <summary>Uses objects through the same rules as the player's hands.</summary>
        public Interactor Hands { get; private set; }
        public bool IsBusy => orderInProgress || interpreting;
        private bool orderInProgress;
        private bool interpreting;
        private Coroutine activeOrder;
        private Coroutine input;
        private Coroutine doorEscort;
        public string LastCommandRoute { get; private set; }
        public enum CompanionMode { QuickCommands, FastLlm, FreeDialogue }
        public CompanionMode Mode { get; private set; }
        public bool FreeDialogue => Mode == CompanionMode.FreeDialogue;
        public bool FastReady { get; private set; }
        public bool FastStarting { get; private set; }
        private const string ModePreference = "EchoSound.CompanionMode";
        private readonly FastIntentClient fast = new FastIntentClient();
        private readonly FastIntentClient warmup = new FastIntentClient();
        private Coroutine preparation;
        private bool lastActionSucceeded;
        private bool started;

        public void SetFreeDialogue(bool enabled, bool remember = true)
            => SetMode(enabled ? CompanionMode.FreeDialogue : CompanionMode.QuickCommands, remember);

        public void SetMode(CompanionMode mode, bool remember = true)
        {
            Interrupt();
            StopPreparation();
            Mode = mode;
            if (mode == CompanionMode.QuickCommands) LocalAiRuntime.Instance.StopChat();
            else if (isActiveAndEnabled) preparation = StartCoroutine(PrepareFast());
            if (remember)
            {
                PlayerPrefs.SetInt(ModePreference, (int)mode);
                PlayerPrefs.Save();
            }
        }

        private readonly List<JObject> history = new List<JObject>();
        private readonly CompanionDialogue dialogue = new CompanionDialogue();
        private List<CompanionTool> fixedTools;

        private void Awake()
        {
            Body = GetComponent<CompanionAgent>();
            Perception = GetComponent<CompanionPerception>();
            Hands = GetComponent<Interactor>();
            fixedTools = CreateFixedTools();
            int saved = PlayerPrefs.GetInt(ModePreference, PlayerPrefs.GetInt("EchoSound.FreeDialogue", 0) == 1 ? 2 : 1);
            Mode = Enum.IsDefined(typeof(CompanionMode), saved) ? (CompanionMode)saved : CompanionMode.FastLlm;
        }

        private void Start()
        {
            started = true;
            _ = LocalAiRuntime.Instance;
            if (Mode != CompanionMode.QuickCommands && !FastStarting) preparation = StartCoroutine(PrepareFast());
        }

        private void OnEnable()
        {
            if (started && Mode != CompanionMode.QuickCommands) preparation = StartCoroutine(PrepareFast());
        }

        private IEnumerator PrepareFast()
        {
            FastReady = false;
            FastStarting = true;
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            runtime.EnsureChatStarted();
            while (runtime.ChatStarting) yield return null;
            if (runtime.ChatReady)
                yield return warmup.Send("Warm up", null, null, result => FastReady = true, error => Debug.LogWarning("[Fast LLM] " + error), true);
            FastStarting = false;
            preparation = null;
        }

        private void StopPreparation()
        {
            if (preparation != null) StopCoroutine(preparation);
            preparation = null;
            warmup.Cancel();
            FastStarting = FastReady = false;
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

        /// <summary>Only for direct player input from push-to-talk or chat. NPC speech and world audio are not orders.</summary>
        public void Command(string order)
        {
            order = order?.Trim();
            if (string.IsNullOrEmpty(order)) return;
            if (order.Length > 500) { Clarify(order); return; }

            if (logToConsole) Debug.Log("[Companion] Order: " + order, this);
            OrderReceived?.Invoke(order);
            CancelFastInput();

            if (QuickCompanionCommand.TryParseDismissal(order, out var dismissal))
            {
                BeginOrder(order, "direct");
                if (dismissal == QuickCompanionCommand.DismissalAction.CloseNearbyDoors)
                {
                    Body.Halt();
                    activeOrder = StartCoroutine(UseNearbyDoors(order, InteractionAction.Close));
                }
                else activeOrder = StartCoroutine(HandleQuick("wait_here", LocalReply(order, "Okay. I'll stay here.", "Dobrze. Zostaję tutaj.")));
                return;
            }

            if (QuickDoorCommand.TryParseStatus(order, out var direction))
            {
                AnswerDoorStatus(order, direction);
                return;
            }
            if (TryReplyLocally(order, out string grounded) || dialogue.TryReply(order, out grounded))
            {
                ReplyToConversation(order, grounded, "game-chat");
                return;
            }

            bool split = QuickCompanionCommand.TrySplitFollowup(order, out string first, out string followup);
            if (split && !QuickDoorCommand.TryParse(first, out _)) split = false;
            if (QuickDoorCommand.TryParseOpenAll(order, out bool escort))
            {
                BeginOrder(order, "direct");
                if (escort) StartDoorEscort(order);
                else activeOrder = StartCoroutine(UseNearbyDoors(order, InteractionAction.Open));
                return;
            }
            if (QuickCompanionCommand.TryParseFollowWithContext(order, out var followContext))
            {
                string note = (followContext & QuickCompanionCommand.FollowContext.Protection) != 0
                    ? LocalReply(order, " I can't help in a fight.", " W walce nie pomogę.") : "";
                BeginOrder(order, "direct");
                if ((followContext & QuickCompanionCommand.FollowContext.OpenOnPath) != 0)
                    StartDoorEscort(order, note);
                else
                    activeOrder = StartCoroutine(HandleQuick("follow_curator", LocalReply(order, "Following you.", "Idę za tobą.") + note));
                return;
            }
            if (split)
            {
                BeginOrder(first, "direct");
                activeOrder = StartCoroutine(HandleFollowup(first, followup));
            }
            else if (QuickCompanionCommand.TryParse(order, out string action, out string reply))
            {
                BeginOrder(order, "direct");
                activeOrder = StartCoroutine(HandleQuick(action, reply));
            }
            else if (QuickDoorCommand.TryParse(order, out var doorRequest))
            {
                BeginOrder(order, "direct");
                activeOrder = StartCoroutine(HandleDoor(order, doorRequest));
            }
            else if (Mode == CompanionMode.QuickCommands || FastIntentClient.NeedsClarification(order))
            {
                LastCommandRoute = "clarify";
                Clarify(order);
            }
            else if (FreeDialogue && !FastIntentClient.CanPlan(order))
            {
                LastCommandRoute = "model-chat";
                input = StartCoroutine(HandleFreeDialogue(order));
            }
            else
            {
                LastCommandRoute = FastIntentClient.CanPlan(order) ? "fast-llm" : "fast-chat";
                input = StartCoroutine(HandleFast(order));
            }
        }

        private void BeginOrder(string order, string route)
        {
            Interrupt();
            dialogue.NoteCommand(order);
            RecordUser(order);
            orderInProgress = true;
            LastCommandRoute = route;
        }

        private void RecordUser(string text)
        {
            history.Add(new JObject { ["role"] = "user", ["content"] = text });
            TrimHistory();
        }

        private void Clarify(string order, string reason = null)
        {
            string answer = reason ?? LocalReply(order,
                "Please give a direct movement or door command, with a direction if needed.",
                "Podaj bezpośrednie polecenie ruchu lub otwarcia/zamknięcia drzwi i kierunek.");
            Spoke?.Invoke(answer);
        }

        private void ReplyToConversation(string order, string reply, string route)
        {
            LastCommandRoute = route;
            dialogue.Remember(order, reply);
            if (!orderInProgress)
            {
                RecordUser(order);
                history.Add(new JObject { ["role"] = "assistant", ["content"] = reply });
            }
            Spoke?.Invoke(reply);
        }

        // The player describing their own action is not an order for the companion.
        private static readonly Regex PlayerDoorStatement = new Regex(
            @"^(?:i'll|i will|i'm going to|i am going to|let me) (?:open|close|shut) (?:the |this |that )?doors?\b"
            + @"|^(?:ja )?(?:teraz |zaraz |sam )?(?:otworze|zamkne|otwieram|zamykam) (?:te )?drzwi\b", RegexOptions.CultureInvariant);

        public enum SmallTalk { None, Farewell, Greeting, Thanks, Uneasy, PlayerDoorAction, Activity }

        /// <summary>Common teammate phrases with authored replies; matching ignores punctuation and Polish diacritics.</summary>
        public static SmallTalk ClassifySmallTalk(string order)
        {
            string text = CompanionLanguage.Simplify(order);
            if (text is "bye" or "see you" or "see you later" or "do zobaczenia" or "na razie") return SmallTalk.Farewell;
            if (text is "hi" or "hello" or "hey" or "hi there" or "hello there" or "czesc" or "hej" or "siema" or "dzien dobry") return SmallTalk.Greeting;
            if (text is "thanks" or "thank you" or "thanks a lot" or "thank you so much" or "dzieki" or "dziekuje"
                or "dziekuje bardzo" or "wielkie dzieki") return SmallTalk.Thanks;
            if (text is "i'm scared" or "i am scared" or "i'm afraid" or "it's creepy here" or "this place is creepy"
                or "boje sie" or "troche sie boje" or "strasznie tu" or "tu jest strasznie") return SmallTalk.Uneasy;
            if (PlayerDoorStatement.IsMatch(text)) return SmallTalk.PlayerDoorAction;
            if (text is "what are you doing" or "what are you doing now" or "are you following me" or "are you still following me"
                or "co robisz" or "co teraz robisz" or "idziesz za mna" or "czy idziesz za mna") return SmallTalk.Activity;
            return SmallTalk.None;
        }

        private bool TryReplyLocally(string order, out string reply)
        {
            switch (ClassifySmallTalk(order))
            {
                case SmallTalk.Farewell: reply = LocalReply(order, "See you.", "Do zobaczenia."); break;
                case SmallTalk.Greeting: reply = LocalReply(order, "Hey. How are you holding up?", "Cześć. Jak się trzymasz?"); break;
                case SmallTalk.Thanks: reply = LocalReply(order, "Anytime.", "Nie ma sprawy."); break;
                case SmallTalk.Uneasy: reply = LocalReply(order, "Let's stick together.", "Trzymajmy się razem."); break;
                case SmallTalk.PlayerDoorAction: reply = LocalReply(order, "Okay. Tell me if you need help.", "Dobrze. Daj znać, jeśli będzie trzeba pomóc."); break;
                case SmallTalk.Activity:
                    if (Body.IsWalking) reply = LocalReply(order, "I'm heading to the target.", "Idę do celu.");
                    else if (Body.Activity == "following the curator") reply = LocalReply(order, "I'm following you.", "Idę za tobą.");
                    else reply = LocalReply(order, "I'm waiting here.", "Czekam tutaj.");
                    break;
                default: reply = null; break;
            }
            return reply != null;
        }

        private IEnumerator HandleFast(string order)
        {
            if (!FastReady || !LocalAiRuntime.Instance.ChatReady)
            {
                if (FastIntentClient.CanPlan(order)) Clarify(order, FastStarting
                    ? LocalReply(order, "Give me a second. Short commands still work.", "Daj mi chwilę. Krótkie polecenia działają.")
                    : null);
                else AskToRepeat(order);
                yield break;
            }
            interpreting = true;
            if (!orderInProgress) SetStatus("interpreting");
            FastIntentClient.Turn turn = null;
            yield return fast.Send(order, dialogue.RecentMessages(), dialogue.Facts, result => turn = result, _ => { },
                onProgress: text => ReplyProgress?.Invoke(text), context: ConversationContext(order));
            FinishInput();
            if (turn?.Reply is string reply)
            {
                ReplyToConversation(order, reply, "fast-chat");
                yield break;
            }
            string[] plan = turn?.Actions;
            if (plan == null && !FastIntentClient.CanPlan(order))
            { AskToRepeat(order); yield break; }
            if (plan == null || Array.IndexOf(plan, "clarify") >= 0)
            { Clarify(order); yield break; }
            if (!FastIntentClient.TryResolvePlan(plan, order, out var doors))
            { Clarify(order); yield break; }
            BeginOrder(order, "fast-llm");
            activeOrder = StartCoroutine(ExecuteFastPlan(order, plan, doors));
        }

        private void AskToRepeat(string order)
        {
            LastCommandRoute = "chat-clarify";
            Spoke?.Invoke(LocalReply(order, "Could you say that another way?", "Możesz powiedzieć to inaczej?"));
        }

        private IEnumerator ExecuteFastPlan(string order, string[] plan, QuickDoorCommand.Request?[] doors)
        {
            for (int i = 0; i < plan.Length; i++)
            {
                string code = plan[i];
                if (doors[i] is QuickDoorCommand.Request request) yield return HandleDoor(order, request, false);
                else
                {
                    string action = code == "follow" ? "follow_curator" : code == "stop" ? "wait_here" : "go_to";
                    string reply = code == "follow" ? LocalReply(order, "Following you.", "Idę za tobą.")
                        : code == "stop" ? LocalReply(order, "Waiting here.", "Czekam tutaj.")
                        : LocalReply(order, "Coming to you.", "Idę do ciebie.");
                    yield return HandleQuick(action, reply, false);
                }
                if (!lastActionSucceeded) break;
            }
            orderInProgress = false;
            SetStatus("");
        }

        private IEnumerator HandleFollowup(string first, string followup)
        {
            QuickDoorCommand.TryParse(first, out var doorRequest);
            yield return HandleDoor(first, doorRequest, false);
            if (!lastActionSucceeded) { orderInProgress = false; yield break; }
            history.Add(new JObject { ["role"] = "user", ["content"] = followup });
            orderInProgress = true;
            QuickCompanionCommand.TryParse(followup, out string action, out string reply);
            yield return HandleQuick(action, reply);
        }

        private IEnumerator UseNearbyDoors(string order, InteractionAction action)
        {
            bool open = action == InteractionAction.Open;
            var remaining = new List<Door>();
            foreach (Interactable item in Perception.Perceived())
                if (item is Door door && door.IsOpen != open) remaining.Add(door);
            if (remaining.Count > 0)
            {
                SetStatus(open ? "opening nearby doors" : "closing nearby doors");
                Spoke?.Invoke(open
                    ? LocalReply(order, "I'll open the nearby doors.", "Otworzę pobliskie drzwi.")
                    : LocalReply(order, "I stopped following you. I'll close the nearby open doors.", "Już za tobą nie idę. Zamknę pobliskie otwarte drzwi."));
            }
            var failed = new HashSet<Door>();
            while (true)
            {
                remaining.RemoveAll(door => door != null && door.IsOpen == open);
                Door next = remaining.Find(door => door != null && door.isActiveAndEnabled && !failed.Contains(door)
                    && (door.IsInReach(Body.transform.position) || CompanionTool.TryChooseStandPoint(Body, door, out _)));
                if (next == null) break;
                yield return UseObjectTool.UseDoor(this, next, action);
                if (next != null && next.IsOpen != open) failed.Add(next);
            }
            string reply;
            if (open)
                reply = remaining.Count == 0
                    ? LocalReply(order, "The nearby doors are open.", "Pobliskie drzwi są otwarte.")
                    : LocalReply(order, "Some nearby doors are out of reach.", "Do części drzwi nie mogę dotrzeć.");
            else
            {
                Body.Halt();
                reply = remaining.Count == 0
                    ? LocalReply(order, "I've stopped. The nearby doors are closed.", "Zatrzymałem się. Pobliskie drzwi są zamknięte.")
                    : LocalReply(order, "Some nearby doors are still open. I've stopped.", "Część pobliskich drzwi jest nadal otwarta. Zatrzymałem się.");
            }
            history.Add(new JObject { ["role"] = "assistant", ["content"] = reply });
            Spoke?.Invoke(reply);
            orderInProgress = false;
            SetStatus("");
        }

        private void StartDoorEscort(string order, string note = "")
        {
            orderInProgress = false;
            if (Body.curator == null)
            {
                Clarify(order, LocalReply(order, "I can't find you.", "Nie mogę cię znaleźć."));
                return;
            }
            Body.Follow();
            string reply = LocalReply(order, "I'm with you. I'll open doors on the way.", "Idę za tobą i otwieram drzwi po drodze.") + note;
            history.Add(new JObject { ["role"] = "assistant", ["content"] = reply });
            Spoke?.Invoke(reply);
            doorEscort = StartCoroutine(FollowAndOpenDoors());
        }

        private IEnumerator FollowAndOpenDoors()
        {
            var interval = new WaitForSeconds(0.2f);
            Door lastUnavailable = null;
            float retryAt = 0;
            while (Body.curator != null)
            {
                yield return interval;
                if (!Body.IsPlaced || Body.curator == null) continue;
                Door door = FindEscortDoor();
                if (door == null || (door == lastUnavailable && Time.time < retryAt)) continue;
                if (!door.IsInReach(Body.transform.position) && !CompanionTool.TryChooseStandPoint(Body, door, out _))
                {
                    lastUnavailable = door;
                    retryAt = Time.time + 1;
                    continue;
                }
                yield return UseObjectTool.UseDoor(this, door, InteractionAction.Open);
                Body.Follow();
                if (door != null && !door.IsOpen)
                {
                    lastUnavailable = door;
                    retryAt = Time.time + 1;
                }
            }
            doorEscort = null;
        }

        private Door FindEscortDoor()
        {
            Transform player = Body.curator;
            Vector3 toPlayer = player.position - Body.transform.position;
            float distance = toPlayer.magnitude;
            if (distance > 0.01f)
            {
                Door between = ClosedDoorOnRay(Body.transform.position + Vector3.up, toPlayer / distance, Mathf.Min(distance, Perception.radius));
                if (between != null) return between;
            }
            return distance <= Body.followDistance + 2
                ? ClosedDoorOnRay(player.position + Vector3.up, player.forward, 3)
                : null;
        }

        private Door ClosedDoorOnRay(Vector3 origin, Vector3 direction, float distance)
        {
            Collider closest = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.transform.IsChildOf(Body.transform)
                    || hit.transform.IsChildOf(Body.curator)) continue;
                closest = hit.collider;
                nearest = hit.distance;
            }
            Door door = closest == null ? null : closest.GetComponentInParent<Door>();
            return door != null && door.isActiveAndEnabled && !door.IsOpen
                && Vector3.Distance(door.transform.position, Body.transform.position) <= Perception.radius ? door : null;
        }

        private IEnumerator HandleDoor(string order, QuickDoorCommand.Request request, bool finishOrder = true)
        {
            lastActionSucceeded = false;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Door door = ResolveDoor(request, false, out var resolution);
            string report = DoorResolutionReply(order, resolution);
            if (resolution == QuickDoorCommand.Resolution.AlreadyDone)
            {
                lastActionSucceeded = true;
                report = DoorStateReply(order, door);
            }
            else if (resolution == QuickDoorCommand.Resolution.Found)
            {
                string name = request.Action.Verb() + "_door";
                UseObjectTool tool = UseObjectTool.CreateFor(new[] { door }).Find(candidate => candidate.Name == name);
                SetStatus("acting");
                Spoke?.Invoke(LocalReply(order, "Okay.", "Dobrze."));
                string outcome = null;
                if (logToConsole) Debug.Log($"[Door timing] dispatch={timer.Elapsed.TotalSeconds:0.000}s action={name} target={door.id} route={LastCommandRoute}", this);
                yield return tool.Run(this, new JObject { ["target"] = door.id }, result => outcome = result);
                lastActionSucceeded = tool.Succeeded;
                if (logToConsole) Debug.Log($"[Companion] {name} target={door.id} -> {outcome}", this);
                // Preserve the real object result for subsequent conversation, including failures.
                history.Add(new JObject { ["role"] = "assistant", ["content"] = outcome });
                if (tool.Succeeded) report = LocalReply(order, "Done.", "Gotowe.");
                else if (outcome != null && outcome.Contains("standing in the doorway"))
                    report = LocalReply(order, "The doorway is blocked. Move away so I can close it.", "Przejście jest zajęte. Odsuń się od drzwi.");
                else if (outcome != null && outcome.Contains("reach"))
                    report = LocalReply(order, "I can't find a route to that door.", "Nie mogę dotrzeć do tych drzwi.");
                else report = LocalReply(order, "I couldn't use that door.", "Nie udało się użyć tych drzwi.");
            }
            if (logToConsole) Debug.Log($"[Companion] Door resolution={resolution}: {report}", this);
            history.Add(new JObject { ["role"] = "assistant", ["content"] = report });
            Spoke?.Invoke(report);
            if (finishOrder) orderInProgress = false;
            SetStatus("");
        }

        private void AnswerDoorStatus(string order, QuickDoorCommand.Direction direction)
        {
            Door door = ResolveDoor(new QuickDoorCommand.Request { Side = direction }, true, out var resolution);
            string answer = door != null ? DoorStateReply(order, door) : DoorResolutionReply(order, resolution);
            LastCommandRoute = "door-status";
            // A read-only question doesn't interrupt an action or add messages mid-plan.
            if (!IsBusy)
            {
                history.Add(new JObject { ["role"] = "user", ["content"] = order });
                history.Add(new JObject { ["role"] = "assistant", ["content"] = answer });
                TrimHistory();
            }
            Spoke?.Invoke(answer);
        }

        private Door ResolveDoor(QuickDoorCommand.Request request, bool readState, out QuickDoorCommand.Resolution resolution)
        {
            Transform player = Body.curator;
            Door aimed = null;
            if (player != null && request.Side == QuickDoorCommand.Direction.Facing)
            {
                Camera camera = player.GetComponentInChildren<Camera>();
                Vector3 origin = camera != null ? camera.transform.position : player.position + Vector3.up * 1.6f;
                Vector3 forward = camera != null ? camera.transform.forward : player.forward;
                if (Physics.Raycast(origin, forward, out RaycastHit hit, Perception.radius, ~0, QueryTriggerInteraction.Ignore))
                    aimed = hit.collider.GetComponentInParent<Door>();
            }
            return QuickDoorCommand.Resolve(Perception.Perceived(), player, request, aimed, out resolution, readState);
        }

        private static string DoorResolutionReply(string order, QuickDoorCommand.Resolution resolution)
        {
            switch (resolution)
            {
                case QuickDoorCommand.Resolution.Ambiguous:
                    return LocalReply(order, "Which door? Look at it or say left/right.", "Które drzwi? Spójrz na nie lub powiedz lewo/prawo.");
                case QuickDoorCommand.Resolution.Missing:
                    return LocalReply(order, "I don't know a door in that direction.", "Nie znam drzwi w tym kierunku.");
                case QuickDoorCommand.Resolution.NoPlayer:
                    return LocalReply(order, "I can't locate you to choose a door.", "Nie mogę cię znaleźć, żeby wybrać drzwi.");
                default: return null;
            }
        }

        private static string DoorStateReply(string order, Door door)
        {
            if (door.IsBusy) return door.IsOpen
                ? LocalReply(order, "The door is already opening.", "Drzwi już się otwierają.")
                : LocalReply(order, "The door is already closing.", "Drzwi już się zamykają.");
            return door.IsOpen
                ? LocalReply(order, "The door is already open.", "Drzwi są już otwarte.")
                : LocalReply(order, "The door is already closed.", "Drzwi są już zamknięte.");
        }

        private IEnumerator HandleQuick(string action, string reply, bool finishOrder = true)
        {
            lastActionSucceeded = false;
            CompanionTool tool = fixedTools.Find(candidate => candidate.Name == action);
            // A missing player must never produce a false "following" acknowledgement.
            if (action != "wait_here" && Body.curator == null)
            {
                ReportFailure("There is no player to follow.");
                orderInProgress = false;
                yield break;
            }
            Spoke?.Invoke(reply);
            string outcome = null;
            yield return tool.Run(this, new JObject { ["target"] = "curator" }, result => outcome = result);
            lastActionSucceeded = tool.Succeeded;
            if (action == "go_to" && outcome != "Arrived next to the curator.") Spoke?.Invoke(outcome);
            history.Add(new JObject { ["role"] = "assistant", ["content"] = outcome });
            if (finishOrder) orderInProgress = false;
            SetStatus("");
        }

        private void CancelFastInput()
        {
            if (input != null) StopCoroutine(input);
            input = null;
            if (!interpreting) return;
            fast.Cancel();
            chat.Cancel();
            FinishInput();
        }

        private void FinishInput()
        {
            input = null;
            interpreting = false;
            ReplyProgress?.Invoke("");
            if (!orderInProgress) SetStatus("");
        }

        /// <summary>Stops interpreting and cancels the current interaction or door escort.</summary>
        public void Interrupt()
        {
            CancelFastInput();
            bool wasEscorting = doorEscort != null;
            if (wasEscorting) StopCoroutine(doorEscort);
            doorEscort = null;
            if (!orderInProgress && !wasEscorting) return;
            if (activeOrder != null) StopCoroutine(activeOrder);
            activeOrder = null;
            chat.Cancel();

            if (Body.IsWalking || wasEscorting) Body.Halt();
            orderInProgress = false;
            SetStatus("");
        }

        /// <summary>Forgets the conversation, for example when a new level replaces every object.</summary>
        public void ResetConversation()
        {
            Interrupt();
            history.Clear();
            dialogue.Reset();
        }

        [ContextMenu("Reset system prompt")]
        private void ResetPrompt() => systemPrompt = DefaultPrompt;

        private IEnumerator HandleFreeDialogue(string order)
        {
            interpreting = true;
            if (!orderInProgress) SetStatus("thinking");
            FastIntentClient.Turn reply = null;
            string error = null;
            yield return fast.Send(order, dialogue.RecentMessages(), dialogue.Facts, result => reply = result, problem => error = problem,
                onProgress: text => ReplyProgress?.Invoke(text), detailed: true, context: ConversationContext(order));
            FinishInput();
            if (error != null) ReportFailure(error);
            else if (!string.IsNullOrWhiteSpace(reply?.Reply)) ReplyToConversation(order, reply.Reply, "model-chat");
        }

        /// <summary>The reply in the language of the player's words; the speech language setting breaks ties.</summary>
        internal static string LocalReply(string order, string english, string polish) => CompanionLanguage.Pick(order, english, polish);

        private static readonly Regex AboutSurroundings = new Regex(
            @"\b(?:doors?|see|where|nearby|around|drzwi|widzisz|gdzie|obok|dookola|wokol)\b", RegexOptions.CultureInvariant);

        private string ConversationContext(string order)
        {
            if (AboutSurroundings.IsMatch(CompanionLanguage.Fold(order)))
                return Perception.DescribeForConversation();
            return "Companion activity: " + Body.Activity + ". No other observations were supplied.";
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
            StopPreparation();
            chat.Cancel();
            fast.Cancel();
        }
    }
}
