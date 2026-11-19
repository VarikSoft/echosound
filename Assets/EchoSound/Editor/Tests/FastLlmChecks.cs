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
            var social = FastIntentClient.ReadTurn("{\"reply\":\"Cześć. Jestem w pobliżu.\"}");
            Require(social.Actions == null && social.Reply == "Cześć. Jestem w pobliżu.", "A social answer carries no actions.");
            foreach (string invalid in new[] { "[]", "[\"teleport\"]", "[\"open_left\"]", "[\"follow\",\"follow\"]", "[\"clarify\",\"open\"]", "[1]", "[\"open\",\"close\",\"stop\"]" })
            {
                bool rejected = false;
                try { FastIntentClient.ReadPlan(invalid); } catch (JsonException) { rejected = true; }
                Require(rejected, "Reject invalid plan " + invalid);
            }
            Require(FastIntentClient.TryDoorRequest("close", "Zamknij za mną drzwi, proszę", out var door) && door.Side == QuickDoorCommand.Direction.Back, "Resolve explicit direction from the transcript.");
            Require(FastIntentClient.TryDoorRequest("close", "Shut the door behind us please", out door) && door.Side == QuickDoorCommand.Direction.Back, "Resolve an English direction.");
            Require(FastIntentClient.TryDoorRequest("open", "Otwórz drzwi i chodź za mną", out door) && door.Side == QuickDoorCommand.Direction.Facing, "Follow clause is not a door direction.");
            Require(FastIntentClient.TryDoorRequest("open", "Open the door and follow me", out door) && door.Side == QuickDoorCommand.Direction.Facing, "English follow clause is not a door direction.");
            Require(FastIntentClient.TryDoorRequest("open", "Uchyl lewe drzwi", out door) && door.Side == QuickDoorCommand.Direction.Left, "A side named before the door counts.");
            Require(!FastIntentClient.TryDoorRequest("open", "Open the door on the left or on the right", out _), "Ambiguous directions must clarify.");
            Require(!FastIntentClient.TryDoorRequest("open", "Otwórz drzwi po lewiej", out _),
                "An unknown direction from speech cannot silently select another door.");
            const string twoDoors = "Uchyl drzwi po lewej, a potem przymknij drzwi po prawej";
            Require(FastIntentClient.TryResolvePlan(new[] { "open", "close" }, twoDoors, out var doors)
                && doors[0]?.Side == QuickDoorCommand.Direction.Left && doors[1]?.Side == QuickDoorCommand.Direction.Right,
                "Each step must resolve the direction from its own clause.");
            Require(!FastIntentClient.TryResolvePlan(new[] { "open" }, twoDoors, out _)
                && !FastIntentClient.TryResolvePlan(new[] { "close", "open" }, twoDoors, out _)
                && !FastIntentClient.TryResolvePlan(new[] { "open", "follow" }, twoDoors, out _),
                "An incomplete or reordered sequence must clarify before starting.");
            Require(!FastIntentClient.TryResolvePlan(new[] { "open" }, "Podnieś klucz", out _),
                "A plan cannot introduce a door absent from the request.");
            Require(FastIntentClient.CanPlan("Czy możesz otworzyć drzwi po mojej lewej?") && FastIntentClient.CanPlan("Proszę zaczekać tutaj"),
                "A polite Polish request uses the infinitive.");
            Require(!FastIntentClient.CanPlan("Otworzyć drzwi") && !FastIntentClient.CanPlan("Zamknąć drzwi?"),
                "A bare infinitive is not an order without a polite request.");
            foreach (string text in new[] { "Do zobaczenia!", "Pójdę za tobą", "Mam kota o imieniu Plusz", "Czy idziesz za mną?",
                "„Stój”", "Stój — powiedział.", "Powiedz, jak otworzyć drzwi", "Możesz opowiedzieć krótki żart?", "Otworzę drzwi",
                "I'll follow you", "\"Stop\"", "He said: stop.", "Are you following me?", "Don't worry about the door" })
                Require(!FastIntentClient.CanPlan(text) && !FastIntentClient.TryResolvePlan(new[] { "stop" }, text, out _),
                    "Conversation cannot authorize movement: " + text);
            Require(!FastIntentClient.TryResolvePlan(new[] { "come" }, "Zostań tutaj", out _),
                "The model cannot substitute another action for an approved command.");
            Require(!FastIntentClient.NeedsClarification("Don't worry about the door"), "A negation that is not an order stays conversation.");
            Require(!FastIntentClient.NeedsClarification("Weź otwórz drzwi z przodu") && FastIntentClient.CanPlan("Weź otwórz drzwi z przodu")
                && QuickDoorCommand.TryParse("Weź otwórz drzwi z przodu", out door) && door.Side == QuickDoorCommand.Direction.Front,
                "Colloquial \"weź\" softens an order; it is not an unsupported action.");

            var fixtures = new JArray();
            foreach (var entry in new[] {
                new[] { "Poczekaj tutaj, aż się rozejrzę", "stop" },
                new[] { "Trzymaj się blisko mnie", "follow" },
                new[] { "Podejdź bliżej do mnie", "come" },
                new[] { "Czy możesz otworzyć drzwi po mojej lewej?", "open_left" },
                new[] { "Zamknij za mną drzwi, proszę", "close_back" },
                new[] { "Uchyl drzwi z przodu", "open_front" },
                new[] { "Otwórz drzwi po lewiej", "clarify" },
                new[] { "Proszę, uchyl drzwi z przodu.", "open_front" },
                new[] { "Przymknij drzwi po prawej", "close_right" },
                new[] { "Nie otwieraj drzwi, zaczekaj tu", "clarify" },
                new[] { "Nie zamykaj drzwi", "clarify" },
                new[] { "Czy drzwi przede mną są zamknięte?", "reply" },
                new[] { "Jeśli powiem, otwórz drzwi", "clarify" },
                new[] { "Zaraz otworzę drzwi", "reply" },
                new[] { "Uchyl drzwi z przodu, a potem chodź za mną", "open_front", "follow" },
                new[] { "Could you stay where you are?", "stop" },
                new[] { "Please stick with me", "follow" },
                new[] { "Come closer please", "come" },
                new[] { "Can you open the door on my right?", "open_right" },
                new[] { "Is the door behind me open?", "reply" },
                new[] { "Poczekaj tutaj aż wrócę", "stop" },
                new[] { "Otwórz drzwi po lewej stronie", "open_left" },
                new[] { "Nie otwieraj drzwi", "clarify" },
                new[] { "Cześć, jak tam?", "reply" },
                new[] { "Martwię się, że się zgubimy.", "reply" },
                new[] { "Jaki masz nastrój?", "reply" },
                new[] { "Thanks, glad you're here.", "reply" },
                new[] { "Miło cię widzieć, trochę się boję.", "reply" },
                new[] { "Podnieś klucz przy ścianie", "clarify" },
                // Held-out wording, not examples in the model prompt.
                new[] { "Zostań w tym miejscu", "stop" },
                new[] { "Zaczekaj na mnie tutaj", "stop" },
                new[] { "Podążaj za mną", "follow" },
                new[] { "Wracaj do mnie", "come" },
                new[] { "Proszę, uchyl najbliższe drzwi", "open_nearest" },
                new[] { "Przymknij drzwi za mną", "close_back" },
                new[] { "Przytrzymaj drzwi otwarte", "clarify" },
                new[] { "Rozwal te drzwi", "clarify" },
                new[] { "Przynieś apteczkę", "clarify" },
                new[] { "Dokąd prowadzą te drzwi?", "reply" },
                new[] { "Wait right there for me", "stop" },
                new[] { "Walk over to me", "come" },
                new[] { "Stay close and follow my lead", "follow" },
                new[] { "Zaczekaj w tym miejscu", "stop" },
                new[] { "Wróć do mnie", "come" },
                new[] { "Cześć, chodź za mną.", "follow" },
                new[] { "Czy możesz podejść do mnie?", "come" },
                new[] { "Możesz poczekać tutaj?", "stop" },
                new[] { "Haha, niezłe.", "reply" },
                new[] { "Możesz opowiedzieć krótki żart?", "reply" },
                new[] { "Mam kota o imieniu Plusz.", "reply" },
                new[] { twoDoors, "open_left", "close_right" },
                new[] { "Podejdź do mnie, a potem uchyl drzwi po prawej", "come", "open_right" },
                new[] { "Uchyl drzwi po prawej, potem otwórz drzwi po lewej", "open_right", "open_left" }
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
                // Every order fixture must be reachable: some plan from the model has to pass game validation.
                bool order = expected[0].ToString() != "reply" && expected[0].ToString() != "clarify";
                Require(!order || accepted.Count > 0, "No model plan can satisfy the order fixture: " + entry[0]);
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
