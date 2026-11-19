using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace EchoSound.Companion
{
    /// <summary>Player-provided facts and a short conversation window. Model replies never update facts.</summary>
    public sealed class CompanionDialogue
    {
        private const string Name = @"(?<name>\p{L}[\p{L}-]{1,31})";
        private const string ProperName = @"(?<name>\p{Lu}[\p{L}-]{1,31})";
        private static readonly Regex ExplicitPlayerName = new Regex(
            @"^(?:(?:я поправлюсь[: ,]+)?(?:на самом деле )?меня зовут|my name is|mam na imię|nazywam się) " + Name + @"(?=$|[,.!])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Introduction = new Regex(
            @"^(?i:(?:привет[,.!]\s*|hey[,.!]\s*|hi[,.!]\s*|cześć[,.!]\s*)?(?:я|i'm|i am|jestem)) " + ProperName + @"(?=$|[,.!]|\s+(?i:а моего|and my))");
        private static readonly Regex CatName = new Regex(
            @"(?:^|[,!.]\s*|\s+а\s+)(?:моего кота зовут|у меня кот по кличке|my cat(?:'s name is| is named| is called)|mój kot ma na imię) " + Name + @"(?=$|[,.!])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PairedCatName = new Regex(
            @"[,!]\s*(?:а |and |a )?(?:кота зовут|my cat is|kot ma na imię) " + Name + @"(?=$|[,.!])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Mood = new Regex(@"^(?:а |ну |(?:привет|hey|hi|hello|hej|cześć)[,.!]? )?(?:(?:у тебя )?как (?:у тебя )?(?:дела|настроение)|как твои дела|как твоё настроение|как ты себя чувствуешь|ты как|как сам|how are you(?: doing)?|how(?:'s| is) it going|how do you feel|jak się czujesz|jak tam|jak nastrój)$");
        private static readonly Regex Opinion = new Regex(@"^(?:а )?(?:что (?:ты )?думаешь об? (?:этой )?игре|как тебе (?:эта )?игра|тебе нравится (?:эта )?игра|what do you think (?:of|about) (?:this|the) game|do you like (?:this|the) game|co (?:myślisz|sądzisz) o (?:tej )?grze)$");
        private static readonly Regex JokeRequest = new Regex(@"^(?:(?:можешь|можете) )?(?:(?:расскажи(?:те)?|рассказать|скажи|придумай|придумать) (?:мне )?(?:(?:короткую|ещё одну|другую|новую) )?(?:шутку|анекдот)|пошути|(?:can you |could you )?(?:tell (?:me )?|make )(?:a |another |a short )?joke|(?:opowiedz|powiedz) (?:mi )?(?:krótki |jeszcze jeden )?(?:żart|dowcip))(?: пожалуйста| please)?$");
        private static readonly Regex Why = new Regex(@"^(?:а )?(?:почему(?: (?:так|ты так|так думаешь|ты так думаешь))?|зачем|why(?: (?:is that|do you (?:think|say) (?:that|so)))?|dlaczego|czemu)$");
        private static readonly Regex ExplainJoke = new Regex(@"^(?:в чём (?:смысл|шутка|прикол)|в чем (?:смысл|шутка|прикол)|объясни (?:шутку|прикол)|не понял(?: шутку)?|what(?:'s| is) the joke|explain (?:the |that )?joke|wyjaśnij (?:żart|dowcip)|o co chodzi w (?:żarcie|dowcipie))$");
        private static readonly Regex PlayerNameQuestion = new Regex(
            @"^(?:как меня зовут|как я представился(?: в начале)?|как теперь ко мне обращаться|как ко мне обращаться|what(?:'s| is) my name|what did i say my name was|jak mam na imię)[?.!]*$");
        private static readonly Regex CatNameQuestion = new Regex(
            @"^(?:как зовут моего кота|как зовут кота|what(?:'s| is) my cat's name|what did i name my cat|jak ma na imię mój kot)[?.!]*$");
        private readonly Queue<(string Player, string Echo)> exchanges = new Queue<(string, string)>();
        private string playerName;
        private string catName;
        private string reason, explanation, nextReason, nextExplanation;
        private int jokeIndex;

        public bool TryReply(string text, out string reply)
        {
            string normalized = text.Trim().Replace('’', '\'');
            string lower = normalized.ToLowerInvariant();
            nextReason = nextExplanation = null;
            if (PlayerNameQuestion.IsMatch(lower))
            {
                reply = playerName == null
                    ? CompanionBrain.LocalReply(text, "I don't know your name yet.", "Jeszcze nie znam twojego imienia.", "Ты ещё не называл своё имя.")
                    : CompanionBrain.LocalReply(text, $"Your name is {playerName}.", $"Masz na imię {playerName}.", $"Тебя зовут {playerName}.");
                return true;
            }
            if (CatNameQuestion.IsMatch(lower))
            {
                reply = catName == null
                    ? CompanionBrain.LocalReply(text, "You haven't told me your cat's name yet.", "Jeszcze nie znam imienia twojego kota.", "Ты ещё не говорил, как зовут твоего кота.")
                    : CompanionBrain.LocalReply(text, $"Your cat is called {catName}.", $"Twój kot ma na imię {catName}.", $"Твоего кота зовут {catName}.");
                return true;
            }
            if (lower.TrimEnd('!', '.', '?') is "как тебя зовут" or "what's your name" or "what is your name" or "jak masz na imię")
            {
                reply = CompanionBrain.LocalReply(text, "Call me Echo.", "Mów mi Echo.", "Зови меня Эхо.");
                return true;
            }

            // Only affirmative statements from the player can create or correct these facts.
            if (!lower.Contains("?") && !lower.Contains('"') && !lower.Contains('«'))
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
                        ? CompanionBrain.LocalReply(text, $"Got it, {playerName}. Your cat is {catName}.", $"Jasne, {playerName}. Twój kot to {catName}.", $"Понял, {playerName}. Кота зовут {catName}.")
                        : person.Success
                        ? CompanionBrain.LocalReply(text, $"Got it, {playerName}.", $"Jasne, {playerName}.", $"Понял, {playerName}.")
                        : CompanionBrain.LocalReply(text, $"I'll remember {catName}.", $"Zapamiętam imię {catName}.", $"Запомню, что твоего кота зовут {catName}.");
                    return true;
                }
            }
            return TrySocial(text, lower.TrimEnd('?', '!', '.').Trim(), out reply);
        }

        private bool TrySocial(string order, string text, out string reply)
        {
            reply = null;
            if (Mood.IsMatch(text))
                return Social(order, "Alert, but better with you here.", "Czujnie, ale z tobą jest raźniej.", "Настороженно, но с тобой спокойнее.",
                    "Two teammates handle surprises better.", "Razem łatwiej radzić sobie z niespodziankami.", "Вдвоём проще справляться с неожиданностями.", out reply);
            if (Opinion.IsMatch(text))
                return Social(order, "Tense. Exploring together makes it worthwhile.", "Napięta atmosfera. Razem odkrywa się ciekawiej.", "Напряжённо. Исследовать вместе интереснее.",
                    "Sharing the uncertainty makes exploration exciting.", "Wspólne odkrywanie nieznanego dodaje emocji.", "Неизвестность добавляет исследованию азарта.", out reply);
            if (JokeRequest.IsMatch(text) || (explanation != null && text is "ещё" or "ещё одну" or "еще" or "еще одну" or "another one" or "jeszcze jeden"))
            {
                bool first = jokeIndex++ % 2 == 0;
                reply = first
                    ? CompanionBrain.LocalReply(order, "Ghosts hate rain. It dampens their spirits.", "Czemu duch nie kłamie? Bo jest przejrzysty.", "Призрак взял выходной: сегодня он не в духе.")
                    : CompanionBrain.LocalReply(order, "The skeleton stayed home. He had no guts.", "Co robi duch na siłowni? Ćwiczy martwy ciąg.", "Скелет не пошёл на хоррор: духу не хватило.");
                nextExplanation = first
                    ? CompanionBrain.LocalReply(order, "Spirits means both ghosts and mood.", "Przejrzysty może znaczyć przezroczysty albo szczery.", "«Не в духе» — и про настроение, и про призрака.")
                    : CompanionBrain.LocalReply(order, "No guts means both no courage and no organs.", "Martwy ciąg to ćwiczenie, a duch już nie żyje.", "«Духу не хватило» значит, что не хватило смелости.");
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
            if (text is "ха-ха, неплохо" or "ха-ха неплохо" or "смешно" or "хаха" or "haha" or "ha ha" or "that's funny" or "dobre" && explanation != null)
            {
                reply = CompanionBrain.LocalReply(order, "Good. The ghosts get the next one.", "Dobrze. Następny żart opowiem duchom.", "Отлично. Следующую расскажем призракам.");
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (text is "не смешно" or "not funny" or "to nie jest śmieszne" && explanation != null)
            {
                reply = CompanionBrain.LocalReply(order, "Fair. I'll retire that one.", "Racja. Ten żart skreślam.", "Ладно, эту вычёркиваю.");
                nextReason = reason; nextExplanation = explanation;
                return true;
            }
            if (text is "а ты чего боишься" or "чего ты боишься" or "ты боишься" or "what are you afraid of" or "czego się boisz")
                return Social(order, "Losing my teammate in the dark.", "Zgubić partnera w ciemności.", "Потерять напарника в темноте.",
                    "Exploring alone is much more unsettling.", "Samotne odkrywanie budzi większy niepokój.", "В одиночку здесь гораздо тревожнее.", out reply);
            if (text is "ты любишь музыку" or "тебе нравится музыка" or "do you like music" or "lubisz muzykę")
                return Social(order, "Quiet music, yes. I like hearing footsteps.", "Cichą, tak. Wolę też słyszeć kroki.", "Тихую — да. Хочется ещё слышать шаги.",
                    "Loud music drowns out nearby sounds.", "Głośna muzyka zagłusza dźwięki otoczenia.", "Громкая музыка заглушает звуки вокруг.", out reply);
            if (text is "я не люблю темноту" or "боюсь темноты" or "я боюсь темноты" or "i don't like the dark" or "i'm afraid of the dark" or "boję się ciemności")
                return Social(order, "Understandable. It's easier with company.", "Rozumiem. Z kimś obok jest łatwiej.", "Понимаю. С компанией пережить её проще.",
                    "You can share the worry instead of facing it alone.", "Nie trzeba mierzyć się z lękiem samemu.", "Не приходится справляться со страхом в одиночку.", out reply);
            return false;
        }

        private bool Social(string order, string en, string pl, string ru, string reasonEn, string reasonPl, string reasonRu, out string reply)
        {
            reply = CompanionBrain.LocalReply(order, en, pl, ru);
            nextReason = CompanionBrain.LocalReply(order, reasonEn, reasonPl, reasonRu);
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
            reason = CompanionBrain.LocalReply(order, "You asked me to do that.", "To było twoje polecenie.", "Ты дал мне такую команду.");
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
