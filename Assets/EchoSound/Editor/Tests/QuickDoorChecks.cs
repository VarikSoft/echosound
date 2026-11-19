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
            Parse("Open the door.", InteractionAction.Open, QuickDoorCommand.Direction.Facing);
            Parse("Otwórz drzwi.", InteractionAction.Open, QuickDoorCommand.Direction.Facing);
            Parse("Otworz drzwi przede mna", InteractionAction.Open, QuickDoorCommand.Direction.Front);
            Parse("Proszę. Otwórz drzwi przede mną.", InteractionAction.Open, QuickDoorCommand.Direction.Front);
            Parse("Close the door ahead.", InteractionAction.Close, QuickDoorCommand.Direction.Front);
            Parse("Zamknij drzwi z przodu.", InteractionAction.Close, QuickDoorCommand.Direction.Front);
            Require(!QuickDoorCommand.TryParse("Otworzę drzwi przede mną.", out _), "First-person speech cannot open a door.");
            Require(!QuickDoorCommand.TryParse("I'll open the door in front of me.", out _), "First-person English speech cannot open a door.");
            Parse("Zamknij drzwi po prawej, proszę!", InteractionAction.Close, QuickDoorCommand.Direction.Right);
            Parse("Zamknij drzwi z prawej strony.", InteractionAction.Close, QuickDoorCommand.Direction.Right);
            Parse("Please open the nearest door.", InteractionAction.Open, QuickDoorCommand.Direction.Nearest);
            Parse("Otwórz najbliższe drzwi.", InteractionAction.Open, QuickDoorCommand.Direction.Nearest);
            Parse("Close the door behind me.", InteractionAction.Close, QuickDoorCommand.Direction.Back);
            Parse("Shut the door on the left.", InteractionAction.Close, QuickDoorCommand.Direction.Left);
            Parse("Open the left door.", InteractionAction.Open, QuickDoorCommand.Direction.Left);
            Parse("Otwórz drzwi po lewej.", InteractionAction.Open, QuickDoorCommand.Direction.Left);
            Parse("Otwórz lewe drzwi.", InteractionAction.Open, QuickDoorCommand.Direction.Left);
            Parse("Zamknij drzwi za mną.", InteractionAction.Close, QuickDoorCommand.Direction.Back);
            Parse("zamknij drzwi z tylu", InteractionAction.Close, QuickDoorCommand.Direction.Back);

            Status("Czy drzwi po lewej są otwarte", QuickDoorCommand.Direction.Left, "Recognize a status question even when speech recognition drops the question mark.");
            Status("Is the door in front of me closed?", QuickDoorCommand.Direction.Front, "Recognize an English status question.");
            Status("Is the door behind me closed?", QuickDoorCommand.Direction.Back, "Recognize a door behind the player.");
            Status("Czy drzwi po prawej stronie są otwarte?", QuickDoorCommand.Direction.Right, "Recognize a Polish status question.");
            Status("Czy lewe drzwi są zamknięte?", QuickDoorCommand.Direction.Left, "Recognize a side named before the door.");
            Status("czy drzwi z przodu sa otwarte", QuickDoorCommand.Direction.Front, "Recognize Polish typed without diacritics.");
            Status("Is the door open?", QuickDoorCommand.Direction.Facing, "Without a side, ask about the door the player faces.");
            Require(!QuickDoorCommand.TryParseStatus("Is the left door on my right open?", out _), "Reject contradictory status directions.");
            Require(!QuickDoorCommand.TryParseStatus("Czy lewe drzwi po prawej są otwarte?", out _), "Reject contradictory Polish status directions.");
            Require(!QuickDoorCommand.TryParseStatus("Open the door.", out _), "An order is not a status question.");

            Require(QuickDoorCommand.TryParseOpenAll("Idź za mną i otwórz wszystkie drzwi.", out bool escort) && escort,
                "A spoken follow-and-open order enables door escort.");
            Require(QuickDoorCommand.TryParseOpenAll("Follow me and open all the doors on the way", out escort) && escort,
                "English escort phrase.");
            Require(QuickDoorCommand.TryParseOpenAll("Otwórz wszystkie drzwi", out escort) && !escort, "A standalone order opens nearby doors.");
            Require(QuickDoorCommand.TryParseOpenAll("Open all the doors.", out escort) && !escort, "A standalone English order opens nearby doors.");
            foreach (string text in new[] {
                "Follow me. Open all the doors on the way.",
                "Idź za mną. Otwieraj wszystkie drzwi po drodze.",
                "Chodź za mną, otwieraj drzwi po drodze.",
                "Okay, follow me and open every door.",
                "Open all the doors and follow me.",
                "Za mną! Pootwieraj drzwi po drodze.",
                "Idz za mna i otworz wszystkie drzwi"
            })
                Require(QuickDoorCommand.TryParseOpenAll(text, out escort) && escort, "Start escort from both complete clauses: " + text);
            foreach (string text in new[] {
                "Don't follow me. Open all the doors.",
                "Follow me. Don't open all the doors.",
                "If I say follow me, open all the doors.",
                "Follow me. Open all the doors when I say so.",
                "Follow me? Open all the doors.",
                "Follow me. \"Open all the doors.\"",
                "Follow me. Open all the doors. Stop.",
                "Follow me. Open all the doors on the left.",
                "Idź za mną. Otwórz drzwi z przodu.",
                "Open the door ahead.",
                "Otwórz drzwi.",
                "Nie idź za mną i otwórz wszystkie drzwi",
                "Jeśli powiem, otwórz wszystkie drzwi"
            })
                Require(!QuickDoorCommand.TryParseOpenAll(text, out _), "Do not start an escort or drop a constraint: " + text);
            foreach (string text in new[] { "Don't open the door", "Nie otwieraj drzwi", "Open the door?", "Is the door open?", "Czy drzwi są otwarte?",
                "If I come closer, open the door", "Open the door when I say", "Otwórz drzwi, kiedy powiem", "Jeśli podejdę, otwórz drzwi",
                "Open the door and stop", "Say open the door", "I'll open the door", "Otworzę drzwi", "open door then close it",
                "open the nearest door on the right", "otwórz najbliższe drzwi po prawej", "\"open the door\"", "„Otwórz drzwi”",
                "Open the door on the lieft", "Uchyl drzwi" })
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
                QuickDoorCommand.TryParse("Otwórz drzwi", out var request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out var stateResult, readState: true) == open
                    && stateResult == QuickDoorCommand.Resolution.Found, "A state question selects the nearest suitable door regardless of its state.");
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out var result) == closed && result == QuickDoorCommand.Resolution.Found,
                    "Skip already open door; don't select closed door behind player.");
                Require(QuickDoorCommand.Resolve(objects, viewer, request, open, out result) == open && result == QuickDoorCommand.Resolution.AlreadyDone,
                    "Aimed open door is acknowledged; never silently switch targets.");
                QuickDoorCommand.TryParse("Close the door", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out result) == open, "Close selects open door.");
                QuickDoorCommand.TryParse("Otwórz drzwi za mną", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, open, out result) == back, "Explicit direction overrides gaze.");
                viewer.rotation = Quaternion.Euler(0, 90, 0);
                QuickDoorCommand.TryParse("Open the door on my left", out request);
                Require(QuickDoorCommand.Resolve(objects, viewer, request, null, out result) == closed, "Directions rotate with player.");
                viewer.rotation = Quaternion.identity;
                Door equal = MakeDoor(host.transform, "equal", new Vector3(1, 0, 5));
                QuickDoorCommand.TryParse("Open the door", out request);
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

        private static void Status(string text, QuickDoorCommand.Direction side, string message)
        {
            Require(QuickDoorCommand.TryParseStatus(text, out var direction) && direction == side, message + " " + text);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Quick door check: " + message);
        }
    }
}