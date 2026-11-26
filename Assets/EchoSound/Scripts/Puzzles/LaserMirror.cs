using System.Collections.Generic;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Puzzles
{
    /// <summary>Reflects laser beams off both faces. Turning rotates it by a fixed step around the vertical axis.</summary>
    public sealed class LaserMirror : Interactable
    {
        private static readonly InteractionAction[] actions = { InteractionAction.Turn };

        [Range(1, 180)] public float turnStep = 45;
        [Min(0.5f)] public float standDistance = 1.1f;

        public override string Kind => "mirror";
        public override string State => $"turned to {Mathf.RoundToInt(transform.localEulerAngles.y) % 360} degrees";
        public override IReadOnlyList<InteractionAction> Actions => actions;
        public override IEnumerable<Vector3> StandPoints => PuzzleStandPoints.Around(transform, standDistance);

        protected override void Perform(Interactor user, InteractionAction action)
        {
            transform.Rotate(0, turnStep, 0, Space.Self);
        }
    }
}
