using System.Text;
using System.Text.RegularExpressions;

namespace EchoSound.Companion
{
    /// <summary>Recognizes complete movement commands and explicit changes to following.</summary>
    public static class QuickCompanionCommand
    {
        public enum DismissalAction { Wait, CloseNearbyDoors, ClarifyDoors }

        [System.Flags]
        public enum FollowContext { None = 0, Protection = 1, OpenOnPath = 2, ClarifyDoors = 4 }

        private static readonly Regex WaitForPlayer = new Regex(@"^(?:можешь|можете) подождать (?:здесь|тут)[,.]? пока я (?:немного |сначала |быстро )?(?:осмотрюсь|разберусь|проверю|посмотрю|попробую)\b[\p{L}\p{N}\s,.-]*[.!?]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ExtraOrder = new Regex(@"\b(?:ты|тебе|тебя|открой|откройте|закрой|закройте|иди|подойди|следуй|возьми|сломай|принеси|open|close|follow|come)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex FollowWithTail = new Regex(
            @"^(?:(?:иди(?:те)?|следуй(?:те)?|ходи(?:те)?) за мной|за мной|держись рядом(?: со мной)?)(?: *[,.!…;]+ *| и | (?=короче\b|следи\b|прикрой\b))(?<tail>.+)$");
        private static readonly Regex FollowTailBoundary = new Regex(
            @"[.!…;]+ *|,? +(?=(?:(?:и|а) )?(?:может(?: быть)?|наверное|возможно|не знаю)\b)| +и +(?=(?:(?:откр|закр)[а-яё]+|прикрой|защити|следи)\b)");
        private static readonly Regex FollowTailLeadIn = new Regex(@"^(?:(?:и|а|короче|слушай)[ ,]+)+");
        private static readonly Regex ProtectionRequest = new Regex(
            @"^(?:(?:прикрой(?:те)?|прикрывай|защити(?:те)?|защищай|охраняй) меня|будь начеку|смотри по сторонам"
            + @"|(?:следи|смотри),? чтоб(?:ы)? (?:(?:враги?|монстры?|они) (?:меня не|не)|меня враги? не) (?:убили?|били?|тронули?|заметили?|поймали?)(?: меня)?)$");
        private static readonly Regex UncertainDoorRequest = new Regex(
            @"^(?:может(?: быть)?|наверное|возможно)[ ,]+(?:[а-яё-]+ ){0,10}двер(?:ь|и|ей|ью|ям|ями)[а-яё ,?-]*$");
        private static readonly Regex FollowConflict = new Regex(
            @"\b(?:не|пока|если|когда|только|после|прежде|чтобы|стой|стоп|жди|остановись|иди|ходи|следуй|останься|подойди|ехать|беги)\b");

        /// <summary>Identify a clear follow order with a separate request about protection or doors.</summary>
        public static bool TryParseFollowWithContext(string text, out FollowContext context)
        {
            context = FollowContext.None;
            if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(new[] { '"', '\'', '«', '»', '“', '”', '„' }) >= 0) return false;
            string value = Regex.Replace(FastIntentClient.StripGreeting(text), @"\s+", " ");
            Match match = FollowWithTail.Match(FollowTailLeadIn.Replace(value, ""));
            if (!match.Success) return false;

            var parsed = FollowContext.None;
            foreach (string part in FollowTailBoundary.Split(match.Groups["tail"].Value))
            {
                string clause = FollowTailLeadIn.Replace(part.Trim(), "");
                if (clause is "" or "короче" or "слушай" or "не знаю" or "не знаю?") continue;
                if (ProtectionRequest.IsMatch(clause)) parsed |= FollowContext.Protection;
                else if (QuickDoorCommand.IsEscortOpening(clause)) parsed |= FollowContext.OpenOnPath;
                else if (UncertainDoorRequest.IsMatch(clause) && !FollowConflict.IsMatch(clause)) parsed |= FollowContext.ClarifyDoors;
                else return false;
            }

            // A tentative door request can revise the preceding one. Keep following and ask.
            if ((parsed & FollowContext.ClarifyDoors) != 0) parsed &= ~FollowContext.OpenOnPath;
            context = parsed;
            return true;
        }

        private static readonly Regex DismissalLeadIn = new Regex(
            @"^(?:(?:эхо|напарник|пана|слушай|пожалуйста|а ещ[её](?: (?:этот|это|э))?)(?: *[,!.…]+ *| +))+");
        private const string DismissalJoin = @"(?: *[,;.!…]+ *| +)(?:(?:и|а|а лучше|лучше) )?";
        private const string DismissalRequest = @"(?<polite>можешь |можете |мог бы ты )?";
        private const string StopFollowing = @"(?:(?:больше )?не (?:иди(?:те)?|ходи(?:те)?|следуй(?:те)?|идти|ходить|следовать)(?: больше)? за мной"
            + @"|(?:(?:больше )?не ходить(?: больше)?(?:,? и|,)? )?(?:больше )?не (?:преследуй(?:те)?|преследовать)(?: больше)? меня"
            + @"|перестан(?:ь|ьте) (?:идти за мной|ходить за мной|следовать за мной|преследовать меня))";
        private const string StopOpening = @"(?:больше )?не (?:(?:открывай(?:те)?|открывать|открой(?:те)?) (?:больше )?двер(?:ь|и|ей)(?: больше)?|открываю больше две)";
        private const string WalkAndClose = @"(?:(?:где[ -]то )?(?:гуляй|гулять|пройдись|пройтись|походи|походить)(?: (?:сам|самостоятельно))? и )?"
            + @"(?:закрой(?:те)?|закрывай(?:те)?|закрыть|закрывать) (?:(?:все )?(?:ближайшие|открытые|соседние) |все )?двери(?: (?:рядом|вокруг|по пути))?";
        private static readonly Regex Dismissal = new Regex(
            "^" + DismissalRequest + StopFollowing
            + "(?:" + DismissalJoin + "(?:" + StopOpening + "|(?<close>" + WalkAndClose + ")))?"
            + @"(?:,? пожалуйста)?(?(polite)\??)$");
        private static readonly Regex UnclearDoorFollowup = new Regex("^" + DismissalRequest + StopFollowing
            + @" *, *(?:а|и) (?:пройти|пройтись|пройдись|гулять|гуляй|походить|походи) (?:[а-яё]+ ){1,8}двер[ьи](?(polite)\??)$");
        private static readonly Regex ConditionalFollowup = new Regex(@"\b(?:если|когда|пока|только|после|прежде)\b");

