using System.Collections.Generic;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Generation
{
    /// <summary>
    /// Expands only a few grid boundaries into long links. Shared axis offsets keep adjacent
    /// rooms aligned and prevent overlaps while most connections stay door-to-door.
    /// </summary>
    public sealed class CorridorLayout
    {
        private sealed class Boundary
        {
            public Vector2Int Key;
            public int Links;
        }
        private readonly HashSet<Vector2Int> expanded = new HashSet<Vector2Int>();
        private readonly float roomSize, extraLength;
        public int ConnectionCount { get; private set; }
        public int LongConnectionCount { get; private set; }

        public CorridorLayout(RoomGraph graph, int seed, float size, int modules, float fraction)
        {
            roomSize = size;
            extraLength = size * Mathf.Clamp(modules, 0, 4);
            var boundaries = new List<Boundary>();
            var lookup = new Dictionary<Vector2Int, Boundary>();
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                var node = graph.Nodes[index];
                for (int side = 0; side < 4; side++)
                {
                    if ((node.Doors & DoorLayout.Bit(side)) == 0) continue;
                    if (graph.Index[node.Cell + DoorLayout.Steps[side]] <= index) continue;
                    ConnectionCount++;
                    var key = BoundaryKey(node.Cell, side);
                    if (!lookup.TryGetValue(key, out Boundary boundary))
                    {
                        boundary = new Boundary { Key = key };
                        lookup.Add(key, boundary);
                        boundaries.Add(boundary);
                    }
                    boundary.Links++;
                }
            }
            if (extraLength <= 0) return;
            int budget = Mathf.RoundToInt(ConnectionCount * Mathf.Clamp01(fraction));
            var random = new System.Random(unchecked(seed ^ 0x34ab19));
            for (int i = boundaries.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                var swap = boundaries[i]; boundaries[i] = boundaries[other]; boundaries[other] = swap;
            }
            foreach (Boundary boundary in boundaries)
            {
                if (boundary.Links > budget - LongConnectionCount) continue;
                expanded.Add(boundary.Key);
                LongConnectionCount += boundary.Links;
            }
        }

        private static Vector2Int BoundaryKey(Vector2Int cell, int side)
        {
            Vector2Int neighbour = cell + DoorLayout.Steps[side];
            return side == 1 || side == 3
                ? new Vector2Int(Mathf.Min(cell.x, neighbour.x), 0)
                : new Vector2Int(Mathf.Min(cell.y, neighbour.y), 1);
        }

        public bool HasCorridor(Vector2Int cell, int side) => expanded.Contains(BoundaryKey(cell, side));
        public Vector3 Position(Vector2Int cell) => new Vector3(AxisPosition(cell.x, 0), 0, AxisPosition(cell.y, 1));

        private float AxisPosition(int cell, int axis)
        {
            float result = cell * roomSize;
            foreach (Vector2Int boundary in expanded)
            {
                if (boundary.y != axis) continue;
                if (cell > 0 && boundary.x >= 0 && boundary.x < cell) result += extraLength;
                if (cell < 0 && boundary.x >= cell && boundary.x < 0) result -= extraLength;
            }
            return result;
        }
    }
}
