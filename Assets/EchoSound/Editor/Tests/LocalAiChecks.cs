using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using EchoSound.Companion;

namespace EchoSound.Editor.Tests
{
    public static class LocalAiChecks
    {
        [MenuItem("EchoSound/Checks/Validate local AI commands")]
        public static void Validate()
        {
            QuickDoorChecks.Validate();
            FastLlmChecks.Validate();
            ConversationChecks.Validate();
            CompanionLanguageChecks.Validate();
            Check(" STOP! ", "wait_here");
            Check("follow me", "follow_curator");
            Check("come here", "go_to");
            Check("Please stay where you are", "wait_here");
            Check("Don't follow me.", "wait_here");
            Check("don't move", "wait_here");
            Check("stój", "wait_here");
            Check("stoj", "wait_here");
            Check("Proszę, zaczekaj tutaj.", "wait_here");
            Check("Nie idź za mną.", "wait_here");
            Check("idź za mną", "follow_curator");
            Check("idz za mna", "follow_curator");
            Check("Za mną!", "follow_curator");
            Check("Podejdź do mnie.", "go_to");
            Check("Wróć do mnie", "go_to");
            const QuickCompanionCommand.FollowContext protection = QuickCompanionCommand.FollowContext.Protection;
            const QuickCompanionCommand.FollowContext escortDoors = QuickCompanionCommand.FollowContext.OpenOnPath;
            CheckFollowContext("Follow me and cover me.", protection);
            CheckFollowContext("Okay, follow me and watch my back.", protection);
            CheckFollowContext("Idź za mną i osłaniaj mnie.", protection);
            CheckFollowContext("Chodź za mną. Pilnuj moich pleców.", protection);
            CheckFollowContext("Follow me. Cover me. And open all the doors on the way.", protection | escortDoors);
            CheckFollowContext("Za mną, osłaniaj mnie i otwieraj wszystkie drzwi po drodze.", protection | escortDoors);
            foreach (string text in new[] {
                "If I say \"follow me\", cover me.",
                "If I say follow me, cover me.",
                "He said: follow me. Cover me.",
                "Follow me? Cover me.",
                "Don't follow me. Cover me.",
                "Follow me. Cover me if you hear footsteps.",
                "Follow me. Cover me. Stop.",
                "Follow me. Maybe close the doors.",
                "Follow me. Cover me. Cover me.",
                "Idź za mną. Osłaniaj mnie, jeśli usłyszysz kroki.",
                "Idź za mną. Może zamknij drzwi.",
                "Follow me."
            })
                Require(!QuickCompanionCommand.TryParseFollowWithContext(text, out _), "Do not split a conditional, quoted or conflicting command: " + text);
            foreach (string text in new[] {
                "Don't follow me.",
                "Stop following me.",
                "Can you stop following me?",
                "Nie idź za mną.",
                "Nie idz za mna",
                "Przestań za mną chodzić.",
                "Echo, nie idź za mną i nie otwieraj już drzwi.",
                "Don't follow me anymore and don't open any more doors."
            })
                Require(QuickCompanionCommand.TryParseDismissal(text, out var waitAction) && waitAction == QuickCompanionCommand.DismissalAction.Wait,
                    "Stop following without operating doors: " + text);
            foreach (string text in new[] {
                "Don't follow me and close all the doors.",
                "Stop following me. Close the nearby doors.",
                "Can you stop following me and close all the doors?",
                "Nie idź za mną, a zamknij wszystkie drzwi.",
                "Przestań za mną chodzić i pozamykaj drzwi."
            })
                Require(QuickCompanionCommand.TryParseDismissal(text, out var closeAction) && closeAction == QuickCompanionCommand.DismissalAction.CloseNearbyDoors,
                    "Stop following and close nearby doors: " + text);
            foreach (string text in new[] {
                "If I say don't follow me, close the doors.",
                "I said: don't follow me.",
                "\"Don't follow me,\" the player said.",
                "Don't follow me?",
                "Don't follow me and open all the doors.",
                "Don't follow me and don't close the doors near Sam.",
                "Don't follow me and close the door on the left.",
                "Nie idź za mną i zamknij drzwi po lewej.",
                "Jeśli powiem nie idź za mną, zamknij drzwi.",
                "Don't open the doors.",
                "Nie otwieraj drzwi.",
                "Stop."
            })
                Require(!QuickCompanionCommand.TryParseDismissal(text, out _), "Do not mistake a quote, condition or unsupported clause for a dismissal: " + text);
            Require(!QuickCompanionCommand.TryParse("Idę za tobą.", out _, out _) && !QuickCompanionCommand.TryParse("I'm following you.", out _, out _),
                "A first-person statement is not a movement order.");
            Require(!QuickCompanionCommand.TryParse("„Stój”", out _, out _) && !QuickCompanionCommand.TryParse("'stop'", out _, out _),
                "A quoted stop does not stop the companion.");
            Require(QuickCompanionCommand.TrySplitFollowup("Open the door in front of me and then follow me.", out string first, out string tail)
                && first == "Open the door in front of me" && tail == "follow me.", "Explicit movement suffix is separated.");
            Require(QuickCompanionCommand.TrySplitFollowup("Otwórz drzwi przede mną, a potem chodź za mną.", out first, out tail)
                && first == "Otwórz drzwi przede mną" && tail == "chodź za mną.", "Explicit Polish movement suffix is separated.");
            foreach (string text in new[] { "If I open the door then follow me", "Open the door if I wave and then follow me", "Don't open the door then follow me",
                "Open the door then follow me?", "Open the door and then close it", "Nie otwieraj drzwi, a potem chodź za mną", "Otwórz drzwi, kiedy powiem, a potem chodź za mną" })
                Require(!QuickCompanionCommand.TrySplitFollowup(text, out _, out _), "Do not guess a conditional or unknown suffix.");
            foreach (string text in new[] { "don't stop", "nie czekaj", "nie stój", "should I stop?", "stop?", "czy mam czekać?",
                "stop and open the door", "czekaj i otwórz drzwi", "follow me then wait", "", "the word stop", "stop-go" })
                Require(!QuickCompanionCommand.TryParse(text, out _, out _), "Not a standalone command: " + text);

            var schema = new JObject
            {
                ["type"] = "object", ["required"] = new JArray("target"),
                ["properties"] = new JObject { ["target"] = new JObject { ["type"] = "string", ["enum"] = new JArray("door_1") } }
            };
            Require(CompanionBrain.ValidArguments(schema, new JObject { ["target"] = "door_1" }), "Known target accepted.");
            Require(!CompanionBrain.ValidArguments(schema, new JObject { ["target"] = "door_999" }), "Invented target rejected.");
            Require(!CompanionBrain.ValidArguments(schema, new JObject()), "Missing target rejected.");
            Require(!CompanionBrain.ValidArguments(schema, new JObject { ["target"] = 1 }), "Non-string target rejected.");
            Require(!CompanionBrain.ValidArguments(schema, new JObject { ["target"] = "door_1", ["other"] = "x" }), "Unexpected parameter rejected.");

            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Polish, new[] { "polish", "english" }) == "polish",
                "A Polish system recognizes Polish without automatic detection.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.English, null) == "english", "An English system recognizes English.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Russian, null) == "english"
                && WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.German, null) == "english",
                "Any other system language falls back to English.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.Polish, SystemLanguage.English, null) == "polish", "Explicit language overrides the system.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.Auto, SystemLanguage.English, null) == "auto", "Mixed-language mode remains available.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Polish, new[] { "english" }) == "english", "Preserve an explicit legacy language.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Polish, new[] { "russian" }) == "polish", "Ignore an unsupported legacy language.");
            Require(WhisperClient.IsSupported("polish") && WhisperClient.IsSupported("english") && !WhisperClient.IsSupported("russian")
                && !WhisperClient.IsSupported("czech") && !WhisperClient.IsSupported(null), "Auto accepts only Polish and English transcripts.");
            Require(!Enum.IsDefined(typeof(WhisperClient.SpeechLanguage), 1) && (int)WhisperClient.SpeechLanguage.Polish == 2
                && (int)WhisperClient.SpeechLanguage.English == 3 && (int)WhisperClient.SpeechLanguage.Auto == 4,
                "Saved languages keep their values; the removed Russian value falls back to the default.");

            var client = new LocalChatClient();
            JObject request = client.CreateRequest(new JArray(new JObject { ["role"] = "user", ["content"] = "hello" }), new JArray());
            Require((int)request["max_tokens"] == 192, "Replies have a token budget.");
            Require((bool)request["chat_template_kwargs"]["enable_thinking"] == false, "Thinking disabled.");
            Require(request["tools"] == null, "Empty tools omitted.");
            Require(request["response_format"]?["type"]?.ToString() == "json_schema", "Constrained plans required.");
            var plan = LocalChatClient.ReadPlan("{\"actions\":[{\"name\":\"wait_here\",\"arguments\":{}}],\"reply\":\"Waiting.\"}");
            Require((string)plan["tool_calls"]?[0]?["function"]?["name"] == "wait_here", "Plans become executable calls.");
            Require(!string.IsNullOrEmpty((string)plan["tool_calls"]?[0]?["id"]), "Calls have ids for interruption bookkeeping.");
            client.Cancel();
            VoiceChecks.ValidateOrders();
            LocalAiBuild.ValidateBundle();
            ExportFixtures();
            Debug.Log("Local AI checks: all passed.");
        }

        private static void CheckFollowContext(string text, QuickCompanionCommand.FollowContext expected)
        {
            Require(QuickCompanionCommand.TryParseFollowWithContext(text, out var context) && context == expected,
                "Recognize the separate parts of a follow command: " + text);
        }

        private static void ExportFixtures()
        {
            var tools = new JArray();
            foreach (var entry in new[] {
                new[] { "follow_curator", "Follow the curator and stay close until another order." },
                new[] { "wait_here", "Stop moving and wait at the current spot." },
                new[] { "go_to", "Walk to an object or to the curator. Come here means go_to curator.", "door_front", "door_back", "curator" },
                new[] { "open_door", "Walk to a door and open it.", "door_front", "door_back" },
                new[] { "close_door", "Walk to a door and close it.", "door_front", "door_back" } })
            {
                var parameters = new JObject { ["type"] = "object", ["properties"] = new JObject() };
                if (entry.Length > 2)
                {
                    var ids = new JArray();
                    for (int i = 2; i < entry.Length; i++) ids.Add(entry[i]);
                    parameters["properties"]["target"] = new JObject { ["type"] = "string", ["enum"] = ids };
                    parameters["required"] = new JArray("target");
                }
                tools.Add(new JObject { ["type"] = "function", ["function"] = new JObject { ["name"] = entry[0], ["description"] = entry[1], ["parameters"] = parameters } });
            }
            var fixtures = new JArray();
            foreach (var entry in new[] {
                new[] { "Open the door in front of me.", "open_door", "door_front" },
                new[] { "Zamknij drzwi za mną.", "close_door", "door_back" },
                new[] { "Otwórz drzwi przede mną.", "open_door", "door_front" },
                new[] { "Please stay where you are.", "wait_here", "" },
                new[] { "Podejdź do mnie.", "go_to", "curator" },
                new[] { "Is the door behind me open?", "", "" },
                new[] { "Don't open the door. Wait here.", "wait_here", "" },
                new[] { "Open the door in front of me and then follow me.", "open_door,follow_curator", "door_front" } })
            {
                bool split = QuickCompanionCommand.TrySplitFollowup(entry[0], out string initial, out string followup);
                var messages = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = CompanionBrain.DefaultPrompt +
                        "\nCurrent perception: You: waiting. Curator: 2 m from you. Objects you perceive:\n" +
                        "- door_front: door, closed. Relative to the curator: in front, 3 m.\n" +
                        "- door_back: door, open. Relative to the curator: behind, 4 m." },
                    new JObject { ["role"] = "user", ["content"] = split ? initial : entry[0] }
                };
                QuickCompanionCommand.TryParse(entry[0], out string quick, out _);
                QuickCompanionCommand.TryParse(followup, out string after, out _);
                fixtures.Add(new JObject { ["phrase"] = entry[0], ["expected"] = entry[1], ["target"] = entry[2], ["quick_action"] = quick, ["quick_followup"] = after,
                    ["request"] = new LocalChatClient().CreateRequest(messages, tools) });
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/ai-fixtures.json", fixtures.ToString());
        }

        private static void Check(string text, string expected)
        {
            Require(QuickCompanionCommand.TryParse(text, out string action, out string reply) && action == expected && !string.IsNullOrEmpty(reply), text);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Local AI check: " + message);
        }
    }
}