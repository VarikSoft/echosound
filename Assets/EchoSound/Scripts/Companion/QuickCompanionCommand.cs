using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static EchoSound.Companion.CompanionPhrases;

namespace EchoSound.Companion
{
    /// <summary>Recognizes complete movement orders and a few fixed compound orders from CompanionPhrases.</summary>
    public static class QuickCompanionCommand
    {
        public enum DismissalAction { Wait, CloseNearbyDoors }

        [Flags]
        public enum FollowContext { None = 0, Protection = 1, OpenOnPath = 2 }

        internal enum Clause { Unknown, LeadIn, StopFollowing, DontOpenDoors, CloseAll, Follow, Protect, OpenAll }

        private static readonly HashSet<string> Stops = Set(book => book.Stop);
        private static readonly HashSet<string> Follows = Set(book => book.Follow);
        private static readonly HashSet<string> Comes = Set(book => book.Come);
        private static readonly HashSet<string> LeadIns = Set(book => book.LeadIn);
        private static readonly HashSet<string> StopFollowingClauses = Set(book => book.StopFollowing);
        private static readonly HashSet<string> DontOpenClauses = Set(book => book.DontOpenDoors);
        private static readonly HashSet<string> ProtectClauses = Set(book => book.Protect);
        private static readonly Regex Courtesy = new Regex("^" + Any(book => book.Please) + @"\s+|\s+" + Any(book => book.Please) + "$", RegexOptions.CultureInvariant);
        private static readonly Regex PoliteLeadIn = new Regex("^" + Any(book => book.PoliteRequest) + @"\s+", RegexOptions.CultureInvariant);
        private static readonly Regex Conditional = new Regex(@"\b" + Any(book => book.Condition) + @"\b", RegexOptions.CultureInvariant);
        private static readonly Regex Reported = new Regex(@"\b" + Any(book => book.Reported) + @"\b", RegexOptions.CultureInvariant);
        private static readonly Regex[] ClauseBreaks =
        {
            ClauseBreak(English),
            ClauseBreak(Polish)
        };

        private static Regex ClauseBreak(Phrasebook book) => new Regex(
            @"\s*[,;.!…]+\s*(?:" + Any(book, b => b.And) + @"\s+)?|\s+" + Any(book, b => b.And) + @"\s+", RegexOptions.CultureInvariant);

        /// <summary>A complete standalone movement order, such as "stop", "za mną" or "come here".</summary>
        public static bool TryParse(string text, out string action, out string reply)
        {
            action = reply = null;
            if (string.IsNullOrWhiteSpace(text) || CompanionLanguage.HasQuote(text) || text.Contains("?")) return false;
            // "Cześć, za mną!" is still a complete order once the greeting is set aside.
            string value = Courtesy.Replace(CompanionLanguage.Simplify(FastIntentClient.StripGreeting(text)), "").Trim();
            if (Stops.Contains(value))
            {
                action = "wait_here";
                reply = CompanionLanguage.Pick(text, "Waiting here.", "Czekam tutaj.");
            }
            else if (Follows.Contains(value))
            {
                action = "follow_curator";
                reply = CompanionLanguage.Pick(text, "Following you.", "Idę za tobą.");
            }
            else if (Comes.Contains(value))
            {
                action = "go_to";
                reply = CompanionLanguage.Pick(text, "Coming to you.", "Idę do ciebie.");
            }
            return action != null;
        }

        /// <summary>
        /// "Don't follow me", optionally followed by "and close all the doors" or "and don't open doors".
        /// Every clause must be a listed one; anything else is left for interpretation.
        /// </summary>
        public static bool TryParseDismissal(string text, out DismissalAction action)
        {
            action = DismissalAction.Wait;
            if (!TrySplit(text, out List<Clause> clauses) || clauses.Count == 0 || clauses[0] != Clause.StopFollowing) return false;
            if (clauses.Count == 1 || (clauses.Count == 2 && clauses[1] == Clause.DontOpenDoors)) return true;
            if (clauses.Count == 2 && clauses[1] == Clause.CloseAll)
            {
                action = DismissalAction.CloseNearbyDoors;
                return true;
            }
            return false;
        }

