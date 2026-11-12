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
        public const string Prompt = @"Translate the player's words into game actions. Output a JSON array, up to two actions, in order.
Actions:
follow = continuously walk with the player, stay close, keep up. Следовать, идти следом, держаться рядом. Podążaj, trzymaj się blisko.
stop = STOP MOVING and WAIT at your current position, stay here, wait for the player to return. Ждать, подождать, остаться на месте. Czekaj, zaczekaj, zostań tutaj.
come = walk to the player once and stop. Come closer, return to me. Подойти, вернуться ко мне. Podejdź, wróć do mnie.
open = OPEN a door, make a closed doorway passable. Open, unlock, распахнуть, открыть, приоткрыть, otworzyć.
close = CLOSE a door, block an open doorway. Close, shut, закрыть, прикрыть, zamknąć.
These are the ONLY supported actions. Breaking, destroying, picking up, attacking, holding objects or discussing the world are unsupported: clarify.
One action per distinct desired change. Stay close and follow are ONE follow action. Wait for me is stop, NOT follow. A door without a stated direction uses open/close, never invent a direction.
Door directions relative to player: open_front, open_back, open_left, open_right, open_nearest; close_front, close_back, close_left, close_right, close_nearest.
Directions: за мной, позади меня, za mną = back; передо мной, впереди, przede mną = front; слева, po lewej = left; справа, po prawej = right. Without a direction use open or close.
Use clarify for questions, statements, chat, hypothetical or conditional orders, unsupported actions or unclear targets. Never execute a negated action. Negation with no positive command means clarify. A request phrased as 'please' or 'could you' is still an order. Do not invent actions.
Examples:
Wait until I return -> [""stop""]
Дождись меня здесь -> [""stop""]
Keep up and stay near me -> [""follow""]
Распахни дверь справа -> [""open_right""]
Прикрой дверь слева -> [""close_left""]
Destroy the door -> [""clarify""]
Don't close the door -> [""clarify""]
If someone knocks, open the door -> [""clarify""]
I will open the door -> [""clarify""]
Не открывай дверь, стой тут -> [""stop""]
Дверь за мной открыта? -> [""clarify""]
Открой дверь если я попрошу -> [""clarify""]
Иди ко мне -> [""come""]
Не отставай от меня -> [""follow""]
Открой дверь впереди и потом следуй за мной -> [""open_front"",""follow""]";

        public static readonly string[] Codes = { "follow", "stop", "come", "open", "close", "clarify",
            "open_front", "open_back", "open_left", "open_right", "open_nearest",
            "close_front", "close_back", "close_left", "close_right", "close_nearest" };
        private UnityWebRequest pending;

        // Conservative boundaries: a small instruction model can drop negations and conditions.
        // Reject the whole phrase instead of executing a plausible but unintended subset.
        public static bool NeedsClarification(string order)
        {
            if (string.IsNullOrWhiteSpace(order) || order.Length > 220) return true;
            string text = order.ToLowerInvariant().Replace('’', '\'');
            if (Regex.IsMatch(text, @"\b(не|ни|нет|если|когда|not|don't|dont|never|unless|if|when|nie|jeśli|jesli|jeżeli|jezeli|gdy)\b")) return true;
            if (Regex.IsMatch(text, @"\b(?:все|всех|каждую)\s+двер|\b(?:all|every)\s+(?:the\s+)?doors?\b|\bwszystkie\s+drzwi\b")) return true;
            if (Regex.IsMatch(text, @"\b(подержи|держи|удерживай|hold|trzymaj)\b.*(двер|door|drzwi)")) return true;
            if (Regex.IsMatch(text.Trim(), @"^(я|мы|i|we|ja|my)\b") || text.Contains("\"") || text.Contains("«")) return true;
            if (text.Contains("?") && !Regex.IsMatch(text.Trim(), @"^(можешь|можете|мог бы ты|can you|could you|would you|czy możesz|czy mozesz)\b")) return true;
            return false;
        }

        public static JObject CreateRequest(string order)
        {
            return new JObject
            {
                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = Prompt }, new JObject { ["role"] = "user", ["content"] = order }),
                ["temperature"] = 0, ["max_tokens"] = 24, ["cache_prompt"] = true,
                ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false },
                ["response_format"] = new JObject { ["type"] = "json_schema", ["json_schema"] = new JObject
                {
                    ["name"] = "intent", ["strict"] = true,
                    ["schema"] = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 2,
                        ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Codes) } }
                } }
            };
        }

        public static string[] ReadPlan(string content)
        {
            JArray array = JArray.Parse(content);
            if (array.Count < 1 || array.Count > 2) throw new JsonException("Invalid intent count.");
            var result = new List<string>();
            foreach (JToken value in array)
            {
                if (value.Type != JTokenType.String || Array.IndexOf(Codes, (string)value) < 0 || result.Contains((string)value))
                    throw new JsonException("Invalid intent.");
                result.Add((string)value);
            }
            if (result.Contains("clarify") && result.Count != 1) throw new JsonException("Ambiguous plan.");
            return result.ToArray();
        }

        public static bool TryDoorRequest(string code, string order, out QuickDoorCommand.Request request)
        {
            request = default;
            if (Array.IndexOf(Codes, code) < 0 || !(code == "open" || code == "close" || code.StartsWith("open_") || code.StartsWith("close_"))) return false;
            request.Action = code.StartsWith("open") ? Interaction.InteractionAction.Open : Interaction.InteractionAction.Close;
            string side = code.Contains("_") ? code.Substring(code.IndexOf('_') + 1) : "";
            request.Side = side == "front" ? QuickDoorCommand.Direction.Front : side == "back" ? QuickDoorCommand.Direction.Back
                : side == "left" ? QuickDoorCommand.Direction.Left : side == "right" ? QuickDoorCommand.Direction.Right
                : side == "nearest" ? QuickDoorCommand.Direction.Nearest : QuickDoorCommand.Direction.Facing;
            // Ground explicit directions in the door clause, not in a later 'follow me' clause.
            string clause = Regex.Split(order.ToLowerInvariant(), @"\b(?:и потом|а потом|и затем|затем|потом|and then|then|a potem|i potem|potem|и следуй|и иди|and follow|i chodź|i idź)\b")[0];
            if (!Regex.IsMatch(clause, @"двер|\bdoor\b|\bdrzwi\b")) return true;
            var sides = new List<QuickDoorCommand.Direction>();
            if (Regex.IsMatch(clause, @"\b(передо мной|перед мной|впереди|in front|przede mną|przede mna)\b")) sides.Add(QuickDoorCommand.Direction.Front);
            if (Regex.IsMatch(clause, @"\b(за мной|позади меня|сзади|behind me|za mną|za mna)\b")) sides.Add(QuickDoorCommand.Direction.Back);
            if (Regex.IsMatch(clause, @"\b(слева|(?:on|to) (?:my|the) left|left door|po lewej)\b")) sides.Add(QuickDoorCommand.Direction.Left);
            if (Regex.IsMatch(clause, @"\b(справа|(?:on|to) (?:my|the) right|right door|po prawej)\b")) sides.Add(QuickDoorCommand.Direction.Right);
            if (sides.Count > 1) return false;
            if (sides.Count == 1) request.Side = sides[0];
            return true;
        }

        public IEnumerator Send(string order, Action<string[]> onPlan, Action<string> onError, bool warmup = false)
        {
            Cancel();
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            if (!runtime.ChatReady) { onError("The language model is not ready."); yield break; }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var web = new UnityWebRequest(runtime.ChatUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(CreateRequest(order).ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(), timeout = warmup ? 30 : 2
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SetRequestHeader("Authorization", "Bearer " + runtime.ApiKey);
            pending = web;
            var operation = web.SendWebRequest();
            double budget = warmup ? 30 : 1.5;
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
            string[] plan;
            try
            {
                var response = JObject.Parse(body);
                if ((string)response["choices"]?[0]?["finish_reason"] != "stop") throw new JsonException("Incomplete intent.");
                plan = ReadPlan((string)response["choices"]?[0]?["message"]?["content"] ?? "");
                Debug.Log($"[Fast LLM] {(warmup ? "warmup" : "interpretation")}={timer.Elapsed.TotalSeconds:0.000}s cached_tokens={response["usage"]?["prompt_tokens_details"]?["cached_tokens"]} output_tokens={response["usage"]?["completion_tokens"]}");
            }
            catch (JsonException) { onError("Invalid interpretation."); yield break; }
            onPlan(plan);
        }

        public void Cancel()
        {
            if (pending == null) return;
            pending.Abort(); pending.Dispose(); pending = null;
        }
    }
}
