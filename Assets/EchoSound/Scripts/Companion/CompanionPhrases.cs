using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EchoSound.Companion
{
    /// <summary>
    /// The English and Polish phrases that game code acts on without the language model, kept in one place
    /// so both languages get the same coverage. Phrases are lower case and matching folds Polish diacritics,
    /// so "otworz" matches "otwórz". Wording that is not listed goes to the language model, which only selects
    /// from a fixed command set, or to a clarification. Add general phrasings, never one recording's mistakes.
    /// </summary>
    public static class CompanionPhrases
    {
        public sealed class Phrasebook
        {
            public CompanionLanguage.Language Language;

            // Complete orders, matched as the whole utterance.
            public string[] Stop, Follow, Come;
            // Imperative openings of longer orders that the planner may interpret.
            public string[] StopOpenings, FollowOpenings, ComeOpenings, OpenOpenings, CloseOpenings;
            // Openings accepted only after a polite request; Polish uses the infinitive there ("czy możesz otworzyć").
            public string[] RequestedStop, RequestedFollow, RequestedCome, RequestedOpen, RequestedClose;

            // A single door: verbs of complete orders, nouns and the words that pick the door.
            public string[] Open, Close, Door, DoorMention, Determiner, Nearest;
            public string[] Front, Back, Left, Right, LeftAdjective, RightAdjective;
            public string[] DoorQuestion, DoorIsOpen, DoorIsClosed;

            // Several doors at once: "open all the doors on the way". Doors is plural; a scope word alone
            // makes a bulk order only with plural doors, so "open the door ahead" stays a single door.
            public string[] OpenAll, CloseAll, Quantifier, BulkAdjective, Doors, DoorScope, OpenEach, CloseEach;

            // Clauses of the compound orders that game code accepts, such as "don't follow me and close all the doors".
            public string[] StopFollowing, DontOpenDoors, Protect;

            // Politeness and sentence structure.
            public string[] Please, PoliteRequest, Greeting, LeadIn, Then, And;

            // Words that make a phrase something other than a plain order to execute. A negation directly before
            // an order verb ("don't open") is refused; other negations ("don't worry") stay conversation.
            public string[] Negation, OrderVerbs, Condition, Reported, Unsupported, HoldDoor;
        }

        public static readonly Phrasebook English = new Phrasebook
        {
            Language = CompanionLanguage.Language.English,
            Stop = new[] { "stop", "wait", "wait here", "wait there", "wait for me", "wait for me here", "stay", "stay here", "stay there",
                "stay put", "stay where you are", "stay right there", "stay right here", "hold on", "hold it", "hold position", "halt",
                "freeze", "stand still", "don't move", "stop following me", "don't follow me", "do not follow me", "you can stop following me",
                "stay where you are please" },
            Follow = new[] { "follow", "follow me", "follow my lead", "come with me", "come along", "stay close", "stay with me", "stick with me",
                "stick close", "keep up", "keep close", "keep following me", "with me", "on me", "after me" },
            Come = new[] { "come here", "come to me", "come back", "come back to me", "come closer", "come over here", "get over here",
                "over here", "return to me", "walk over to me" },
            StopOpenings = new[] { "stop", "wait", "hold on", "hold position", "halt", "stand still", "stay here", "stay there", "stay put",
                "stay where you are", "stay right there", "stay right here", "remain here", "remain there", "remain where you are",
                "remain at your current position", "remain at your position" },
            FollowOpenings = new[] { "follow me", "follow my lead", "follow along", "come with me", "come along", "stay close", "stay with me",
                "stick with me", "stick close", "keep up", "keep close", "keep following" },
            ComeOpenings = new[] { "come here", "come closer", "come to me", "come back", "come over", "get over here", "get back here",
                "return to me", "walk over to me" },
            OpenOpenings = new[] { "open", "open up" },
            CloseOpenings = new[] { "close", "shut", "slam" },
            RequestedStop = new string[0], RequestedFollow = new string[0], RequestedCome = new string[0],
            RequestedOpen = new string[0], RequestedClose = new string[0],

            Open = new[] { "open" },
            Close = new[] { "close", "shut" },
            Door = new[] { "door" },
            DoorMention = new[] { "door", "doors", "doorway" },
            Determiner = new[] { "the", "this", "that" },
            Nearest = new[] { "nearest", "closest" },
            Front = new[] { "in front of me", "in front of us", "in front", "ahead", "ahead of me", "ahead of us" },
            Back = new[] { "behind me", "behind us", "at my back" },
            Left = new[] { "on my left", "on the left", "to my left", "to the left" },
            Right = new[] { "on my right", "on the right", "to my right", "to the right" },
            LeftAdjective = new[] { "left" },
            RightAdjective = new[] { "right" },
            DoorQuestion = new[] { "is" },
            DoorIsOpen = new[] { "open" },
            DoorIsClosed = new[] { "closed", "shut" },

            OpenAll = new[] { "open", "open up", "keep opening" },
            CloseAll = new[] { "close", "shut", "keep closing" },
            Quantifier = new[] { "all the", "all of the", "all", "every", "each" },
            BulkAdjective = new[] { "nearby", "open", "other" },
            Doors = new[] { "doors" },
            DoorScope = new[] { "on the way", "along the way", "on our way", "as we go", "ahead", "nearby", "around", "around here", "around us" },
            OpenEach = new string[0],
            CloseEach = new string[0],

            StopFollowing = new[] { "don't follow me", "do not follow me", "don't follow me anymore", "don't follow me any more",
                "stop following me", "stop following me now", "you can stop following me", "no need to follow me", "quit following me" },
            DontOpenDoors = new[] { "don't open doors", "don't open the doors", "don't open any doors", "don't open any more doors",
                "do not open the doors", "do not open any doors", "stop opening doors", "stop opening the doors", "no more doors" },
            Protect = new[] { "cover me", "protect me", "guard me", "watch my back", "keep me safe", "keep watch", "keep an eye out",
                "watch out for enemies", "look out for enemies", "watch for enemies" },

            Please = new[] { "please", "pls" },
            PoliteRequest = new[] { "can you", "could you", "would you", "will you" },
            Greeting = new[] { "hey", "hi", "hello", "echo", "okay", "ok" },
            LeadIn = new[] { "echo", "hey", "hi", "listen", "okay", "ok", "so", "partner", "buddy", "please" },
            Then = new[] { "and then", "then", "after that", "and after that" },
            And = new[] { "and then", "and also", "and", "then", "also" },

            Negation = new[] { "not", "don't", "dont", "do not", "never" },
            OrderVerbs = new[] { "open", "close", "shut", "follow", "leave", "wait", "stop", "stay", "come", "go", "move" },
            Condition = new[] { "if", "when", "unless", "once" },
            Reported = new[] { "said", "says", "say", "told", "repeat" },
            Unsupported = new[] { "pick up", "bring", "fetch", "grab", "carry", "break", "attack", "kill", "fight", "throw" },
            HoldDoor = new[] { "hold the door", "hold that door", "hold this door", "hold the doors", "keep the door open", "keep the doors open" },
        };

        public static readonly Phrasebook Polish = new Phrasebook
        {
            Language = CompanionLanguage.Language.Polish,
            Stop = new[] { "stój", "stój tu", "stój tutaj", "stop", "czekaj", "czekaj tu", "czekaj tutaj", "poczekaj", "poczekaj tu",
                "poczekaj tutaj", "poczekaj na mnie", "zaczekaj", "zaczekaj tu", "zaczekaj tutaj", "zaczekaj na mnie", "zatrzymaj się",
                "zostań", "zostań tu", "zostań tutaj", "zostań na miejscu", "nie ruszaj się", "nie idź za mną", "nie chodź za mną",
                "przestań za mną chodzić", "przestań chodzić za mną", "przestań iść za mną" },
            Follow = new[] { "za mną", "chodź za mną", "idź za mną", "podążaj za mną", "trzymaj się blisko", "trzymaj się mnie",
                "trzymaj się przy mnie", "zostań ze mną", "chodź ze mną", "idź ze mną", "nie odstępuj mnie", "nie zostawaj w tyle" },
            Come = new[] { "chodź tu", "chodź tutaj", "chodź do mnie", "chodź bliżej", "podejdź", "podejdź do mnie", "podejdź bliżej",
                "wróć", "wróć do mnie", "wracaj", "wracaj do mnie", "przyjdź do mnie", "przyjdź tu", "do mnie", "zbliż się" },
            StopOpenings = new[] { "stój", "czekaj", "poczekaj", "zaczekaj", "zatrzymaj się", "zostań tu", "zostań tutaj", "zostań na miejscu",
                "zostań w tym miejscu", "zostań gdzie jesteś", "przestań iść", "przestań chodzić" },
            FollowOpenings = new[] { "idź za mną", "chodź za mną", "podążaj za mną", "trzymaj się blisko", "trzymaj się mnie",
                "trzymaj się przy mnie", "zostań ze mną", "chodź ze mną", "idź ze mną" },
            ComeOpenings = new[] { "podejdź", "chodź tu", "chodź tutaj", "chodź do mnie", "chodź bliżej", "wróć", "wracaj",
                "przyjdź do mnie", "przyjdź tu", "zbliż się" },
            OpenOpenings = new[] { "otwórz", "otwórzcie", "uchyl", "otwieraj" },
            CloseOpenings = new[] { "zamknij", "zamknijcie", "przymknij", "zatrzaśnij", "zamykaj" },
            RequestedStop = new[] { "poczekać", "zaczekać", "zatrzymać się", "zostać tu", "zostać tutaj", "zostać na miejscu" },
            RequestedFollow = new[] { "iść za mną", "pójść za mną", "chodzić za mną", "podążać za mną", "trzymać się blisko" },
            RequestedCome = new[] { "podejść", "przyjść do mnie", "wrócić", "zbliżyć się" },
            RequestedOpen = new[] { "otworzyć", "uchylić" },
            RequestedClose = new[] { "zamknąć", "przymknąć", "zatrzasnąć" },

            Open = new[] { "otwórz", "otwórzcie" },
            Close = new[] { "zamknij", "zamknijcie" },
            Door = new[] { "drzwi" },
            DoorMention = new[] { "drzwi", "drzwiach", "drzwiami" },
            Determiner = new[] { "te", "tamte" },
            Nearest = new[] { "najbliższe" },
            Front = new[] { "przede mną", "przed nami", "z przodu", "na wprost" },
            Back = new[] { "za mną", "za nami", "z tyłu", "za plecami", "za moimi plecami" },
            Left = new[] { "po lewej", "po lewej stronie", "po mojej lewej", "z lewej", "z lewej strony", "na lewo" },
            Right = new[] { "po prawej", "po prawej stronie", "po mojej prawej", "z prawej", "z prawej strony", "na prawo" },
            LeftAdjective = new[] { "lewe" },
            RightAdjective = new[] { "prawe" },
            DoorQuestion = new[] { "czy" },
            DoorIsOpen = new[] { "są otwarte", "otwarte" },
            DoorIsClosed = new[] { "są zamknięte", "zamknięte" },

            OpenAll = new[] { "otwórz", "otwieraj" },
            CloseAll = new[] { "zamknij", "zamykaj" },
            Quantifier = new[] { "wszystkie", "każde" },
            BulkAdjective = new[] { "pobliskie", "otwarte", "pozostałe" },
            // "Drzwi" is both singular and plural, so Polish scope words must not also name a single door's side.
            Doors = new[] { "drzwi" },
            DoorScope = new[] { "po drodze", "na drodze", "w pobliżu", "dookoła", "wokół", "naokoło", "po kolei" },
            OpenEach = new[] { "pootwieraj" },
            CloseEach = new[] { "pozamykaj" },

            StopFollowing = new[] { "nie idź za mną", "nie chodź za mną", "nie idź już za mną", "nie chodź już za mną", "już nie idź za mną",
                "przestań za mną chodzić", "przestań chodzić za mną", "przestań iść za mną", "nie musisz za mną iść", "nie musisz za mną chodzić" },
            DontOpenDoors = new[] { "nie otwieraj drzwi", "nie otwieraj już drzwi", "nie otwieraj więcej drzwi", "nie otwieraj żadnych drzwi",
                "przestań otwierać drzwi" },
            Protect = new[] { "osłaniaj mnie", "osłoń mnie", "chroń mnie", "pilnuj mnie", "pilnuj moich pleców", "pilnuj pleców",
                "uważaj na wrogów", "obserwuj okolicę" },

            // "Weź" softens an order like "come on": "weź otwórz drzwi".
            Please = new[] { "proszę", "weź" },
            PoliteRequest = new[] { "czy możesz", "możesz", "czy mógłbyś", "mógłbyś", "czy mogłabyś", "mogłabyś", "czy możecie", "możecie" },
            Greeting = new[] { "hej", "cześć", "echo", "dobra" },
            LeadIn = new[] { "echo", "hej", "cześć", "słuchaj", "dobra", "partnerze", "kolego", "proszę" },
            Then = new[] { "a potem", "i potem", "potem", "a następnie", "i następnie", "następnie", "a później", "później" },
            And = new[] { "a potem", "i potem", "potem", "a następnie", "następnie", "a także", "oraz", "i", "a" },

            Negation = new[] { "nie", "nigdy" },
            OrderVerbs = new[] { "otwieraj", "otwórz", "zamykaj", "zamknij", "idź", "chodź", "czekaj", "podchodź", "wracaj", "zostawaj", "odchodź" },
            Condition = new[] { "jeśli", "jeżeli", "gdy", "kiedy", "chyba że" },
            Reported = new[] { "powiedział", "powiedziała", "mówił", "mówiła", "powtórz" },
            // "Weź" (take) is absent on purpose: it usually means "come on", as in "weź otwórz drzwi".
            Unsupported = new[] { "podnieś", "przynieś", "złap", "złam", "wyważ", "rozwal", "zaatakuj", "walcz", "rzuć" },
            HoldDoor = new[] { "przytrzymaj drzwi", "potrzymaj drzwi", "trzymaj drzwi" },
        };

        public static readonly Phrasebook[] Books = { English, Polish };

        /// <summary>A regex alternation of the phrases from both languages, longest first, folded and escaped.</summary>
        public static string Any(Func<Phrasebook, string[]> select) => Any(Books.SelectMany(book => select(book) ?? new string[0]));

        /// <summary>A regex alternation of one language's phrases.</summary>
        public static string Any(Phrasebook book, Func<Phrasebook, string[]> select) => Any(select(book) ?? new string[0]);

        public static string Any(IEnumerable<string> phrases)
        {
            var escaped = phrases.Where(phrase => !string.IsNullOrEmpty(phrase)).Select(CompanionLanguage.Fold).Distinct()
                .OrderByDescending(phrase => phrase.Length).Select(Regex.Escape).ToArray();
            // An empty list must never match, not even the empty string.
            return escaped.Length == 0 ? "(?!)" : "(?:" + string.Join("|", escaped) + ")";
        }

        /// <summary>Simplified phrases from both languages, for whole-utterance comparison.</summary>
        public static HashSet<string> Set(Func<Phrasebook, string[]> select)
            => new HashSet<string>(Books.SelectMany(book => select(book) ?? new string[0]).Select(CompanionLanguage.Simplify));

        /// <summary>Whether the folded phrase is one of the listed ones in either language.</summary>
        public static bool Contains(Func<Phrasebook, string[]> select, string phrase)
            => Books.Any(book => (select(book) ?? new string[0]).Any(entry => CompanionLanguage.Fold(entry) == CompanionLanguage.Fold(phrase)));
    }
}
