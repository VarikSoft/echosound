using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Companion
{
    public sealed class FollowCuratorTool : CompanionTool
    {
        public override string Name => "follow_curator";
        public override string Description => "Follow the curator and stay close until another order.";

        public override IEnumerator Run(CompanionBrain companion, JObject arguments, Action<string> report)
        {
            if (companion.Body.curator == null)
            {
                report("There is no curator to follow.");
                yield break;
            }
            companion.Body.Follow();
            report("Now following the curator.");
        }
    }

    public sealed class WaitHereTool : CompanionTool
    {
        public override string Name => "wait_here";
        public override string Description => "Stop moving and wait at the current spot.";

        public override IEnumerator Run(CompanionBrain companion, JObject arguments, Action<string> report)
        {
            companion.Body.Halt();
            report("Stopped. Waiting here.");
            yield break;
        }
    }

    public sealed class GoToTool : CompanionTool
    {
        private const string Curator = "curator";

        public override string Name => "go_to";
        public override string Description =>
            "Walk to an object you perceive, or to the curator (target 'curator' for orders like come here, " +
            "come back, return to me). Finishes when you arrive.";

        public override JObject Parameters(CompanionBrain companion)
        {
            List<string> ids = companion.Perception.Ids();
            ids.Add(Curator);
            return TargetParameter("Object id, or 'curator'.", ids);
        }

        public override IEnumerator Run(CompanionBrain companion, JObject arguments, Action<string> report)
        {
            CompanionAgent body = companion.Body;
            string target = Target(arguments);
            Vector3 destination;
            float arriveDistance;

            if (target == Curator)
            {
                if (body.curator == null)
                {
                    report("There is no curator to walk to.");
                    yield break;
                }
                destination = body.curator.position;
                arriveDistance = 1.8f;
            }
            else
            {
                Interactable item = companion.Perception.Find(target);
                if (item == null)
                {
                    report($"There is no object '{target}' near you.");
                    yield break;
                }
                if (!TryChooseStandPoint(body, item, out destination))
                {
                    report($"Could not reach {target}: no walkable route to it. A closed door may be in the way.");
                    yield break;
                }
                arriveDistance = 0.3f;
            }

            body.Activity = "walking to " + target;
            string problem = null;
            yield return body.WalkTo(destination, arriveDistance, reason => problem = reason);
            if (problem != null)
            {
                body.Halt();
                report($"Could not reach {target}: {problem}.");
                yield break;
            }

            body.Halt("waiting next to " + (target == Curator ? "the curator" : target));
            report(target == Curator ? "Arrived next to the curator." : $"Arrived at {target}.");
        }
    }
}
