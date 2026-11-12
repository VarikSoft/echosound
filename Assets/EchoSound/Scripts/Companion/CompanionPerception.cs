using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using EchoSound.Interaction;

namespace EchoSound.Companion
{
    /// <summary>
    /// What the companion knows about the world, written as plain text for the LLM.
    /// Placeholder for fair perception: today it knows every interactable within a radius;
    /// later it should know only what echo pulses, cameras and hearing reveal.
    /// </summary>
    [RequireComponent(typeof(CompanionAgent))]
    public sealed class CompanionPerception : MonoBehaviour
    {
        [Min(1)] public float radius = 30;
        [Min(1)] public int maxObjects = 12;

        private CompanionAgent body;

        private void Awake()
        {
            body = GetComponent<CompanionAgent>();
        }

        /// <summary>Known objects, nearest first.</summary>
        public List<Interactable> Perceived()
        {
            var result = new List<Interactable>();
            foreach (Interactable item in Interactable.Active)
            {
                if (Vector3.Distance(item.transform.position, transform.position) <= radius) result.Add(item);
            }
            result.Sort((first, second) => Distance(first).CompareTo(Distance(second)));
            if (result.Count > maxObjects) result.RemoveRange(maxObjects, result.Count - maxObjects);
            return result;
        }

        /// <summary>Ids of known objects, used to restrict a tool's target argument.</summary>
        public List<string> Ids()
        {
            var ids = new List<string>();
            foreach (Interactable item in Perceived()) ids.Add(item.id);
            return ids;
        }

        /// <summary>Finds a known object. Objects the companion does not perceive cannot be used.</summary>
        public Interactable Find(string id)
        {
            foreach (Interactable item in Perceived())
            {
                if (item.id == id) return item;
            }
            return null;
        }

        public string Describe()
        {
            var text = new StringBuilder();
            text.Append("You: ").Append(body.Activity).AppendLine(".");

            Transform curator = body.curator;
            if (curator != null)
            {
                text.Append("Curator: ").Append(Meters(Vector3.Distance(curator.position, transform.position))).AppendLine(" from you.");
            }

            List<Interactable> perceived = Perceived();
            if (perceived.Count == 0)
            {
                text.AppendLine("You do not perceive any objects nearby.");
                return text.ToString();
            }

            text.AppendLine("Objects you perceive:");
            foreach (Interactable item in perceived)
            {
                text.Append("- ").Append(item.id).Append(": ").Append(item.Kind).Append(", ").Append(item.State).Append('.');
                if (curator != null)
                {
                    text.Append(" Relative to the curator: ").Append(Direction(curator, item.transform.position))
                        .Append(", ").Append(Meters(Vector3.Distance(curator.position, item.transform.position))).Append('.');
                }
                text.Append(" From you: ").Append(Meters(Distance(item))).Append('.');
                if (!string.IsNullOrEmpty(item.locationHint)) text.Append(" Location: ").Append(item.locationHint).Append('.');
                text.AppendLine();
            }
            return text.ToString();
        }

        private float Distance(Interactable item) => Vector3.Distance(item.transform.position, transform.position);

        // Lets the LLM resolve the curator's "in front of me", "behind me", "on my left".
        private static string Direction(Transform viewer, Vector3 point)
        {
            Vector3 offset = point - viewer.position;
            Vector3 forward = viewer.forward;
            offset.y = 0;
            forward.y = 0;
            if (offset.sqrMagnitude < 0.25f) return "right here";

            float angle = Vector3.SignedAngle(forward, offset, Vector3.up);
            if (Mathf.Abs(angle) <= 45) return "in front";
            if (Mathf.Abs(angle) >= 135) return "behind";
            return angle > 0 ? "on the right" : "on the left";
        }

        private static string Meters(float distance) => distance.ToString("0.0", CultureInfo.InvariantCulture) + " m";
    }
}
