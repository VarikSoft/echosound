using System.Text;

namespace EchoSound.Companion
{
    /// <summary>Exact, standalone commands only. Negations, questions and compound orders go to the LLM.</summary>
    public static class QuickCompanionCommand
    {
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
            if (string.IsNullOrWhiteSpace(text) || text.Contains("?")) return false;
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
                // Whisper occasionally renders the imperative as "следую"; accept that
                // exact standalone ASR variant, without fuzzy matching negations or questions.
                case "за мной": case "иди за мной": case "следуй за мной": case "следую за мной":
                    action = "follow_curator"; reply = "Иду за тобой."; break;
                case "ко мне": case "иди сюда": case "вернись ко мне":
                    action = "go_to"; reply = "Иду к тебе."; break;
            }
            return action != null;
        }
    }
}
