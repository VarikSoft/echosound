using System;
using UnityEngine;
using EchoSound.Companion;
using EchoSound.Interaction;

namespace EchoSound.Editor.Tests
{
    public static class QuickDoorChecks
    {
        public static void Validate()
        {
            Parse("Открой дверь.", InteractionAction.Open, QuickDoorCommand.Direction.Facing);
            Parse("Откройте дверь передо мной.", InteractionAction.Open, QuickDoorCommand.Direction.Front);
            Parse("Пожалуйста. Откройте дверь передо мной.", InteractionAction.Open, QuickDoorCommand.Direction.Front);
            Require(!QuickDoorCommand.TryParse("Открою дверь передо мной.", out _), "First-person speech cannot open a door.");
            Parse("Закрой дверь справа, пожалуйста!", InteractionAction.Close, QuickDoorCommand.Direction.Right);
            Parse("Please open the nearest door.", InteractionAction.Open, QuickDoorCommand.Direction.Nearest);
            Parse("Close the door behind me.", InteractionAction.Close, QuickDoorCommand.Direction.Back);
            Parse("Otwórz drzwi po lewej.", InteractionAction.Open, QuickDoorCommand.Direction.Left);
            Parse("Zamknij drzwi za mną.", InteractionAction.Close, QuickDoorCommand.Direction.Back);
            Require(QuickDoorCommand.TryParseStatus("Дверь слева открыта", out var side) && side == QuickDoorCommand.Direction.Left,
                "Recognize a status question even when speech recognition drops the question mark.");
            Require(QuickDoorCommand.TryParseStatus("Открыта ли дверь передо мной?", out side) && side == QuickDoorCommand.Direction.Front,
                "Recognize a question beginning with the expected state.");
            Require(QuickDoorCommand.TryParseStatus("Is the door behind me closed?", out side) && side == QuickDoorCommand.Direction.Back,
                "Recognize an English status question.");
            Require(QuickDoorCommand.TryParseStatus("Czy drzwi po prawej stronie są otwarte?", out side) && side == QuickDoorCommand.Direction.Right,
                "Recognize a Polish status question.");
            Require(!QuickDoorCommand.TryParseStatus("Дверь слева открыта справа?", out _), "Reject contradictory status directions.");
            Require(QuickDoorCommand.TryParseOpenAll("Иди за мной и открой все двери.", out bool escort) && escort,
                "A spoken follow-and-open order enables door escort.");
            Require(QuickDoorCommand.TryParseOpenAll("Follow me and open all the doors on the way", out escort) && escort,
                "English escort phrase.");
            Require(QuickDoorCommand.TryParseOpenAll("Otwórz wszystkie drzwi", out escort) && !escort,
                "A standalone order opens nearby doors.");
            foreach (string text in new[] {
                "Короче, иди за мной. И открывай все двери передом ногей.",
                "Иди за мной. Открывай все дверью передо мной.",
                "Короче, иди за мной. И открывай все двере передо мной.",
                "Иди за мной. Открывай все двери передо мной.",
                "Короче иди за мной открывай все двери передо мной",
                "Слушай, иди за мной, открывай все двери по дороге.",
                "Follow me. Open all the doors on the way.",
                "Idź za mną. Otwieraj wszystkie drzwi po drodze."
            })
                Require(QuickDoorCommand.TryParseOpenAll(text, out escort) && escort, "Start escort from both complete clauses: " + text);
            foreach (string text in new[] {
                "Не иди за мной. И открывай все двери передо мной.",
                "Иди за мной. Не открывай все двери передо мной.",
                "Если я скажу «иди за мной», открывай все двери передо мной.",
                "Иди за мной. Открывай все двери передо мной, когда скажу.",
                "Иди за мной? Открывай все двери передо мной.",
                "Иди за мной. «Открывай все двери передо мной».",
                "Иди за мной. Открывай все двери передо мной. Стой.",
                "Иди за мной. Открывай все двери слева.",
                "Открывай все дверью передо мной."
            })
                Require(!QuickDoorCommand.TryParseOpenAll(text, out _), "Do not start an escort or drop a constraint: " + text);
            Require(!QuickDoorCommand.TryParseOpenAll("Не иди за мной и открой все двери", out _)
                && !QuickDoorCommand.TryParseOpenAll("Если я скажу, открой все двери", out _), "Reject negated or conditional bulk commands.");
            foreach (string text in new[] { "Не открой дверь", "Не открывай дверь", "Открой дверь?", "Дверь открыта?", "Don't open the door", "Nie otwieraj drzwi",
                "Если я подойду открой дверь", "Открой дверь когда я скажу", "Открой дверь и стой", "Скажи открой дверь", "Я открою дверь",
                "open door then close it", "открой ближайшую дверь справа", "\"открой дверь\"" })
                Require(!QuickDoorCommand.TryParse(text, out _), "Unsafe or unsupported phrase: " + text);

            var host = new GameObject("Door selection checks");
            try
            {
                Transform viewer = new GameObject("viewer").transform;
                viewer.SetParent(host.transform);
                Door open = MakeDoor(host.transform, "open", new Vector3(0, 0, 2));
                Require(open.TryUse(open.gameObject.AddComponent<Interactor>(), InteractionAction.Open, out _), "Prepare open door.");
                Door closed = MakeDoor(host.transform, "closed", new Vector3(0, 0, 5));
                Door back = MakeDoor(host.transform, "back", new Vector3(0, 0, -3));
                var objects = new Interactable[] { open, closed, back };
                QuickDoorCommand.TryParse("Открой дверь", out var request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out var stateResult, readState: true) == open
                    && stateResult == QuickDoorCommand.Resolution.Found, "A state question selects the nearest suitable door regardless of its state.");
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out var result) == closed && result == QuickDoorCommand.Resolution.Found,
                    "Skip already open door; don't select closed door behind player.");
                Require(QuickDoorCommand.Resolve(objects, viewer, request, open, out result) == open && result == QuickDoorCommand.Resolution.AlreadyDone,
                    "Aimed open door is acknowledged; never silently switch targets.");
                QuickDoorCommand.TryParse("Закрой дверь", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out result) == open, "Close selects open door.");
                QuickDoorCommand.TryParse("Открой дверь за мной", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, open, out result) == back, "Explicit direction overrides gaze.");
                viewer.rotation = Quaternion.Euler(0, 90, 0);
                QuickDoorCommand.TryParse("Открой дверь слева", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out result) == closed, "Directions rotate with player.");
                viewer.rotation = Quaternion.identity;
                Door equal = MakeDoor(host.transform, "equal", new Vector3(1, 0, 5));
                QuickDoorCommand.TryParse("Открой дверь", out request);
                Require(QuickDoorCommand.Resolve(new Interactable[] { closed, equal }, viewer, request, null, out result) == null
                    && result == QuickDoorCommand.Resolution.Ambiguous, "Ask about similarly close doors.");
                Require(QuickDoorCommand.Resolve(new Interactable[] { back }, viewer, request, closed, out result) == null
                    && result == QuickDoorCommand.Resolution.Missing, "Never use an unperceived aimed door or guess behind the player.");
                Require(QuickDoorCommand.Resolve(objects, null, request, null, out result) == null
                    && result == QuickDoorCommand.Resolution.NoPlayer, "Missing player is handled.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            Debug.Log("Quick door checks: all passed.");
        }

        private static Door MakeDoor(Transform parent, string id, Vector3 position)
        {
            var item = new GameObject(id);
            item.transform.SetParent(parent);
            item.transform.position = position;
            Door door = item.AddComponent<Door>();
            door.id = id;
            return door;
        }

        private static void Parse(string text, InteractionAction action, QuickDoorCommand.Direction side)
        {
            Require(QuickDoorCommand.TryParse(text, out var request) && request.Action == action && request.Side == side, text);
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Quick door check: " + message);
        }
    }
}
