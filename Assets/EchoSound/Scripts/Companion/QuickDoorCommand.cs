using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using EchoSound.Interaction;

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

        private static readonly Regex Russian = new Regex(@"^(?:пожалуйста )?(открой|откройте|открою|закрой|закройте|закрою) (ближайшую |эту )?дверь(?: (передо мной|перед мной|впереди|за мной|позади меня|сзади|слева|справа|слева от меня|справа от меня))?(?: пожалуйста)?$");
        private static readonly Regex English = new Regex(@"^(?:please )?(open|close) (?:the |this )?(nearest )?door(?: (in front of me|in front|behind me|on my left|on my right|to my left|to my right))?(?: please)?$");
        private static readonly Regex Polish = new Regex(@"^(?:proszę |prosze )?(otwórz|otworz|zamknij) (najbliższe |najblizsze |te )?drzwi(?: (przede mną|przede mna|za mną|za mna|po lewej|po prawej))?(?: proszę| prosze)?$");

        public static bool TryParse(string text, out Request request)
        {
            request = default;
            if (string.IsNullOrWhiteSpace(text) || text.Contains("?")) return false;
            string value = Regex.Replace(text.Trim().TrimEnd('.', '!', '…').ToLowerInvariant().Replace(',', ' '), @"\s+", " ").Trim();
            Match match = Russian.Match(value);
            if (!match.Success) match = English.Match(value);
            if (!match.Success) match = Polish.Match(value);
            if (!match.Success) return false;
            string verb = match.Groups[1].Value;
            request.Action = verb.StartsWith("откр") || verb == "open" || verb.StartsWith("otw") ? InteractionAction.Open : InteractionAction.Close;
            request.Side = Direction.Facing;
            string adjective = match.Groups[2].Value.Trim();
            if (adjective == "ближайшую" || adjective == "nearest" || adjective == "najbliższe" || adjective == "najblizsze")
                request.Side = Direction.Nearest;
            string side = match.Groups[3].Value;
            // Do not silently discard one of two different target constraints.
            if (request.Side == Direction.Nearest && side.Length > 0) return false;
            switch (side)
            {
                case "передо мной": case "перед мной": case "впереди": case "in front of me": case "in front": case "przede mną": case "przede mna": request.Side = Direction.Front; break;
                case "за мной": case "позади меня": case "сзади": case "behind me": case "za mną": case "za mna": request.Side = Direction.Back; break;
                case "слева": case "слева от меня": case "on my left": case "to my left": case "po lewej": request.Side = Direction.Left; break;
                case "справа": case "справа от меня": case "on my right": case "to my right": case "po prawej": request.Side = Direction.Right; break;
            }
            return true;
        }

        public static Door Resolve(IEnumerable<Interactable> perceived, Transform viewer, Request request, Door aimed, out Resolution resolution)
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
                    resolution = HasDesiredState(door, request) ? Resolution.AlreadyDone : Resolution.Found;
                    return door;
                }
                if (MatchesDirection(viewer, door.transform.position, request.Side)) candidates.Add(door);
            }
            resolution = Resolution.Missing;
            if (candidates.Count == 0) return null;
            // Never choose an open door to open when a closed candidate fits the same description.
            var actionable = candidates.FindAll(door => !HasDesiredState(door, request));
            if (actionable.Count > 0) candidates = actionable;
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
            resolution = HasDesiredState(selected, request) ? Resolution.AlreadyDone : Resolution.Found;
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
