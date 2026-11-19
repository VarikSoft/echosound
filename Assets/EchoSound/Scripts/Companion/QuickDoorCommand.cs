using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using EchoSound.Interaction;
using static EchoSound.Companion.CompanionPhrases;

namespace EchoSound.Companion
{
    /// <summary>Small, complete door orders only. Unknown wording stays with the language model.</summary>
    public static class QuickDoorCommand
    {
        public enum Direction { Facing, Front, Back, Left, Right, Nearest }
        public enum Resolution { Found, AlreadyDone, Ambiguous, Missing, NoPlayer }
        public struct Request
        {
            public InteractionAction Action;
            public Direction Side;
        }

        private const RegexOptions Options = RegexOptions.CultureInvariant;
        private static readonly Regex[] Orders = { DoorOrder(English), DoorOrder(Polish) };
        private static readonly Regex[] Questions = { DoorQuestion(English), DoorQuestion(Polish) };
        private static readonly Regex[] OpenMany = { Bulk(English, true), Bulk(Polish, true) };
        private static readonly Regex[] CloseMany = { Bulk(English, false), Bulk(Polish, false) };
        private static readonly Regex DoorVerb = new Regex("^" + Any(book => book.Open.Concat(book.Close).ToArray()) + " ", Options);

        private static string Sides(Phrasebook book) => Any(book, b => b.Front.Concat(b.Back).Concat(b.Left).Concat(b.Right).ToArray());
        private static string Adjectives(Phrasebook book) => Any(book, b => b.LeftAdjective.Concat(b.RightAdjective).ToArray());

        // "please open the nearest door", "close the door behind me", "otwórz lewe drzwi", "zamknij drzwi za mną".
        private static Regex DoorOrder(Phrasebook book) => new Regex(
            "^(?:" + Any(book, b => b.Please) + " )?"
            + "(?:(?<open>" + Any(book, b => b.Open) + ")|(?<close>" + Any(book, b => b.Close) + ")) "
            + "(?:" + Any(book, b => b.Determiner) + " )?"
            + "(?:(?<nearest>" + Any(book, b => b.Nearest) + ") |(?<adjective>" + Adjectives(book) + ") )?"
            + Any(book, b => b.Door)
            + "(?: (?<side>" + Sides(book) + "))?"
            + "(?: " + Any(book, b => b.Please) + ")?$", Options);

        // "is the door on my left open", "czy lewe drzwi są zamknięte".
        private static Regex DoorQuestion(Phrasebook book) => new Regex(
            "^" + Any(book, b => b.DoorQuestion) + " "
            + "(?:" + Any(book, b => b.Determiner) + " )?"
            + "(?:(?<adjective>" + Adjectives(book) + ") )?"
            + Any(book, b => b.Door)
            + "(?: (?<side>" + Sides(book) + "))? "
            + Any(book, b => b.DoorIsOpen.Concat(b.DoorIsClosed).ToArray()) + "$", Options);

        // "open all the doors on the way", "close the nearby doors", "otwieraj drzwi po drodze", "pozamykaj drzwi".
        private static Regex Bulk(Phrasebook book, bool open)
        {
            string verb = Any(book, b => open ? b.OpenAll : b.CloseAll);
            string each = Any(book, b => open ? b.OpenEach : b.CloseEach);
            string doors = Any(book, b => b.Doors);
            string anyDoor = Any(book, b => b.Doors.Concat(b.Door).ToArray());
            string adjective = Any(book, b => b.BulkAdjective);
            string scope = Any(book, b => b.DoorScope);
            string the = "(?:" + Any(book, b => b.Determiner) + " )?";
            string target = "(?:" + Any(book, b => b.Quantifier) + " (?:" + adjective + " )?" + anyDoor + "(?: " + scope + ")?"
                + "|" + the + adjective + " " + doors + "(?: " + scope + ")?"
                + "|" + the + doors + " " + scope + ")";
            return new Regex("^(?:" + Any(book, b => b.Please) + " )?"
                + "(?:" + verb + " " + target + "|" + each + " " + the + doors + "(?: " + scope + ")?)"
                + "(?: " + Any(book, b => b.Please) + ")?$", Options);
        }

        /// <summary>Whether a simplified clause asks to open or close several doors at once.</summary>
        internal static bool IsBulk(string simplified, bool open) => (open ? OpenMany : CloseMany).Any(regex => regex.IsMatch(simplified));

        internal static bool StartsWithDoorVerb(string folded) => DoorVerb.IsMatch(folded);

        /// <summary>"Open all the doors" on its own, or together with "follow me" to open doors on the way.</summary>
        public static bool TryParseOpenAll(string text, out bool whileFollowing)
        {
            whileFollowing = false;
            if (!QuickCompanionCommand.TrySplit(text, out var clauses)) return false;
            if (clauses.Count == 1 && clauses[0] == QuickCompanionCommand.Clause.OpenAll) return true;
            if (clauses.Count == 2 && clauses.Contains(QuickCompanionCommand.Clause.Follow) && clauses.Contains(QuickCompanionCommand.Clause.OpenAll))
            {
                whileFollowing = true;
                return true;
            }
            return false;
        }

        /// <summary>A question about one door's state. It reads the state and never operates the door.</summary>
        public static bool TryParseStatus(string text, out Direction direction)
        {
            direction = Direction.Facing;
            if (string.IsNullOrWhiteSpace(text) || CompanionLanguage.HasQuote(text)) return false;
            string value = CompanionLanguage.Simplify(text);
            foreach (Regex question in Questions)
            {
                Match match = question.Match(value);
                if (!match.Success) continue;
                return TryReadDirection(match, out direction);
            }
            return false;
        }

