using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static EchoSound.Companion.CompanionPhrases;

namespace EchoSound.Companion
{
    /// <summary>A cached, bounded LLM interpreter. It chooses intents; game code chooses and validates targets.</summary>
    public sealed class FastIntentClient
    {
        public const string Prompt = @"Classify the player's request as a JSON array of one or two actions in spoken order.
follow = keep walking with the player, stay close (follow me, chodź za mną, trzymaj się blisko).
stop = stay here and wait for the player (wait here, zostań tu, poczekaj).
come = walk toward the player once (come here, podejdź do mnie, chodź tutaj).
open = open a door (otwórz, uchyl). close = close a door (zamknij, przymknij, shut).
The game handles door directions; output only the verb. Repeat a door action if asked to do it to two different doors. A sentence requesting the same movement in two ways is one action.
Questions about the world, statements, negated or conditional orders, and unsupported actions (including breaking, holding, picking up) yield [""clarify""]. Polite requests are orders.
Poczekaj tu na mnie -> [""stop""]
Keep up and stay close -> [""follow""]
Czy możesz przymknąć drzwi po lewej? -> [""close""]
Uchyl drzwi po lewej, potem zamknij drzwi po prawej -> [""open"",""close""]
Come here and then open the door -> [""come"",""open""]
Wyważ drzwi -> [""clarify""]";

        public static readonly string[] Codes = { "follow", "stop", "come", "open", "close", "clarify" };
        private UnityWebRequest pending;
        private readonly CompanionConversationClient conversation = new CompanionConversationClient();

        private const RegexOptions Options = RegexOptions.CultureInvariant;
        private static readonly Regex SequenceSeparator = new Regex(@"(?:\s*,\s*|\s+)\b" + Any(b => b.Then) + @"\b\s+", Options);
        private static readonly Regex DoorMention = new Regex(@"\b" + Any(b => b.DoorMention) + @"\b", Options);
        private static readonly Regex Opening = new Regex(@"\b" + Any(b => b.Open.Concat(b.OpenOpenings).Concat(b.RequestedOpen).ToArray()) + @"\b", Options);
        private static readonly Regex Closing = new Regex(@"\b" + Any(b => b.Close.Concat(b.CloseOpenings).Concat(b.RequestedClose).ToArray()) + @"\b", Options);
        private static readonly Regex PoliteRequest = new Regex("^" + Any(b => b.PoliteRequest) + @"\s+", Options);
        private static readonly Regex Greeting = new Regex("^" + Any(b => b.Greeting) + @"\s*[,.!]\s*", Options);
        private static readonly Regex Courtesy = new Regex("^" + Any(b => b.Please) + @"\b[\s,.!]*", Options);
        private static readonly Regex UnsupportedOrder = new Regex("^(?:" + Any(b => b.Please) + " )?" + Any(b => b.Unsupported) + @"\b", Options);
        private static readonly Regex NegationOrCondition = new Regex(@"\b" + Any(b => b.Negation.Concat(b.Condition).ToArray()) + @"\b", Options);
        private static readonly Regex DeniedOrder = new Regex("^(?:" + Any(b => b.Negation) + @"\s+" + Any(b => b.OrderVerbs) + @"\b|" + Any(b => b.Condition) + @"\b)", Options);
        private static readonly Regex AnyDoor = new Regex(@"\b" + Any(b => b.Quantifier) + @"\s+(?:" + Any(b => b.Determiner) + @"\s+)?" + Any(b => b.DoorMention) + @"\b", Options);
        private static readonly Regex HoldDoor = new Regex(@"\b" + Any(b => b.HoldDoor) + @"\b", Options);
        private static readonly Regex ReportedSpeech = new Regex(@"\b" + Any(b => b.Reported) + @"\b", Options);
        private static readonly Regex FollowSuffix = new Regex(@"\s+(?:" + Any(b => b.And) + @")\s+(?=" + Any(b => b.FollowOpenings) + @"\b)", Options);
        private static readonly Regex AllowedTail = new Regex("^(?:" + Any(b => b.Please) + "|now|teraz)$", Options);

        // A plain order starts with an imperative. After a polite request, Polish uses the infinitive instead.
        private sealed class Intent
        {
            private readonly Regex imperative, requested;

            public Intent(Func<Phrasebook, string[]> openings, Func<Phrasebook, string[]> afterRequest)
            {
                imperative = new Regex("^" + Any(openings) + @"\b", Options);
                requested = new Regex("^" + Any(book => openings(book).Concat(afterRequest(book)).ToArray()) + @"\b", Options);
            }

            public bool Starts(string head, bool polite) => (polite ? requested : imperative).IsMatch(head);
        }

        private static readonly Intent Follow = new Intent(b => b.FollowOpenings, b => b.RequestedFollow);
        private static readonly Intent Stop = new Intent(b => b.StopOpenings, b => b.RequestedStop);
        private static readonly Intent Come = new Intent(b => b.ComeOpenings, b => b.RequestedCome);
        private static readonly Intent OpenDoor = new Intent(b => b.OpenOpenings, b => b.RequestedOpen);
        private static readonly Intent CloseDoor = new Intent(b => b.CloseOpenings, b => b.RequestedClose);

