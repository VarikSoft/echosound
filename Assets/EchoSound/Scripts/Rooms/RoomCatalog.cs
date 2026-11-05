using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Rooms
{
    /// <summary>Available room prefabs and their selection settings. Contains no scene-generation logic.</summary>
    [CreateAssetMenu(menuName = "EchoSound/Room catalog")]
    public sealed class RoomCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Drag a room PREFAB from the Project window. Its root must have RoomTemplate.")]
            public GameObject prefab;
            [Range(0, 100)] public int weight = 1;
            public bool allowRotation = true;
        }

        [Min(4)] public float roomSize = 18;
        public List<Entry> rooms = new List<Entry>();

        public readonly struct Match
        {
            public readonly GameObject Prefab;
            public readonly int Turns;

            public Match(GameObject prefab, int turns)
            {
                Prefab = prefab;
                Turns = turns;
            }
        }

        public Match Pick(DoorMask requiredDoors, System.Random random)
        {
            var candidates = new List<Match>();
            var weights = new List<int>();
            int totalWeight = 0;

            foreach (Entry entry in rooms)
            {
                if (entry == null || entry.prefab == null || entry.weight <= 0) continue;
                RoomTemplate template = entry.prefab.GetComponent<RoomTemplate>();
                if (template == null) continue;

                List<int> rotations = FindMatchingRotations(template.Doors, requiredDoors, entry.allowRotation);
                if (rotations.Count == 0) continue;

                // A symmetric prefab receives its weight once, regardless of matching rotation count.
                int selectedRotation = rotations[random.Next(rotations.Count)];
                candidates.Add(new Match(entry.prefab, selectedRotation));
                int weight = Mathf.Clamp(entry.weight, 1, 100);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (totalWeight == 0)
            {
                throw new InvalidOperationException($"Catalog '{name}' has no prefab for doors: {requiredDoors}.");
            }

            int choice = random.Next(totalWeight);
            for (int index = 0; index < candidates.Count; index++)
            {
                choice -= weights[index];
                if (choice < 0) return candidates[index];
            }
            return candidates[candidates.Count - 1];
        }

        private static List<int> FindMatchingRotations(DoorMask available, DoorMask required, bool allowRotation)
        {
            var rotations = new List<int>();
            int rotationCount = allowRotation ? 4 : 1;
            for (int turns = 0; turns < rotationCount; turns++)
            {
                if (DoorLayout.Rotate(available, turns) == required) rotations.Add(turns);
            }
            return rotations;
        }

        public void ValidateOrThrow()
        {
            foreach (Entry entry in rooms)
            {
                if (entry == null || entry.weight <= 0) continue;
                if (entry.prefab == null) throw new InvalidOperationException("A catalog entry has no room prefab.");
                if (entry.prefab.scene.IsValid()) throw new InvalidOperationException("Use a prefab asset, not a scene object.");

                RoomTemplate template = entry.prefab.GetComponent<RoomTemplate>();
                if (template == null) throw new InvalidOperationException(entry.prefab.name + " needs RoomTemplate on its root.");
                if (!template.Validate(roomSize, out string error))
                {
                    throw new InvalidOperationException(entry.prefab.name + ": " + error);
                }
            }

            // The five basic shapes, with rotations enabled, cover all fifteen non-empty masks.
            for (int mask = 1; mask <= 15; mask++) Pick((DoorMask)mask, new System.Random(0));
        }
    }
}
