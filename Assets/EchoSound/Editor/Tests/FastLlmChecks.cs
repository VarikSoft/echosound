using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EchoSound.Companion;

namespace EchoSound.Editor.Tests
{
    public static class FastLlmChecks
    {
        public static void Validate()
        {
            Require(FastIntentClient.ReadPlan("[\"open\",\"follow\"]").Length == 2, "Two-action plan.");
            var social = FastIntentClient.ReadTurn("{\"reply\":\"Привет. Я на связи.\"}");
            Require(social.Actions == null && social.Reply == "Привет. Я на связи.", "A social answer carries no actions.");
            foreach (string invalid in new[] { "[]", "[\"teleport\"]", "[\"open_left\"]", "[\"follow\",\"follow\"]", "[\"clarify\",\"open\"]", "[1]", "[\"open\",\"close\",\"stop\"]" })
            {
                bool rejected = false;
                try { FastIntentClient.ReadPlan(invalid); } catch (JsonException) { rejected = true; }
                Require(rejected, "Reject invalid plan " + invalid);
            }
            Require(FastIntentClient.TryDoorRequest("close", "Закрой за мной дверь пожалуйста", out var door) && door.Side == QuickDoorCommand.Direction.Back, "Resolve explicit direction from the transcript.");
            Require(FastIntentClient.TryDoorRequest("open", "Открой дверь и следуй за мной", out door) && door.Side == QuickDoorCommand.Direction.Facing, "Follow clause is not a door direction.");
            Require(!FastIntentClient.TryDoorRequest("open", "Открой дверь слева или справа", out _), "Ambiguous directions must clarify.");
            Require(!FastIntentClient.TryDoorRequest("open", "Открой дверь слива", out _),
                "An unknown direction from speech cannot silently select another door.");
            const string twoDoors = "Распахни дверь слева, а потом прикрой дверь справа";
            Require(FastIntentClient.TryResolvePlan(new[] { "open", "close" }, twoDoors, out var doors)
                && doors[0]?.Side == QuickDoorCommand.Direction.Left && doors[1]?.Side == QuickDoorCommand.Direction.Right,
                "Each step must resolve the direction from its own clause.");
            Require(!FastIntentClient.TryResolvePlan(new[] { "open" }, twoDoors, out _)
                && !FastIntentClient.TryResolvePlan(new[] { "close", "open" }, twoDoors, out _)
                && !FastIntentClient.TryResolvePlan(new[] { "open", "follow" }, twoDoors, out _),
                "An incomplete or reordered sequence must clarify before starting.");
            Require(!FastIntentClient.TryResolvePlan(new[] { "open" }, "Подними ключ", out _),
                "A plan cannot introduce a door absent from the request.");
            foreach (string text in new[] { "Пока!", "Я пойду за тобой", "У меня кот по кличке Плюш", "Ты идёшь за мной?",
                "«Стой»", "Стой — сказал он.", "Скажи, как открыть дверь", "Можешь рассказать короткую шутку?", "Открою дверь" })
                Require(!FastIntentClient.CanPlan(text) && !FastIntentClient.TryResolvePlan(new[] { "stop" }, text, out _),
                    "Conversation cannot authorize movement: " + text);
            Require(!FastIntentClient.TryResolvePlan(new[] { "come" }, "Останься здесь", out _),
                "The model cannot substitute another action for an approved command.");

            var fixtures = new JArray();
            foreach (var entry in new[] {
                new[] { "Побудь здесь пока я осмотрюсь", "stop" },
                new[] { "Держись рядом со мной", "follow" },
                new[] { "Подойди поближе ко мне", "come" },
                new[] { "Можешь открыть дверь слева от меня?", "open_left" },
                new[] { "Закрой за мной дверь пожалуйста", "close_back" },
                new[] { "Распахни дверь впереди", "open_front" },
                new[] { "Открой дверь слива", "clarify" },
                new[] { "Распахните дверь впереди, пожалуйста.", "open_front" },
                new[] { "Прикрой дверь справа", "close_right" },
                new[] { "Не открывай дверь, жди здесь", "clarify" },
                new[] { "Не закрывай дверь", "clarify" },
                new[] { "Дверь передо мной закрыта?", "reply" },
                new[] { "Если я скажу, открой дверь", "clarify" },
                new[] { "Я сейчас открою дверь", "reply" },
                new[] { "Открой дверь впереди и потом иди за мной", "open_front", "follow" },
                new[] { "Could you stay where you are?", "stop" },
                new[] { "Please stick with me", "follow" },
                new[] { "Come closer please", "come" },
                new[] { "Can you open the door on my right?", "open_right" },
                new[] { "Is the door behind me open?", "reply" },
                new[] { "Poczekaj tutaj aż wrócę", "stop" },
                new[] { "Trzymaj się blisko mnie", "follow" },
                new[] { "Podejdź bliżej do mnie", "come" },
                new[] { "Otwórz drzwi po lewej stronie", "open_left" },
                new[] { "Nie otwieraj drzwi", "clarify" },
                new[] { "Привет как дела", "reply" },
                new[] { "Я переживаю, что мы заблудимся.", "reply" },
                new[] { "Как у тебя настроение?", "reply" },
                new[] { "Thanks, glad you're here.", "reply" },
                new[] { "Miło cię widzieć, trochę się boję.", "reply" },
                new[] { "Подбери ключ у стены", "clarify" },
                // Held-out wording, not examples in the model prompt.
                new[] { "Останься на этом месте", "stop" },
                new[] { "Подожди меня тут", "stop" },
                new[] { "Иди следом за мной", "follow" },
                new[] { "Возвращайся ко мне", "come" },
                new[] { "Пожалуйста распахни ближайшую дверь", "open_nearest" },
                new[] { "Прикрой дверь позади меня", "close_back" },
                new[] { "Подержи дверь открытой", "clarify" },
                new[] { "Сломай эту дверь", "clarify" },
                new[] { "Принеси аптечку", "clarify" },
                new[] { "Куда ведёт эта дверь?", "reply" },
                new[] { "Wait right there for me", "stop" },
                new[] { "Walk over to me", "come" },
                new[] { "Stay close and follow my lead", "follow" },
                new[] { "Zaczekaj w tym miejscu", "stop" },
                new[] { "Wróć do mnie", "come" },
                new[] { "Привет, иди за мной.", "follow" },
                new[] { "Можешь подойти ко мне?", "come" },
                new[] { "Можешь подождать тут?", "stop" },
                new[] { "Ха-ха, неплохо.", "reply" },
                new[] { "Можешь рассказать короткую шутку?", "reply" },
                new[] { "У меня кот по кличке Плюш.", "reply" },
                new[] { twoDoors, "open_left", "close_right" },
                new[] { "Подойди ко мне, а потом распахни дверь справа", "come", "open_right" },
                new[] { "Приоткрой дверь справа, потом распахни дверь слева", "open_right", "open_left" }
            })
            {
                var expected = new JArray();
                for (int i = 1; i < entry.Length; i++) expected.Add(entry[i]);
                var accepted = new JArray();
                foreach (string first in FastIntentClient.Codes)
                {
                    AcceptIfExpected(new[] { first }, entry[0], expected, accepted);
                    if (first == "clarify") continue;
                    foreach (string second in FastIntentClient.Codes)
                        if (second != "clarify" && (second != first || first == "open" || first == "close"))
                            AcceptIfExpected(new[] { first, second }, entry[0], expected, accepted);
                }
                fixtures.Add(new JObject { ["text"] = entry[0], ["expected"] = expected, ["guarded"] = FastIntentClient.NeedsClarification(entry[0]),
                    ["accepted"] = accepted, ["request"] = FastIntentClient.CreateRequest(entry[0]) });
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/fast-llm-fixtures.json", fixtures.ToString());
            Debug.Log("Fast LLM checks: validation passed; exported " + fixtures.Count + " inference fixtures.");
        }

        private static void AcceptIfExpected(string[] plan, string order, JArray expected, JArray accepted)
        {
            var resolved = new JArray();
            if (!FastIntentClient.TryResolvePlan(plan, order, out var doors)) resolved.Add("clarify");
            else
            {
                for (int i = 0; i < plan.Length; i++)
                {
                    if (doors[i] is not QuickDoorCommand.Request door) resolved.Add(plan[i]);
                    else
                    {
                        string action = plan[i];
                        resolved.Add(door.Side == QuickDoorCommand.Direction.Facing ? action : action + "_" + door.Side.ToString().ToLowerInvariant());
                    }
                }
            }
            if (JToken.DeepEquals(resolved, expected)) accepted.Add(string.Join(",", plan));
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Fast LLM: " + message);
        }
    }
}
