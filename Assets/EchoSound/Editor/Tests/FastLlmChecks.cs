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
            Require(FastIntentClient.ReadPlan("[\"open_left\",\"follow\"]").Length == 2, "Two-action plan.");
            foreach (string invalid in new[] { "[]", "[\"teleport\"]", "[\"follow\",\"follow\"]", "[\"clarify\",\"open\"]", "[1]", "[\"open\",\"close\",\"stop\"]" })
            {
                bool rejected = false;
                try { FastIntentClient.ReadPlan(invalid); } catch (JsonException) { rejected = true; }
                Require(rejected, "Reject invalid plan " + invalid);
            }
            Require(FastIntentClient.TryDoorRequest("close_front", "Закрой за мной дверь пожалуйста", out var door) && door.Side == QuickDoorCommand.Direction.Back, "Explicit direction overrides model guess.");
            Require(FastIntentClient.TryDoorRequest("open", "Открой дверь и следуй за мной", out door) && door.Side == QuickDoorCommand.Direction.Facing, "Follow clause is not a door direction.");
            Require(!FastIntentClient.TryDoorRequest("open", "Открой дверь слева или справа", out _), "Ambiguous directions must clarify.");

            var fixtures = new JArray();
            foreach (var entry in new[] {
                new[] { "Побудь здесь пока я осмотрюсь", "stop" },
                new[] { "Держись рядом со мной", "follow" },
                new[] { "Подойди поближе ко мне", "come" },
                new[] { "Можешь открыть дверь слева от меня?", "open_left" },
                new[] { "Закрой за мной дверь пожалуйста", "close_back" },
                new[] { "Распахни дверь впереди", "open_front" },
                new[] { "Прикрой дверь справа", "close_right" },
                new[] { "Не открывай дверь, жди здесь", "clarify" },
                new[] { "Не закрывай дверь", "clarify" },
                new[] { "Дверь передо мной закрыта?", "clarify" },
                new[] { "Если я скажу, открой дверь", "clarify" },
                new[] { "Я сейчас открою дверь", "clarify" },
                new[] { "Открой дверь впереди и потом иди за мной", "open_front", "follow" },
                new[] { "Could you stay where you are?", "stop" },
                new[] { "Please stick with me", "follow" },
                new[] { "Come closer please", "come" },
                new[] { "Can you open the door on my right?", "open_right" },
                new[] { "Is the door behind me open?", "clarify" },
                new[] { "Poczekaj tutaj aż wrócę", "stop" },
                new[] { "Trzymaj się blisko mnie", "follow" },
                new[] { "Podejdź bliżej do mnie", "come" },
                new[] { "Otwórz drzwi po lewej stronie", "open_left" },
                new[] { "Nie otwieraj drzwi", "clarify" },
                new[] { "Привет как дела", "clarify" },
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
                new[] { "Куда ведёт эта дверь?", "clarify" },
                new[] { "Открой все двери вокруг", "clarify" },
                new[] { "Wait right there for me", "stop" },
                new[] { "Walk over to me", "come" },
                new[] { "Stay close and follow my lead", "follow" },
                new[] { "Zaczekaj w tym miejscu", "stop" },
                new[] { "Wróć do mnie", "come" }
            })
            {
                var expected = new JArray();
                for (int i = 1; i < entry.Length; i++) expected.Add(entry[i]);
                var resolved = new JObject();
                foreach (string code in FastIntentClient.Codes)
                {
                    string final = code;
                    if (code.StartsWith("open") || code.StartsWith("close"))
                    {
                        if (!FastIntentClient.TryDoorRequest(code, entry[0], out var request)) final = "clarify";
                        else final = (code.StartsWith("open") ? "open" : "close") + (request.Side == QuickDoorCommand.Direction.Facing ? "" : "_" + request.Side.ToString().ToLowerInvariant());
                    }
                    resolved[code] = final;
                }
                fixtures.Add(new JObject { ["text"] = entry[0], ["expected"] = expected, ["guarded"] = FastIntentClient.NeedsClarification(entry[0]),
                    ["resolved"] = resolved, ["request"] = FastIntentClient.CreateRequest(entry[0]) });
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/fast-llm-fixtures.json", fixtures.ToString());
            Debug.Log("Fast LLM checks: validation passed; exported " + fixtures.Count + " inference fixtures.");
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Fast LLM: " + message);
        }
    }
}