        /// <summary>Cancel following and handle an explicit or unclear door follow-up.</summary>
        public static bool TryParseDismissal(string text, out DismissalAction action)
        {
            action = DismissalAction.Wait;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = Regex.Replace(FastIntentClient.StripGreeting(text), @"\s+", " ");
            value = DismissalLeadIn.Replace(value, "").TrimEnd('.', '!', '…').Trim();
            Match match = Dismissal.Match(value);
            if (match.Success)
            {
                if (match.Groups["close"].Success) action = DismissalAction.CloseNearbyDoors;
                return true;
            }
            // A complete stop request can stand on its own when the distinct door follow-up is garbled.
            if (!UnclearDoorFollowup.IsMatch(value) || ConditionalFollowup.IsMatch(value)) return false;
            action = DismissalAction.ClarifyDoors;
            return true;
        }

        // Offload an explicit movement suffix after a door order. Keep questions, conditions,
        // negations and unrecognised suffixes intact for interpretation rather than guessing.
        public static bool TrySplitFollowup(string order, out string first, out string followup)
        {
            first = followup = null;
            if (string.IsNullOrWhiteSpace(order) || order.Contains("?") || order.Contains("\"")) return false;
            string lower = order.Trim().ToLowerInvariant();
            foreach (string condition in new[] { " if ", " unless ", " when ", " jeśli ", " jeżeli ", " jezeli ", " gdy ", " если ", " когда " })
                if (lower.Contains(condition)) return false;
            bool doorOrder = false;
            foreach (string verb in new[] { "open ", "close ", "открой ", "закрой ", "otwórz ", "zamknij " })
                if (lower.StartsWith(verb, System.StringComparison.Ordinal)) doorOrder = true;
            if (!doorOrder) return false;
            foreach (string separator in new[] { " and then ", " then ", " a potem ", " i potem ", " potem ", " а потом ", " и затем ", " затем ", " потом " })
            {
                int split = lower.LastIndexOf(separator, System.StringComparison.Ordinal);
                if (split < 0) continue;
                string tail = order.Trim().Substring(split + separator.Length);
                if (!TryParse(tail, out _, out _)) continue;
                first = order.Trim().Substring(0, split).Trim();
                followup = tail;
                return true;
            }
            return false;
        }

        public static bool TryParse(string text, out string action, out string reply)
        {
            action = reply = null;
            if (string.IsNullOrWhiteSpace(text) || text.Contains('"') || text.Contains('\'') || text.Contains('‘') || text.Contains('’')
                || text.Contains('«') || text.Contains('»') || text.Contains('“') || text.Contains('”')) return false;
            // An explicit wait while the player looks around is one order, not a sequence of
            // actions for the companion. Reject another addressee or an imperative in the reason.
            if (WaitForPlayer.IsMatch(Regex.Replace(text.Trim(), @"\s+", " ")) && !ExtraOrder.IsMatch(text))
            {
                action = "wait_here";
                reply = "Жду здесь.";
                return true;
            }
            if (text.Contains("?")) return false;
            var normalized = new StringBuilder();
            foreach (char letter in text.Trim().ToLowerInvariant())
                if (char.IsLetterOrDigit(letter) || char.IsWhiteSpace(letter)) normalized.Append(letter);
            string value = string.Join(" ", normalized.ToString().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
            switch (value)
            {
                case "stop": case "wait": case "wait here": case "stay here": case "please stop": case "please stay where you are":
                    action = "wait_here"; reply = "Waiting here."; break;
                case "follow me": case "follow":
                    action = "follow_curator"; reply = "Following you."; break;
                case "come here": case "come to me": case "come back":
                    action = "go_to"; reply = "Coming to you."; break;
                case "stój": case "stoj": case "czekaj": case "zatrzymaj się": case "zostań tutaj":
                    action = "wait_here"; reply = "Czekam tutaj."; break;
                case "chodź za mną": case "idź za mną": case "za mną":
                    action = "follow_curator"; reply = "Idę za tobą."; break;
                case "chodź tutaj": case "chodź do mnie": case "wróć do mnie": case "podejdź do mnie": case "podejdz do mnie":
                    action = "go_to"; reply = "Idę do ciebie."; break;
                case "стой": case "стоп": case "жди": case "жди здесь": case "остановись":
                    action = "wait_here"; reply = "Жду здесь."; break;
                case "за мной": case "иди за мной": case "следуй за мной": case "не отставай":
                    action = "follow_curator"; reply = "Иду за тобой."; break;
                case "ко мне": case "иди сюда": case "вернись ко мне":
                    action = "go_to"; reply = "Иду к тебе."; break;
            }
            return action != null;
        }
    }
}
