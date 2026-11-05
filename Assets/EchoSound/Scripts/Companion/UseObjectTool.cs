using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Companion
{
    /// <summary>
    /// One function per action and object kind, for example open_door. Generated from the objects
    /// the companion perceives, so a new kind of interactable needs no new companion code.
    /// The companion walks to the object first; then the object's own rules decide, exactly as for the player.
    /// </summary>
    public sealed class UseObjectTool : CompanionTool
    {
        private readonly InteractionAction action;
        private readonly string kind;
        private readonly List<string> targets = new List<string>();

        private UseObjectTool(InteractionAction action, string kind)
        {
            this.action = action;
            this.kind = kind;
        }

        public override string Name => action.Verb() + "_" + kind;
        public override string Description => $"Walk to a {kind} and {action.Verb()} it.";

        public override JObject Parameters(CompanionBrain companion)
        {
            return TargetParameter($"Id of the {kind} to {action.Verb()}.", targets);
        }

        /// <summary>The functions that make sense for these objects, with their ids as allowed targets.</summary>
        public static List<UseObjectTool> CreateFor(IEnumerable<Interactable> perceived)
        {
            var result = new List<UseObjectTool>();
            foreach (Interactable item in perceived)
            {
                foreach (InteractionAction supported in item.Actions)
                {
                    UseObjectTool tool = result.Find(candidate => candidate.action == supported && candidate.kind == item.Kind);
                    if (tool == null)
                    {
                        tool = new UseObjectTool(supported, item.Kind);
                        result.Add(tool);
                    }
                    tool.targets.Add(item.id);
                }
            }
            return result;
        }

        public override IEnumerator Run(CompanionBrain companion, JObject arguments, Action<string> report)
        {
            CompanionAgent body = companion.Body;
            string target = Target(arguments);
            string verb = action.Verb();

            Interactable item = companion.Perception.Find(target);
            if (item == null || item.Kind != kind)
            {
                report($"There is no {kind} '{target}' near you.");
                yield break;
            }
            // Ask the object's rules before walking, so the companion does not walk over for nothing.
            if (!item.CanUse(action, out string problem))
            {
                report($"Cannot {verb} {target}: {problem}.");
                yield break;
            }

            if (!item.IsInReach(body.transform.position))
            {
                // A closed door splits the NavMesh, so only the stand point on the companion's side is reachable.
                if (!TryChooseStandPoint(body, item, out Vector3 standPoint))
                {
                    report($"Could not reach {target}: no walkable route to it.");
                    yield break;
                }

                body.Activity = $"walking to {target} to {verb} it";
                string walkProblem = null;
                yield return body.WalkTo(standPoint, 0.3f, reason => walkProblem = reason);
                if (walkProblem != null)
                {
                    body.Halt();
                    report($"Could not reach {target}: {walkProblem}.");
                    yield break;
                }
            }

            body.Face(item.transform.position);
            if (!item.TryUse(companion.Hands, action, out problem))
            {
                body.Halt("waiting next to " + target);
                report($"Could not {verb} {target}: {problem}.");
                yield break;
            }
            while (item.IsBusy) yield return null;
            // Give the NavMesh a frame to drop or add a door's carved hole before the next function plans a route.
            yield return null;

            body.Halt("waiting next to " + target);
            report($"{target} is now {item.State}.");
        }
    }
}