        public sealed class Turn
        {
            public string[] Actions { get; }
            public string Reply { get; }

            public Turn(string[] actions, string reply)
            {
                Actions = actions;
                Reply = reply;
            }
        }

        // Conservative boundaries: a small instruction model can drop negations and conditions.
        // Reject the whole phrase instead of executing a plausible but unintended subset.
        public static bool NeedsClarification(string order)
        {
            if (string.IsNullOrWhiteSpace(order)) return true;
            string text = StripGreeting(order);
            return UnsupportedOrder.IsMatch(text) || DeniedOrder.IsMatch(text)
                || (DoorMention.IsMatch(text) && (AnyDoor.IsMatch(text) || HoldDoor.IsMatch(text)))
                || (HasCommandHead(text) && NegationOrCondition.IsMatch(text));
        }

        public static bool CanPlan(string order)
        {
            if (string.IsNullOrWhiteSpace(order) || order.Length > 220 || NeedsClarification(order)) return false;
            string text = StripGreeting(order);
            if (CompanionLanguage.HasQuote(text) || ReportedSpeech.IsMatch(text)) return false;
            foreach (string clause in SequenceSeparator.Split(text))
            {
                CommandHead(clause, out bool polite);
                if (!polite && clause.Contains("?")) return false;
                if (!HasCommandHead(clause)) return false;
            }
            return true;
        }

        /// <summary>Folded order without a leading greeting such as "hey," or "cześć,".</summary>
        internal static string StripGreeting(string order) => Greeting.Replace(CompanionLanguage.Fold(order.Trim()), "");

        private static string CommandHead(string text, out bool polite)
        {
            string head = text.Trim();
            bool courteous = Courtesy.IsMatch(head);
            head = Courtesy.Replace(head, "");
            polite = courteous || PoliteRequest.IsMatch(head);
            return Courtesy.Replace(PoliteRequest.Replace(head, ""), "");
        }

        private static bool HasCommandHead(string text)
        {
            string head = CommandHead(text, out bool polite);
            return Follow.Starts(head, polite) || Stop.Starts(head, polite) || Come.Starts(head, polite)
                || (DoorMention.IsMatch(head) && (OpenDoor.Starts(head, polite) || CloseDoor.Starts(head, polite)));
        }

