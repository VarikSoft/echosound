using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace EchoSound.Companion
{
    /// <summary>Player-provided facts and a short conversation window. Model replies never update facts.</summary>
    public sealed class CompanionDialogue
    {
        private const RegexOptions Options = RegexOptions.CultureInvariant;
        private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Names are read from the original words, so their diacritics and capital letters survive.
        private const string Name = @"(?<name>\p{L}[\p{L}-]{1,31})";
        private const string ProperName = @"(?<name>\p{Lu}[\p{L}-]{1,31})";
        private static readonly Regex ExplicitPlayerName = new Regex(
            @"^(?:(?:actually|w(?:ł|l)a(?:ś|s)ciwie)[, ]+)?(?:my name is|mam na imi[eę]|nazywam si[eę]) " + Name + @"(?=$|[,.!])", IgnoreCase);
        private static readonly Regex Introduction = new Regex(
            @"^(?i:(?:hey[,.!]\s*|hi[,.!]\s*|hej[,.!]\s*|cze(?:ś|s)(?:ć|c)[,.!]\s*)?(?:i'm|i am|jestem)) " + ProperName
            + @"(?=$|[,.!]|\s+(?i:and my|a m(?:ó|o)j|i m(?:ó|o)j))", Options);
        private static readonly Regex CatName = new Regex(
            @"(?:^|[,!.]\s*|\s+(?:and|a|i)\s+)(?:my cat(?:'s name is| is named| is called)|i have a cat (?:called|named)"
            + @"|m(?:ó|o)j kot ma na imi[eę]|m(?:ó|o)j kot nazywa si[eę]|mam kota o imieniu) " + Name + @"(?=$|[,.!])", IgnoreCase);
        private static readonly Regex PairedCatName = new Regex(
            @"[,!]\s*(?:and |a |i )?(?:my cat is|my cat's name is|kot ma na imi[eę]|m(?:ó|o)j kot to) " + Name + @"(?=$|[,.!])", IgnoreCase);

        // The rest is matched on simplified text: lower case, no punctuation, no Polish diacritics.
        private static readonly Regex Mood = new Regex(@"^(?:(?:hey|hi|hello|hej|czesc) )?(?:how are you(?: doing| feeling)?|how(?:'s| is) it going|how do you feel|jak sie czujesz|jak tam|jak nastroj|co u ciebie|jak leci)$", Options);
        private static readonly Regex Opinion = new Regex(@"^(?:what do you think (?:of|about) (?:this|the) game|do you like (?:this|the) game|co (?:myslisz|sadzisz) o (?:tej )?grze|podoba ci sie (?:ta )?gra)$", Options);
        private static readonly Regex JokeRequest = new Regex(@"^(?:(?:can you |could you )?(?:tell (?:me )?|make (?:up )?)(?:a |another |a short |a new )?joke|(?:czy )?(?:mozesz )?(?:opowiedz|powiedz|wymysl|opowiedziec|powiedziec|wymyslic) (?:mi )?(?:krotki |jeszcze jeden |nowy |inny )?(?:zart|dowcip))(?: please| prosze)?$", Options);
        private static readonly Regex Why = new Regex(@"^(?:why(?: (?:is that|do you (?:think|say) (?:that|so)))?|dlaczego|czemu|dlaczego tak(?: myslisz)?)$", Options);
        private static readonly Regex ExplainJoke = new Regex(@"^(?:what(?:'s| is) the joke|explain (?:the |that )?joke|i don't get it|wyjasnij (?:zart|dowcip)|o co chodzi w (?:zarcie|dowcipie)|nie rozumiem(?: zartu)?)$", Options);
        private static readonly Regex PlayerNameQuestion = new Regex(
            @"^(?:what(?:'s| is) my name|what did i say my name was|what should you call me(?: now)?|jak mam na imie|jak sie nazywam|jak masz sie do mnie (?:teraz )?zwracac)$", Options);
        private static readonly Regex CatNameQuestion = new Regex(
            @"^(?:what(?:'s| is) my cat'?s name|what did i name my cat|jak ma na imie moj kot|jak nazywa sie moj kot)$", Options);
        private readonly Queue<(string Player, string Echo)> exchanges = new Queue<(string, string)>();
        private string playerName;
        private string catName;
        private string reason, explanation, nextReason, nextExplanation;
        private int jokeIndex;

        public bool TryReply(string text, out string reply)
        {
            reply = null;
            // Quoted or reported words are not addressed to the companion.
            if (string.IsNullOrWhiteSpace(text) || CompanionLanguage.HasQuote(text)) return false;
            string normalized = text.Trim().Replace('’', '\'');
            string simple = CompanionLanguage.Simplify(text);
            nextReason = nextExplanation = null;
            if (PlayerNameQuestion.IsMatch(simple))
            {
                reply = playerName == null
                    ? Say(text, "I don't know your name yet.", "Jeszcze nie znam twojego imienia.")
                    : Say(text, $"Your name is {playerName}.", $"Masz na imię {playerName}.");
                return true;
            }
            if (CatNameQuestion.IsMatch(simple))
            {
                reply = catName == null
                    ? Say(text, "You haven't told me your cat's name yet.", "Jeszcze nie znam imienia twojego kota.")
                    : Say(text, $"Your cat is called {catName}.", $"Twój kot ma na imię {catName}.");
                return true;
            }
            if (simple is "what's your name" or "what is your name" or "jak masz na imie" or "jak sie nazywasz")
            {
                reply = Say(text, "Call me Echo.", "Mów mi Echo.");
                return true;
            }

            // Only affirmative statements from the player can create or correct these facts.
            if (!text.Contains("?"))
            {
                Match person = ExplicitPlayerName.Match(normalized);
                if (!person.Success) person = Introduction.Match(normalized);
                Match cat = CatName.Match(normalized);
                if (person.Success && !cat.Success) cat = PairedCatName.Match(normalized);
                if (person.Success) playerName = person.Groups["name"].Value;
                if (cat.Success) catName = cat.Groups["name"].Value;
                if (person.Success || cat.Success)
                {
                    reply = person.Success && cat.Success
                        ? Say(text, $"Got it, {playerName}. Your cat is {catName}.", $"Jasne, {playerName}. Twój kot to {catName}.")
                        : person.Success
                        ? Say(text, $"Got it, {playerName}.", $"Jasne, {playerName}.")
                        : Say(text, $"I'll remember {catName}.", $"Zapamiętam imię {catName}.");
                    return true;
                }
            }
            return TrySocial(text, simple, out reply);
        }

        private static string Say(string playerWords, string english, string polish) => CompanionLanguage.Pick(playerWords, english, polish);

        private bool TrySocial(string order, string text, out string reply)
        {
            reply = null;
            if (Mood.IsMatch(text))
                return Social(order, "Alert, but better with you here.", "Czujnie, ale z tobą jest raźniej.",
                    "Two teammates handle surprises better.", "Razem łatwiej radzić sobie z niespodziankami.", out reply);
            if (Opinion.IsMatch(text))
                return Social(order, "Tense. Exploring together makes it worthwhile.", "Napięta atmosfera. Razem odkrywa się ciekawiej.",
                    "Sharing the uncertainty makes exploration exciting.", "Wspólne odkrywanie nieznanego dodaje emocji.", out reply);
            if (JokeRequest.IsMatch(text) || (explanation != null && text is "another one" or "one more" or "jeszcze jeden" or "jeszcze raz"))
            {
                bool first = jokeIndex++ % 2 == 0;
                reply = first
                    ? Say(order, "Ghosts hate rain. It dampens their spirits.", "Czemu duch nie kłamie? Bo jest przejrzysty.")
                    : Say(order, "The skeleton stayed home. He had no guts.", "Co robi duch na siłowni? Ćwiczy martwy ciąg.");
                nextExplanation = first
                    ? Say(order, "Spirits means both ghosts and mood.", "Przejrzysty może znaczyć przezroczysty albo szczery.")
                    : Say(order, "No guts means both no courage and no organs.", "Martwy ciąg to ćwiczenie, a duch już nie żyje.");
                nextReason = nextExplanation;
                return true;
            }
            if (ExplainJoke.IsMatch(text) && explanation != null)
            {
                reply = explanation;
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (Why.IsMatch(text) && reason != null)
            {
                reply = reason;
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (text is "haha" or "ha ha" or "that's funny" or "nice one" or "dobre" or "haha dobre" or "smieszne" && explanation != null)
            {
                reply = Say(order, "Good. The ghosts get the next one.", "Dobrze. Następny żart opowiem duchom.");
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (text is "not funny" or "that's not funny" or "to nie jest smieszne" or "nie smieszne" or "slabe" && explanation != null)
            {
                reply = Say(order, "Fair. I'll retire that one.", "Racja. Ten żart skreślam.");
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (text is "what are you afraid of" or "are you afraid" or "czego sie boisz" or "boisz sie")
                return Social(order, "Losing my teammate in the dark.", "Zgubić partnera w ciemności.",
                    "Exploring alone is much more unsettling.", "Samotne odkrywanie budzi większy niepokój.", out reply);
            if (text is "do you like music" or "lubisz muzyke")
                return Social(order, "Quiet music, yes. I like hearing footsteps.", "Cichą, tak. Wolę też słyszeć kroki.",
                    "Loud music drowns out nearby sounds.", "Głośna muzyka zagłusza dźwięki otoczenia.", out reply);
            if (text is "i don't like the dark" or "i'm afraid of the dark" or "boje sie ciemnosci" or "nie lubie ciemnosci")
                return Social(order, "Understandable. It's easier with company.", "Rozumiem. Z kimś obok jest łatwiej.",
                    "You can share the worry instead of facing it alone.", "Nie trzeba mierzyć się z lękiem samemu.", out reply);
            return false;
        }

        private bool Social(string order, string en, string pl, string reasonEn, string reasonPl, out string reply)
        {
            reply = Say(order, en, pl);
            nextReason = Say(order, reasonEn, reasonPl);
            return true;
        }

        public string Facts
        {
            get
            {
                string facts = playerName == null ? "" : $"The player's name is {playerName}. ";
                if (catName != null) facts += $"The player's cat is called {catName}.";
                return facts.TrimEnd();
            }
        }

        public JArray RecentMessages()
        {
            var messages = new JArray();
            foreach (var exchange in exchanges)
            {
                messages.Add(new JObject { ["role"] = "user", ["content"] = exchange.Player });
                messages.Add(new JObject { ["role"] = "assistant", ["content"] = exchange.Echo });
            }
            return messages;
        }

        public void Remember(string player, string reply)
        {
            reason = nextReason; explanation = nextExplanation;
            nextReason = nextExplanation = null;
            if (exchanges.Count == 4) exchanges.Dequeue();
            exchanges.Enqueue((Shorten(player), Shorten(reply)));
            int characters = 0;
            foreach (var exchange in exchanges) characters += exchange.Player.Length + exchange.Echo.Length;
            while (exchanges.Count > 2 && characters > 600)
            {
                var oldest = exchanges.Dequeue();
                characters -= oldest.Player.Length + oldest.Echo.Length;
            }
        }

        public void NoteCommand(string order)
        {
            reason = Say(order, "You asked me to do that.", "To było twoje polecenie.");
            explanation = nextReason = nextExplanation = null;
        }

        private static string Shorten(string text)
        {
            if (text.Length <= 160) return text;
            int end = text.LastIndexOfAny(new[] { '.', '!', '?' }, 159);
            if (end < 20) end = text.LastIndexOf(' ', 159);
            return text.Substring(0, end > 0 ? end + 1 : 160).TrimEnd();
        }

        public void Reset()
        {
            exchanges.Clear();
            playerName = catName = null;
            reason = explanation = nextReason = nextExplanation = null;
            jokeIndex = 0;
        }
    }
}
