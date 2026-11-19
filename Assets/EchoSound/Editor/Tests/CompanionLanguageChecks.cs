using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using EchoSound.Companion;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// English and Polish coverage: language detection, phrase table parity, and the routes that the
    /// Windows-only built-player tests expect, so a routing change is caught on any machine.
    /// </summary>
    public static class CompanionLanguageChecks
    {
        // Phrase lists that only Polish needs: infinitives after a polite request and distributive verbs.
        private static readonly string[] PolishOnly = { "RequestedStop", "RequestedFollow", "RequestedCome", "RequestedOpen", "RequestedClose", "OpenEach", "CloseEach" };

        public static void Validate()
        {
            CompanionLanguage.Language before = CompanionLanguage.Preferred;
            try
            {
                CheckDetection();
                CheckPhrasebooks();
                CheckRoutes();
            }
            finally { CompanionLanguage.Preferred = before; }
            Debug.Log("Companion language checks: all passed (English and Polish detection, phrase parity, built-player routes).");
        }

        private static void CheckDetection()
        {
            foreach (string polish in new[] { "Otwórz drzwi", "otworz drzwi po lewej", "zamknij drzwi za mna", "czekaj", "Dzieki, jestes super", "Mam na imię Marek" })
                Require(CompanionLanguage.Detect(polish) == CompanionLanguage.Language.Polish, "Polish detected: " + polish);
            foreach (string english in new[] { "Open the door", "follow me", "thanks", "What's my name?", "Can you wait here?" })
                Require(CompanionLanguage.Detect(english) == CompanionLanguage.Language.English, "English detected: " + english);
            CompanionLanguage.Preferred = CompanionLanguage.Language.Polish;
            Require(CompanionLanguage.Detect("stop") == CompanionLanguage.Language.Polish && CompanionLanguage.Pick("Plusz", "en", "pl") == "pl",
                "Words without a language follow the speech setting.");
            CompanionLanguage.Preferred = CompanionLanguage.Language.English;
            Require(CompanionLanguage.Detect("stop") == CompanionLanguage.Language.English, "The speech setting breaks ties both ways.");
            Require(CompanionLanguage.Fold("Zażółć gęślą jaźń") == "zazolc gesla jazn", "Folding removes every Polish diacritic.");
            Require(CompanionLanguage.Simplify(" Don't, follow ME! ") == "don't follow me", "Simplify keeps contractions and drops punctuation.");
            foreach (string quoted in new[] { "\"stop\"", "„Stój”", "'stop'", "«stop»", "‘stop’" })
                Require(CompanionLanguage.HasQuote(quoted), "Quote recognized: " + quoted);
            foreach (string plain in new[] { "don't move", "it's fine", "stop" })
                Require(!CompanionLanguage.HasQuote(plain), "A contraction is not a quote: " + plain);
            Require(CompanionLanguage.FromSpeech("polish") == CompanionLanguage.Language.Polish && CompanionLanguage.FromSpeech("english") == CompanionLanguage.Language.English
                && CompanionLanguage.FromSpeech("auto") == null && CompanionLanguage.FromSpeech("russian") == null, "Only Polish and English speech set the reply language.");
        }

        private static void CheckPhrasebooks()
        {
            var cyrillic = new Regex(@"[Ѐ-ӿ]");
            foreach (var field in typeof(CompanionPhrases.Phrasebook).GetFields().Where(field => field.FieldType == typeof(string[])))
            {
                foreach (var book in CompanionPhrases.Books)
                {
                    var phrases = (string[])field.GetValue(book);
                    Require(phrases != null, $"{book.Language} {field.Name} is defined.");
                    bool optional = book.Language == CompanionLanguage.Language.English && PolishOnly.Contains(field.Name);
                    Require(optional || phrases.Length > 0, $"{book.Language} has phrases for {field.Name}.");
                    foreach (string phrase in phrases)
                    {
                        Require(phrase == phrase.ToLowerInvariant().Trim() && phrase.Length > 0, $"Phrase is lower case and trimmed: {field.Name} '{phrase}'.");
                        Require(!cyrillic.IsMatch(phrase), $"Only English and Polish phrases: {field.Name} '{phrase}'.");
                    }
                }
            }
            // Where one noun means "door" and "doors" (Polish "drzwi"), a bulk scope word must never also be a single
            // door's side, or "otwórz drzwi z przodu" would open every door. English plural "doors" keeps them apart.
            foreach (var book in CompanionPhrases.Books.Where(book => book.Doors.Intersect(book.Door).Any()))
                foreach (string scope in book.DoorScope)
                    Require(!book.Front.Concat(book.Back).Concat(book.Left).Concat(book.Right).Contains(scope), "Scope is not a side: " + scope);
            Require(!QuickDoorCommand.TryParseOpenAll("Open the door ahead.", out _) && QuickDoorCommand.TryParseOpenAll("Open all the doors ahead.", out _),
                "English \"ahead\" is a bulk scope only with plural doors.");
            Require(!Regex.IsMatch(FastIntentClient.Prompt, @"[Ѐ-ӿ]"), "The planner prompt has no Russian examples.");
            foreach (string heldOut in new[] { "Zostań w tym miejscu", "Zaczekaj na mnie tutaj", "Podążaj za mną", "Wracaj do mnie", "Zaczekaj w tym miejscu", "Wróć do mnie", "Walk over to me" })
                Require(!FastIntentClient.Prompt.Contains(heldOut), "Held-out wording stays out of the prompt: " + heldOut);
        }

        // Mirrors the order of checks in CompanionBrain.Command, which needs a scene to run.
        private static string Route(string order)
        {
            if (QuickCompanionCommand.TryParseDismissal(order, out var dismissal)) return "dismiss-" + dismissal;
            if (QuickDoorCommand.TryParseStatus(order, out var side)) return "status-" + side;
            if (CompanionBrain.ClassifySmallTalk(order) != CompanionBrain.SmallTalk.None || new CompanionDialogue().TryReply(order, out _)) return "game-chat";
            if (QuickDoorCommand.TryParseOpenAll(order, out bool escort)) return escort ? "escort" : "open-all";
            if (QuickCompanionCommand.TryParseFollowWithContext(order, out var context)) return "follow-" + context;
            if (QuickCompanionCommand.TrySplitFollowup(order, out string first, out _) && QuickDoorCommand.TryParse(first, out _)) return "door-then-move";
            if (QuickCompanionCommand.TryParse(order, out string action, out _)) return action;
            if (QuickDoorCommand.TryParse(order, out var door)) return "door-" + door.Action + "-" + door.Side;
            if (FastIntentClient.NeedsClarification(order)) return "clarify";
            return FastIntentClient.CanPlan(order) ? "planner" : "chat";
        }

        private static void CheckRoutes()
        {
            foreach (var (order, expected) in new[]
            {
                // OfflineAiSmokeTest
                ("follow me", "follow_curator"), ("stój", "wait_here"), ("Podejdź do mnie.", "go_to"), ("stop", "wait_here"),
                ("Otwórz drzwi dopiero, kiedy pozwolę", "clarify"),
                ("Trzymaj się blisko mnie", "planner"), ("Poczekaj tutaj, aż się rozejrzę", "planner"), ("Stick with me for a while", "planner"),
                ("Nie zamykaj drzwi", "clarify"), ("za mną", "follow_curator"), ("Trochę się boję", "game-chat"),
                ("Mam na imię Łucja.", "game-chat"), ("Mam kota o imieniu Plusz.", "game-chat"), ("Do zobaczenia!", "game-chat"),
                ("Jak mam na imię?", "game-chat"), ("What's my cat's name?", "game-chat"), ("Czy idziesz za mną?", "game-chat"),
                ("„Stój”", "chat"), ("What are you doing?", "game-chat"), ("Please remain at your current position until I return.", "planner"),
                ("Czy drzwi z przodu są zamknięte?", "status-Front"), ("Is the door ahead open?", "status-Front"),
                ("Otwórz drzwi.", "door-Open-Facing"), ("Uchyl drzwi z przodu", "planner"),
                ("Zamknij drzwi przede mną.", "door-Close-Front"), ("Przymknij drzwi przede mną", "planner"),
                ("Zamknij drzwi przede mną, a potem za mną", "door-then-move"), ("Open the door in front of me and then follow me", "door-then-move"),
                ("Przymknij drzwi z przodu, a potem trzymaj się blisko mnie", "planner"), ("Uchyl drzwi z przodu, a potem trzymaj się blisko mnie", "planner"),
                ("Idź za mną i otwórz wszystkie drzwi", "escort"), ("Follow me. Open all the doors on the way.", "escort"),
                ("Chodź za mną. Otwieraj drzwi po drodze.", "escort"), ("Idź za mną. Osłaniaj mnie. I otwieraj wszystkie drzwi po drodze.", "follow-Protection, OpenOnPath"),
                ("Otwórz wszystkie drzwi", "open-all"), ("Idź za mną. Otwórz wszystkie drzwi.", "escort"),
                ("Echo, nie idź za mną i nie otwieraj już drzwi.", "dismiss-Wait"), ("Don't follow me anymore and close all the doors.", "dismiss-CloseNearbyDoors"),
                ("Przestań za mną chodzić, a potem pozamykaj drzwi.", "dismiss-CloseNearbyDoors"),
                ("Follow me.", "follow_curator"), ("Close the door in front of me.", "door-Close-Front"),
                // Tools/Test-Companion.py
                ("Zaczekaj tutaj", "wait_here"), ("Idź za mną", "follow_curator"), ("Cześć, chodź za mną.", "follow_curator"),
                ("Czy możesz podejść do mnie?", "planner"), ("Zostań w tym miejscu", "planner"), ("Come closer please", "go_to"),
                ("Otworzę drzwi", "game-chat"), ("I'll open the door", "game-chat"), ("Repeat after me: stop.", "chat"), ("He said: follow me.", "chat"),
                ("Don't close the door.", "clarify"), ("Nie otwieraj drzwi po prawej.", "clarify"), ("Jeśli powiem, otwórz drzwi.", "clarify"),
                ("Wyważ te drzwi", "clarify"), ("Przynieś apteczkę", "clarify"), ("Otwórz drzwi?", "chat"),
                ("Otwórz drzwi z przodu.", "door-Open-Front"), ("Otwórz drzwi po lewej.", "door-Open-Left"), ("Open the door on my right.", "door-Open-Right"),
                ("Zamknij drzwi po lewej.", "door-Close-Left"), ("Close the door on my right.", "door-Close-Right"),
                ("Uchyl drzwi po lewej, a potem przymknij drzwi po prawej", "planner"),
                ("Follow me and cover me.", "follow-Protection"), ("Idź za mną i osłaniaj mnie.", "follow-Protection"),
                ("Tell me a joke.", "game-chat"), ("Opowiedz mi żart.", "game-chat"), ("Dlaczego?", "chat"),
                ("Jaki film wybrałbyś na wieczór?", "chat"), ("What would you name a robot?", "chat"),
            })
                Require(Route(order) == expected, $"Route of \"{order}\" is {Route(order)}, expected {expected}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Companion language check: " + message);
        }
    }
}