        public static JObject CreateRequest(string order, JArray recent = null, string facts = null, bool forcePlan = false, bool detailed = false, string context = null)
        {
            if (!forcePlan && !CanPlan(order)) return CompanionConversationClient.CreateRequest(order, recent, facts, detailed, context);
            string prompt = Prompt;
            int tokenLimit = 12;
            JObject schema = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 2,
                ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Codes) } };
            var messages = new JArray(new JObject { ["role"] = "system", ["content"] = prompt });
            messages.Add(new JObject { ["role"] = "user", ["content"] = order });
            return new JObject
            {
                ["messages"] = messages,
                ["temperature"] = 0, ["max_tokens"] = tokenLimit, ["cache_prompt"] = true,
                ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false },
                ["response_format"] = new JObject { ["type"] = "json_schema", ["json_schema"] = new JObject
                {
                    ["name"] = "intent", ["strict"] = true,
                    ["schema"] = schema
                } }
            };
        }

        public static Turn ReadTurn(string content)
        {
            if (!content.TrimStart().StartsWith("{", StringComparison.Ordinal)) return new Turn(ReadPlan(content), null);
            JObject json = JObject.Parse(content);
            string reply = json.Count == 1 && json["reply"]?.Type == JTokenType.String ? ((string)json["reply"]).Trim() : "";
            if (reply.Length == 0 || reply.Length > 180 || reply.Contains("\n") || reply.Contains("<"))
                throw new JsonException("Invalid conversational reply.");
            if (reply.Length >= 53 && reply[reply.Length - 1] != '.' && reply[reply.Length - 1] != '!' && reply[reply.Length - 1] != '?')
            {
                int end = reply.LastIndexOfAny(new[] { '.', '!', '?' });
                if (end > 0) reply = reply.Substring(0, end + 1);
            }
            return new Turn(null, reply);
        }

        public static string[] ReadPlan(string content)
        {
            JArray array = JArray.Parse(content);
            if (array.Count < 1 || array.Count > 2) throw new JsonException("Invalid intent count.");
            var result = new List<string>();
            foreach (JToken value in array)
            {
                if (value.Type != JTokenType.String || Array.IndexOf(Codes, (string)value) < 0)
                    throw new JsonException("Invalid intent.");
                string code = (string)value;
                if (result.Contains(code) && !IsDoorCode(code)) throw new JsonException("Repeated intent.");
                result.Add(code);
            }
            if (result.Contains("clarify") && result.Count != 1) throw new JsonException("Ambiguous plan.");
            return result.ToArray();
        }

        private static bool IsDoorCode(string code) => code == "open" || code == "close";

        public static bool TryResolvePlan(string[] plan, string order, out QuickDoorCommand.Request?[] doors)
        {
            doors = null;
            if (plan.Length < 1 || plan.Length > 2 || !CanPlan(order)) return false;
            string[] clauses = SequenceSeparator.Split(StripGreeting(order));
            if (clauses.Length > 1 && clauses.Length != plan.Length) return false;
            if (clauses.Length == 1 && plan.Length == 2) return false;

            var resolved = new QuickDoorCommand.Request?[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                string code = plan[i];
                string clause = clauses[i];
                string head = CommandHead(clause, out bool polite);
                if (code == "stop" && Stop.Starts(head, polite) || code == "follow" && Follow.Starts(head, polite)
                    || code == "come" && Come.Starts(head, polite)) continue;
                if (code != "open" && code != "close" || !(code == "open" ? OpenDoor : CloseDoor).Starts(head, polite)) return false;
                if (!TryDoorRequest(code, clause, out var request)) return false;
                if (i > 0 && code == plan[0] && resolved[0]?.Side == request.Side) return false;
                resolved[i] = request;
            }
            doors = resolved;
            return true;
        }

        public static bool TryDoorRequest(string code, string clause, out QuickDoorCommand.Request request)
        {
            request = default;
            if (!IsDoorCode(code)) return false;
            // "Open the door and follow me": the movement clause is not part of the door's location.
            clause = FollowSuffix.Split(CompanionLanguage.Fold(clause))[0];
            if (!DoorMention.IsMatch(clause)) return false;
            bool open = code == "open";
            if (open ? Closing.IsMatch(clause) : Opening.IsMatch(clause)) return false;
            request.Action = open ? Interaction.InteractionAction.Open : Interaction.InteractionAction.Close;

            // Directions come from this clause only. Without one, use the usual gaze-based selection.
            request.Side = QuickDoorCommand.Direction.Facing;
            List<QuickDoorCommand.Direction> sides = QuickDoorCommand.SidesMentioned(clause);
            if (sides.Count > 1) return false;
            if (sides.Count == 1) request.Side = sides[0];
            else
            {
                Match mention = DoorMention.Match(clause);
                string tail = clause.Substring(mention.Index + mention.Length).Trim(' ', ',', '.', '!', '?');
                // An unfamiliar location must not silently turn into the door in front.
                if (tail.Length > 0 && !AllowedTail.IsMatch(tail)) return false;
            }
            return true;
        }

        public IEnumerator Send(string order, JArray recent, string facts, Action<Turn> onTurn, Action<string> onError, bool warmup = false,
            Action<string> onProgress = null, bool detailed = false, string context = null)
        {
            Cancel();
            if (!warmup && !CanPlan(order))
            {
                yield return conversation.Send(CreateRequest(order, recent, facts, false, detailed, context),
                    text => onTurn(new Turn(null, text)), onError, onProgress, detailed);
                yield break;
            }
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            if (!runtime.ChatReady) { onError("The language model is not ready."); yield break; }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var web = new UnityWebRequest(runtime.ChatUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(CreateRequest(order, recent, facts, warmup).ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(), timeout = warmup ? 30 : 2
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SetRequestHeader("Authorization", "Bearer " + runtime.ApiKey);
            pending = web;
            var operation = web.SendWebRequest();
            double budget = warmup ? 30 : CanPlan(order) ? 1.5 : 1.9;
            while (!operation.isDone && timer.Elapsed.TotalSeconds < budget) yield return null;
            if (pending != web) yield break;
            if (!operation.isDone)
            {
                Cancel();
                Debug.LogWarning($"[Fast LLM] deadline={budget:0.0}s exceeded; no actions executed.");
                onError("Interpretation timed out.");
                yield break;
            }
            pending = null;
            bool success = web.result == UnityWebRequest.Result.Success;
            string body = web.downloadHandler.text;
            web.Dispose();
            if (!success) { onError("Interpretation failed."); yield break; }
            Turn turn;
            try
            {
                var response = JObject.Parse(body);
                if ((string)response["choices"]?[0]?["finish_reason"] != "stop") throw new JsonException("Incomplete intent.");
                turn = ReadTurn((string)response["choices"]?[0]?["message"]?["content"] ?? "");
                Debug.Log($"[Fast LLM] {(warmup ? "warmup" : "interpretation")}={timer.Elapsed.TotalSeconds:0.000}s cached_tokens={response["usage"]?["prompt_tokens_details"]?["cached_tokens"]} output_tokens={response["usage"]?["completion_tokens"]}");
            }
            catch (JsonException) { onError("Invalid interpretation."); yield break; }
            onTurn(turn);
        }

        public void Cancel()
        {
            conversation.Cancel();
            if (pending == null) return;
            pending.Abort(); pending.Dispose(); pending = null;
        }
    }
}