        /// <summary>"Follow me" with a separate request to cover the player and/or to open doors on the way.</summary>
        public static bool TryParseFollowWithContext(string text, out FollowContext context)
        {
            context = FollowContext.None;
            if (!TrySplit(text, out List<Clause> clauses) || clauses.Count < 2 || clauses[0] != Clause.Follow) return false;
            var parsed = FollowContext.None;
            for (int i = 1; i < clauses.Count; i++)
            {
                FollowContext part = clauses[i] == Clause.Protect ? FollowContext.Protection
                    : clauses[i] == Clause.OpenAll ? FollowContext.OpenOnPath : FollowContext.None;
                // Unknown, repeated or contradictory clauses are not guessed.
                if (part == FollowContext.None || (parsed & part) != 0) return false;
                parsed |= part;
            }
            context = parsed;
            return true;
        }

        // Splits a compound order into known clauses. Questions, quotes, conditions and reported speech are rejected.
        internal static bool TrySplit(string text, out List<Clause> clauses)
        {
            clauses = new List<Clause>();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 220 || CompanionLanguage.HasQuote(text)) return false;
            string folded = Regex.Replace(CompanionLanguage.Fold(text.Trim()), @"\s+", " ");
            bool polite = PoliteLeadIn.IsMatch(Regex.Replace(folded, @"^\W+", ""));
            if (folded.TrimEnd().EndsWith("?") && !polite) return false;
            if (folded.TrimEnd('?', '.', '!', ' ').Contains("?")) return false;
            if (Conditional.IsMatch(folded) || Reported.IsMatch(folded)) return false;

            Regex breaks = ClauseBreaks[CompanionLanguage.Detect(text) == CompanionLanguage.Language.Polish ? 1 : 0];
            foreach (string part in breaks.Split(folded.TrimEnd('?', '.', '!', '…', ' ')))
            {
                string clause = Courtesy.Replace(CompanionLanguage.Simplify(part), "").Trim();
                if (clauses.Count == 0) clause = PoliteLeadIn.Replace(clause, "");
                if (clause.Length == 0) continue;
                Clause kind = Classify(clause);
                if (kind == Clause.LeadIn) continue;
                if (kind == Clause.Unknown) return false;
                clauses.Add(kind);
            }
            return true;
        }

        private static Clause Classify(string clause)
        {
            if (LeadIns.Contains(clause)) return Clause.LeadIn;
            if (StopFollowingClauses.Contains(clause)) return Clause.StopFollowing;
            if (DontOpenClauses.Contains(clause)) return Clause.DontOpenDoors;
            if (ProtectClauses.Contains(clause)) return Clause.Protect;
            if (Follows.Contains(clause)) return Clause.Follow;
            if (QuickDoorCommand.IsBulk(clause, open: true)) return Clause.OpenAll;
            if (QuickDoorCommand.IsBulk(clause, open: false)) return Clause.CloseAll;
            return Clause.Unknown;
        }

        // Offload an explicit movement suffix after a door order, such as "open the door and then follow me".
        // Keep questions, conditions, negations and unrecognised suffixes intact for interpretation.
        public static bool TrySplitFollowup(string order, out string first, out string followup)
        {
            first = followup = null;
            if (string.IsNullOrWhiteSpace(order) || order.Contains("?") || CompanionLanguage.HasQuote(order)) return false;
            string folded = CompanionLanguage.Fold(order.Trim());
            if (Conditional.IsMatch(folded)) return false;
            if (!QuickDoorCommand.StartsWithDoorVerb(folded)) return false;
            foreach (Match separator in FollowupSeparator.Matches(folded))
            {
                string tail = order.Trim().Substring(separator.Index + separator.Length);
                if (!TryParse(tail, out _, out _)) continue;
                first = order.Trim().Substring(0, separator.Index).Trim().TrimEnd(',');
                followup = tail;
                return true;
            }
            return false;
        }

        private static readonly Regex FollowupSeparator = new Regex(@"(?:\s*,\s*|\s+)" + Any(book => book.Then) + @"\s+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
    }
}
