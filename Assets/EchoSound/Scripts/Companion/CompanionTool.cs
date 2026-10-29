using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Companion
{
    /// <summary>
    /// One function the LLM may call. To give the companion a new ability, add a subclass
    /// and list it in CompanionBrain.CreateTools().
    /// </summary>
    public abstract class CompanionTool
    {
        /// <summary>Function name the LLM sees, in snake_case.</summary>
        public abstract string Name { get; }
        /// <summary>Tells the LLM when to use the function.</summary>
        public abstract string Description { get; }

        /// <summary>JSON schema of the arguments. Rebuilt for every request, so id lists stay current.</summary>
        public virtual JObject Parameters(CompanionBrain companion)
        {
            return new JObject { ["type"] = "object", ["properties"] = new JObject() };
        }

        /// <summary>Performs the action, possibly over many frames, and reports one short outcome sentence.</summary>
        public abstract IEnumerator Run(CompanionBrain companion, JObject arguments, Action<string> report);

        public JObject Definition(CompanionBrain companion)
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = Name,
                    ["description"] = Description,
                    ["parameters"] = Parameters(companion)
                }
            };
        }

        /// <summary>A single required "target" argument limited to the given ids.</summary>
        protected static JObject TargetParameter(string description, List<string> ids)
        {
            var target = new JObject { ["type"] = "string", ["description"] = description };
            // An enum stops the model from inventing ids; an empty enum would be an invalid schema.
            if (ids.Count > 0) target["enum"] = new JArray(ids);
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["target"] = target },
                ["required"] = new JArray("target")
            };
        }

        protected static string Target(JObject arguments) => (string)arguments["target"] ?? "";

        /// <summary>The nearest stand point the companion can walk to, by route length.</summary>
        protected static bool TryChooseStandPoint(CompanionAgent body, Interactable item, out Vector3 point)
        {
            point = default;
            float shortest = float.MaxValue;
            foreach (Vector3 candidate in item.StandPoints)
            {
                if (body.TryGetRouteLength(candidate, out float length) && length < shortest)
                {
                    shortest = length;
                    point = candidate;
                }
            }
            return shortest < float.MaxValue;
        }
    }
}
