using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EchoSound.Companion
{
    /// <summary>
    /// The companion speaks English and Polish. A reply follows the language of the player's words;
    /// when the words do not tell, for example a bare name or "stop", the speech language setting decides.
    /// </summary>
    public static class CompanionLanguage
    {
        public enum Language { English, Polish }

        private static Language? preferred;

        /// <summary>Tie-breaker for ambiguous words. Follows the speech language; otherwise the system language.</summary>
        public static Language Preferred
        {
            get => preferred ?? (Application.systemLanguage == SystemLanguage.Polish ? Language.Polish : Language.English);
            set => preferred = value;
        }

        /// <summary>The language for a Whisper language name such as "polish"; null for "auto" or anything else.</summary>
        public static Language? FromSpeech(string whisperLanguage)
        {
            switch (whisperLanguage)
            {
                case "polish": return Language.Polish;
                case "english": return Language.English;
                default: return null;
            }
        }

        private static readonly Regex PolishLetters = new Regex("[ąćęłńóśźż]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Words that stay recognisably Polish or English after folding. Words both languages use,
        // such as "stop", "ok", "a", "i" and "do", are deliberately absent.
        private static readonly Regex PolishWords = new Regex(@"\b(?:drzwi|mna|mnie|mi|cie|ciebie|tobie|sie|nie|tak|czy|jak|jaki|jaka|gdzie|kiedy|tu|tutaj|tam|prosze|dzieki|dziekuje|czesc|hej|jestem|jestes|jest|sa|mam|masz|za|po|na|przed|ze|juz|jeszcze|bardzo|moze|mozesz|ten|ta|lewej|prawej|przodu|tylu|wszystkie|otworz|otwieraj|zamknij|zamykaj|idz|chodz|podazaj|czekaj|poczekaj|zaczekaj|stoj|zatrzymaj|zostan|podejdz|wroc|trzymaj|przestan|oslaniaj|zart|dowcip|dobrze|twoj|moj|kot|imie|co|robisz|dlaczego|czemu|zaproponuj|powiedz|opowiedz)\b",
            RegexOptions.CultureInvariant);
        private static readonly Regex EnglishWords = new Regex(@"\b(?:the|an|you|your|me|my|i'm|it|it's|this|that|with|for|of|about|have|has|was|were|will|just|is|are|am|door|doors|please|open|close|shut|follow|come|here|there|what|how|why|wait|stay|thanks|thank|hello|hi|hey|joke|name|cat|left|right|front|behind|all|and|then|don't|can|could|would|suggest|tell)\b",
            RegexOptions.CultureInvariant);

        /// <summary>The language of the player's words, with the speech setting as the tie-breaker.</summary>
        public static Language Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Preferred;
            if (PolishLetters.IsMatch(text)) return Language.Polish;
            string folded = Fold(text);
            int polish = PolishWords.Matches(folded).Count;
            int english = EnglishWords.Matches(folded).Count;
            if (polish != english) return polish > english ? Language.Polish : Language.English;
            return Preferred;
        }

        /// <summary>Chooses the reply in the language of the player's words.</summary>
        public static string Pick(string playerWords, string english, string polish)
            => Detect(playerWords) == Language.Polish ? polish : english;

        /// <summary>The language's English name, for instructions to the language model.</summary>
        public static string Name(Language language) => language == Language.Polish ? "Polish" : "English";

        /// <summary>
        /// Lower case without Polish diacritics and with straight apostrophes, so typed and transcribed
        /// Polish match the same phrases whether or not the diacritics are present.
        /// </summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var builder = new StringBuilder(text.Length);
            foreach (char letter in text.ToLowerInvariant())
            {
                switch (letter)
                {
                    case 'ą': builder.Append('a'); break;
                    case 'ć': builder.Append('c'); break;
                    case 'ę': builder.Append('e'); break;
                    case 'ł': builder.Append('l'); break;
                    case 'ń': builder.Append('n'); break;
                    case 'ó': builder.Append('o'); break;
                    case 'ś': builder.Append('s'); break;
                    case 'ź': case 'ż': builder.Append('z'); break;
                    case '’': case '‘': builder.Append('\''); break;
                    default: builder.Append(letter); break;
                }
            }
            return builder.ToString();
        }

        /// <summary>Folded text with punctuation removed and spaces collapsed; apostrophes inside words are kept.</summary>
        public static string Simplify(string text)
        {
            string folded = Fold(text);
            var builder = new StringBuilder(folded.Length);
            for (int i = 0; i < folded.Length; i++)
            {
                char letter = folded[i];
                bool contraction = letter == '\'' && i > 0 && i + 1 < folded.Length
                    && char.IsLetter(folded[i - 1]) && char.IsLetter(folded[i + 1]);
                if (char.IsLetterOrDigit(letter) || contraction) builder.Append(letter);
                else builder.Append(' ');
            }
            return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>
        /// Quoted or reported words are never orders. An apostrophe between letters is a contraction
        /// such as "don't", not a quote.
        /// </summary>
        public static bool HasQuote(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char letter = text[i];
                if ("\"«»“”„".IndexOf(letter) >= 0) return true;
                if (letter != '\'' && letter != '’' && letter != '‘') continue;
                bool contraction = i > 0 && i + 1 < text.Length && char.IsLetter(text[i - 1]) && char.IsLetter(text[i + 1]);
                if (!contraction) return true;
            }
            return false;
        }
    }
}