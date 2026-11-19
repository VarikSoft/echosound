using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>A cached, bounded LLM interpreter. It chooses intents; game code chooses and validates targets.</summary>
    public sealed class FastIntentClient
    {
        public const string Prompt = @"Classify the player's request as a JSON array of one or two actions in spoken order.
follow = keep walking with the player, stay close (иди за мной, держись рядом, podążaj).
stop = stay here and wait for the player (жди здесь, останься, zaczekaj).
come = walk toward the player once (ко мне, подойди ближе, podejdź do mnie, wróć do mnie).
open = open a door (открой, распахни, otwórz). close = close a door (закрой, прикрой, zamknij).
The game handles door directions; output only the verb. Repeat a door action if asked to do it to two different doors. A sentence requesting the same movement in two ways is one action.
Questions about the world, statements, negated or conditional orders, and unsupported actions (including breaking, holding, picking up) yield [""clarify""]. Polite requests are orders.
Дождись меня здесь -> [""stop""]
Keep up and stay close -> [""follow""]
Можешь прикрыть дверь слева? -> [""close""]
Распахни дверь слева, затем прикрой дверь справа -> [""open"",""close""]
Come here and then open the door -> [""come"",""open""]
Сломай дверь -> [""clarify""]";

        public static readonly string[] Codes = { "follow", "stop", "come", "open", "close", "clarify" };
        private UnityWebRequest pending;
        private readonly CompanionConversationClient conversation = new CompanionConversationClient();
        private static readonly Regex SequenceSeparator = new Regex(@"(?:\s*,\s*|\s+)\b(?:and then|then|и потом|а потом|и затем|затем|потом|a potem|i potem|potem)\b\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex DoorMention = new Regex(@"\b(?:двер[а-яё]*|doors?|drzwi)\b");
        private static readonly Regex Opening = new Regex(@"\b(?:открой(?:те)?|открыть|распахни(?:те)?|приоткрой(?:те)?|open|otwórz|otworz)\b");
        private static readonly Regex Closing = new Regex(@"\b(?:закрой(?:те)?|закрыть|прикрой(?:те)?|захлопни(?:те)?|close|shut|zamknij)\b");
        private static readonly Regex PoliteRequest = new Regex(@"^(можешь|можете|мог бы ты|can you|could you|would you|czy możesz|czy mozesz)\s+");
        private static readonly Regex Greeting = new Regex(@"^(привет|здравствуй|hey|hi|hello|hej|cześć)\s*[,.!]\s*");
        private static readonly Regex Courtesy = new Regex(@"^(пожалуйста|плиз|please|proszę|prosze)\b[\s,.!]*");
        private static readonly Regex Follow = new Regex(@"^(?:(?:иди|идти|ходи|следуй|следовать|двигайся|иди следом) за мной|держись рядом|держаться рядом|follow me|follow my lead|stick with me|stay close|keep up|keep following|trzymaj się blisko|(?:idź|chodź|podążaj) za mną)\b");
        private static readonly Regex Stop = new Regex(@"^(?:стой|стоп|остановись|остановиться|жди|подожди|подождать|побудь|побыть|останься|остаться|дождись|stop|wait|remain (?:here|where you are|at your current position)|stay (?:here|put|where you are)|czekaj|poczekaj|zaczekaj|stój|zostań|zatrzymaj się)\b");
        private static readonly Regex Come = new Regex(@"^(?:подойди|подойти|возвращайся|вернись|вернуться|приблизься|иди (?:ко мне|сюда)|come (?:here|closer|to me|back)|walk over to me|podejdź|podejdz|wróć|chodź (?:tu|tutaj|do mnie))\b");
        private static readonly Regex UnsupportedOrder = new Regex(@"^(?:пожалуйста |please |proszę )?(?:подбери|подними|возьми|принеси|сломай|pick up|bring|break|podnieś|przynieś|złam)\b");
        private static readonly Regex NegationOrCondition = new Regex(@"\b(не|ни|нет|если|когда|not|don't|dont|never|unless|if|when|nie|jeśli|jesli|jeżeli|jezeli|gdy)\b");
        private static readonly Regex DeniedOrder = new Regex(@"^(?:(?:не|don't|dont|nie)\s+(?:иди|уходи|жди|открывай|закрывай|открой|закрой|open|close|follow|leave|wait|otwieraj|zamykaj|idź)\b|(?:если|if|jeśli|jeżeli)\b)", RegexOptions.CultureInvariant);
        private static readonly Regex AnyDoor = new Regex(@"\b(?:все|всех|каждую)\s+двер|\b(?:all|every)\s+(?:the\s+)?doors?\b|\bwszystkie\s+drzwi\b");
        private static readonly Regex HoldDoor = new Regex(@"\b(подержи|держи|удерживай|hold|trzymaj)\b.*(двер|door|drzwi)");
        private static readonly Regex ReportedSpeech = new Regex(@"\b(?:сказал|сказала|говорит|говорил|повтори|repeat|said|powiedział|powiedziała|powtórz)\b");
        private static readonly Regex RussianInfinitive = new Regex(@"^(?:идти|следовать|держаться|остановиться|подождать|побыть|остаться|подойти|вернуться|открыть|закрыть)\b");

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
            if (text.IndexOfAny(new[] { '"', '«', '»', '“', '”' }) >= 0 || ReportedSpeech.IsMatch(text)) return false;
            foreach (string clause in SequenceSeparator.Split(text))
            {
                string request = Courtesy.Replace(clause.Trim(), "");
                bool politeRequest = PoliteRequest.IsMatch(request);
                if (!politeRequest && (clause.Contains("?") || RussianInfinitive.IsMatch(request))) return false;
                if (!HasCommandHead(clause)) return false;
            }
            return true;
        }

        internal static string StripGreeting(string order) =>
            Greeting.Replace(order.Trim().ToLowerInvariant().Replace('’', '\''), "");

        private static string CommandHead(string text)
        {
            text = Courtesy.Replace(text.Trim(), "");
            return Courtesy.Replace(PoliteRequest.Replace(text, ""), "");
        }

        private static bool HasCommandHead(string text)
        {
            string head = CommandHead(text);
            return Follow.IsMatch(head) || Stop.IsMatch(head) || Come.IsMatch(head)
                || (DoorMention.IsMatch(head) && (StartsWith(Opening, head) || StartsWith(Closing, head)));
        }

        private static bool StartsWith(Regex regex, string text)
        {
            Match match = regex.Match(text);
            return match.Success && match.Index == 0;
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
                string head = CommandHead(clause);
                if (code == "stop" && Stop.IsMatch(head) || code == "follow" && Follow.IsMatch(head)
                    || code == "come" && Come.IsMatch(head)) continue;
                if (code != "open" && code != "close" || !StartsWith(code == "open" ? Opening : Closing, head)) return false;
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
            clause = Regex.Split(clause.ToLowerInvariant(), @"\b(?:и следуй|и иди|and follow|i chodź|i idź)\b")[0];
            if (!DoorMention.IsMatch(clause)) return false;
            bool open = code == "open";
            if (open ? Closing.IsMatch(clause) : Opening.IsMatch(clause)) return false;
            request.Action = open ? Interaction.InteractionAction.Open : Interaction.InteractionAction.Close;

            // Directions come from this clause only. Without one, use the usual gaze-based selection.
            request.Side = QuickDoorCommand.Direction.Facing;
            var sides = new List<QuickDoorCommand.Direction>();
            if (Regex.IsMatch(clause, @"\b(передо мной|перед мной|впереди|in front|przede mną|przede mna)\b")) sides.Add(QuickDoorCommand.Direction.Front);
            if (Regex.IsMatch(clause, @"\b(за мной|позади меня|сзади|behind me|za mną|za mna)\b")) sides.Add(QuickDoorCommand.Direction.Back);
            if (Regex.IsMatch(clause, @"\b(слева|(?:on|to) (?:my|the) left|left door|po lewej)\b")) sides.Add(QuickDoorCommand.Direction.Left);
            if (Regex.IsMatch(clause, @"\b(справа|(?:on|to) (?:my|the) right|right door|po prawej)\b")) sides.Add(QuickDoorCommand.Direction.Right);
            if (Regex.IsMatch(clause, @"\b(ближайш[а-яё]*|nearest|closest|najbliższe|najblizsze)\b")) sides.Add(QuickDoorCommand.Direction.Nearest);
            if (sides.Count > 1) return false;
            if (sides.Count == 1) request.Side = sides[0];
            else
            {
                Match mention = DoorMention.Match(clause);
                string tail = clause.Substring(mention.Index + mention.Length).Trim(' ', ',', '.', '!', '?');
                // An unfamiliar location must not silently turn into the door in front.
                if (tail.Length > 0 && !Regex.IsMatch(tail, @"^(?:пожалуйста|сейчас|please|now|proszę|prosze)$")) return false;
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