        public static bool TryParse(string text, out Request request)
        {
            request = default;
            if (string.IsNullOrWhiteSpace(text) || text.Contains("?") || CompanionLanguage.HasQuote(text)) return false;
            string value = CompanionLanguage.Simplify(text);
            foreach (Regex order in Orders)
            {
                Match match = order.Match(value);
                if (!match.Success) continue;
                request.Action = match.Groups["open"].Success ? InteractionAction.Open : InteractionAction.Close;
                // Do not silently discard one of two different target constraints.
                if (match.Groups["nearest"].Success)
                {
                    if (match.Groups["side"].Success) return false;
                    request.Side = Direction.Nearest;
                    return true;
                }
                return TryReadDirection(match, out request.Side);
            }
            return false;
        }

        private static bool TryReadDirection(Match match, out Direction direction)
        {
            direction = Direction.Facing;
            Group side = match.Groups["side"], adjective = match.Groups["adjective"];
            // "the left door on my right" names two places for one door.
            if (side.Success && adjective.Success) return false;
            if (side.Success) direction = ParseSide(side.Value);
            else if (adjective.Success) direction = Contains(b => b.LeftAdjective, adjective.Value) ? Direction.Left : Direction.Right;
            return true;
        }

        private static Direction ParseSide(string side)
        {
            if (Contains(b => b.Front, side)) return Direction.Front;
            if (Contains(b => b.Back, side)) return Direction.Back;
            if (Contains(b => b.Left, side)) return Direction.Left;
            if (Contains(b => b.Right, side)) return Direction.Right;
            return Direction.Facing;
        }

        private static readonly (Direction Side, Regex Words)[] SideWords =
        {
            (Direction.Front, Mention(b => b.Front)),
            (Direction.Back, Mention(b => b.Back)),
            (Direction.Left, Mention(b => b.Left)),
            (Direction.Right, Mention(b => b.Right)),
            (Direction.Left, new Regex(@"\b" + Any(b => b.LeftAdjective) + " " + Any(b => b.DoorMention) + @"\b", Options)),
            (Direction.Right, new Regex(@"\b" + Any(b => b.RightAdjective) + " " + Any(b => b.DoorMention) + @"\b", Options)),
            (Direction.Nearest, Mention(b => b.Nearest)),
        };

        private static Regex Mention(System.Func<Phrasebook, string[]> select) => new Regex(@"\b" + Any(select) + @"\b", Options);

        /// <summary>Every distinct door direction named anywhere in a folded clause.</summary>
        public static List<Direction> SidesMentioned(string folded)
        {
            var sides = new List<Direction>();
            foreach (var (side, words) in SideWords)
                if (!sides.Contains(side) && words.IsMatch(folded)) sides.Add(side);
            return sides;
        }

        public static Door Resolve(IEnumerable<Interactable> perceived, Transform viewer, Request request, Door aimed, out Resolution resolution, bool readState = false)
        {
            resolution = Resolution.NoPlayer;
            if (viewer == null) return null;
            var candidates = new List<Door>();
            foreach (Interactable item in perceived)
            {
                if (!(item is Door door)) continue;
                // An explicitly aimed door wins even if it already has the requested state.
                if (request.Side == Direction.Facing && door == aimed)
                {
                    resolution = !readState && HasDesiredState(door, request) ? Resolution.AlreadyDone : Resolution.Found;
                    return door;
                }
                if (MatchesDirection(viewer, door.transform.position, request.Side)) candidates.Add(door);
            }
            resolution = Resolution.Missing;
            if (candidates.Count == 0) return null;
            // Never choose an open door to open when a closed candidate fits the same description.
            if (!readState)
            {
                var actionable = candidates.FindAll(door => !HasDesiredState(door, request));
                if (actionable.Count > 0) candidates = actionable;
            }
            candidates.Sort((a, b) => Vector3.Distance(viewer.position, a.transform.position).CompareTo(Vector3.Distance(viewer.position, b.transform.position)));
            if (candidates.Count > 1)
            {
                float first = Vector3.Distance(viewer.position, candidates[0].transform.position);
                float second = Vector3.Distance(viewer.position, candidates[1].transform.position);
                if (second - first < Mathf.Max(0.75f, first * 0.2f))
                {
                    resolution = Resolution.Ambiguous;
                    return null;
                }
            }
            Door selected = candidates[0];
            resolution = !readState && HasDesiredState(selected, request) ? Resolution.AlreadyDone : Resolution.Found;
            return selected;
        }

        private static bool HasDesiredState(Door door, Request request) => door.IsOpen == (request.Action == InteractionAction.Open);

        private static bool MatchesDirection(Transform viewer, Vector3 point, Direction side)
        {
            if (side == Direction.Nearest) return true;
            Vector3 delta = point - viewer.position;
            Vector3 forward = viewer.forward;
            delta.y = forward.y = 0;
            float angle = Vector3.SignedAngle(forward, delta, Vector3.up);
            switch (side)
            {
                case Direction.Back: return Mathf.Abs(angle) >= 135;
                case Direction.Left: return angle < -45 && angle > -135;
                case Direction.Right: return angle > 45 && angle < 135;
                default: return Mathf.Abs(angle) <= 45;
            }
        }
    }
}
