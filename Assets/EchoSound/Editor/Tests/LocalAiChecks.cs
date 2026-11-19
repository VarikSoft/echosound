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
            Check(" STOP! ", "wait_here");
            Check("follow me", "follow_curator");
            Check("come here", "go_to");
            Check("stój", "wait_here");
            Check("idź za mną", "follow_curator");
            Check("Podejdź do mnie.", "go_to");
            Check("стой", "wait_here");
            Check("иди за мной", "follow_curator");
            const QuickCompanionCommand.FollowContext protection = QuickCompanionCommand.FollowContext.Protection;
            const QuickCompanionCommand.FollowContext unclearDoor = QuickCompanionCommand.FollowContext.ClarifyDoors;
            const QuickCompanionCommand.FollowContext escortDoors = QuickCompanionCommand.FollowContext.OpenOnPath;
            CheckFollowContext("Иди за мной. Короче, следи, чтобы враги меня не убили. Может, двери открываем меня. Не знаю.", protection | unclearDoor);
            CheckFollowContext("Следуй за мной и прикрой меня. Может, откроем дверь?", protection | unclearDoor);
            CheckFollowContext("Иди за мной, может, закроем двери.", unclearDoor);
            CheckFollowContext("Иди за мной. Прикрой меня. И открывай все двери по дороге.", protection | escortDoors);
            CheckFollowContext("Иди за мной. Открой все двери. Может, закроем двери?", unclearDoor);
            foreach (string text in new[] {
                "Если я скажу «иди за мной», прикрой меня.",
                "Он сказал: иди за мной. Прикрой меня.",
                "Иди за мной? Прикрой меня.",
                "Не иди за мной. Прикрой меня.",
                "Иди за мной. Прикрой меня, если услышишь шаги.",
                "Иди за мной. Прикрой меня. Стой.",
                "Иди за мной. Может, стой за дверью.",
                "Иди за мной. Может, не открывай двери, пока я осмотрюсь."
            })
                Require(!QuickCompanionCommand.TryParseFollowWithContext(text, out _), "Do not split a conditional, quoted or conflicting command: " + text);
            foreach (string text in new[] {
                "Не иди за мной.",
                "Перестань преследовать меня.",
                "Пана, не иди за мной и не открываю больше две.",
                "Эхо, не ходи больше за мной и не открывай двери."
            })
                Require(QuickCompanionCommand.TryParseDismissal(text, out var waitAction) && waitAction == QuickCompanionCommand.DismissalAction.Wait,
                    "Stop following without operating doors: " + text);
            foreach (string text in new[] {
                "А еще этот. Можешь не ходить больше не преследовать меня и где-то гулять и закрывать двери.",
                "Можешь больше не ходить за мной, а пройтись и закрыть двери?",
                "Не следуй за мной. Закрой ближайшие двери."
            })
                Require(QuickCompanionCommand.TryParseDismissal(text, out var closeAction) && closeAction == QuickCompanionCommand.DismissalAction.CloseNearbyDoors,
                    "Stop following and close nearby doors: " + text);
            Require(QuickCompanionCommand.TryParseDismissal("Можешь больше не ходить за мной, а пройти связы к крык двери.", out var unclear)
                && unclear == QuickCompanionCommand.DismissalAction.ClarifyDoors, "Stop safely even if the door clause is garbled.");
            foreach (string text in new[] {
                "Если скажу не иди за мной, закрой двери.",
                "Я сказал: не иди за мной.",
                "«Не иди за мной», сказал игрок.",
                "Не иди за мной?",
                "Не иди за мной и открывай все двери.",
                "Не иди за мной и не закрывай двери рядом с Сашей.",
                "Не иди за мной и закрой двери слева.",
                "Можешь не ходить за мной, а пройти если я открою двери?",
                "Не открываю больше две."
            })
                Require(!QuickCompanionCommand.TryParseDismissal(text, out _), "Do not mistake a quote, condition or unsupported clause for a dismissal: " + text);
            Require(!QuickCompanionCommand.TryParse("Следую за мной.", out _, out _), "A first-person transcript is not a movement order.");
            Require(!QuickCompanionCommand.TryParse("«Стой»", out _, out _), "A quoted stop does not stop the companion.");
            Check("ко мне", "go_to");
            Check("Можешь подождать здесь, пока я немного осмотрюсь и попробую разобраться, куда нам дальше идти?", "wait_here");
            foreach (string text in new[] { "Можешь подождать здесь, пока я осмотрюсь, и открой дверь", "Можешь подождать здесь, пока я посмотрю, а ты иди за мной", "Если можешь подождать здесь, пока я осмотрюсь" })
                Require(!QuickCompanionCommand.TryParse(text, out _, out _), "A wait explanation cannot conceal another order or condition.");
            Require(QuickCompanionCommand.TrySplitFollowup("Open the door in front of me and then follow me.", out string first, out string tail)
                && first == "Open the door in front of me" && tail == "follow me.", "Explicit movement suffix is separated.");
            foreach (string text in new[] { "If I open the door then follow me", "Open the door if I wave and then follow me", "Don't open the door then follow me", "Open the door then follow me?", "Open the door and then close it" })
                Require(!QuickCompanionCommand.TrySplitFollowup(text, out _, out _), "Do not guess a conditional or unknown suffix.");
            foreach (string text in new[] { "don't stop", "не стой", "nie czekaj", "should I stop?", "stop?",
                "stop and open the door", "follow me then wait", "", "the word stop", "stop-go", "не следую за мной", "ни иди за мной", "следую за мной?" })
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

            var client = new LocalChatClient();
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Russian, new[] { "polish", "english", "russian" }) == "russian", "Short Russian orders should not trigger automatic detection.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.Polish, SystemLanguage.Russian, null) == "polish", "Explicit language overrides the system.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.Auto, SystemLanguage.Russian, null) == "auto", "Mixed-language mode remains available.");
            Require(WhisperClient.ResolveLanguage(WhisperClient.SpeechLanguage.System, SystemLanguage.Russian, new[] { "english" }) == "english", "Preserve an explicit legacy language.");
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
                new[] { "Открой дверь передо мной.", "open_door", "door_front" },
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
