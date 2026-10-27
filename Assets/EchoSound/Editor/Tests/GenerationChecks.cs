using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoSound.Generation;
using EchoSound.Rooms;

namespace EchoSound.Editor.Tests
{
    /// <summary>Layout checks and physical traversal checks, kept outside runtime code.</summary>
    public static class GenerationChecks
    {
        [MenuItem("EchoSound/Checks/Validate 1000 layouts")]
        public static void ValidateLayouts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomCatalog>("Assets/EchoSound/Settings/DefaultRooms.asset");
            catalog.ValidateOrThrow();

            for (int seed = -500; seed < 500; seed++)
            {
                int count = 2 + Math.Abs(seed) % 63;
                float loopChance = Math.Abs(seed) % 3 * 0.5f;
                var graph = new RoomGraph(seed, count, loopChance);
                var repeated = new RoomGraph(seed, count, loopChance);
                int[] distances = graph.Distances();
                Require(graph.Nodes.Count == count, "Wrong room count.");

                for (int index = 0; index < count; index++)
                {
                    RoomGraph.Node node = graph.Nodes[index];
                    Require(distances[index] >= 0, "Disconnected room.");
                    Require(node.Cell == repeated.Nodes[index].Cell && node.Doors == repeated.Nodes[index].Doors, "Non-deterministic layout.");

                    RoomCatalog.Match match = catalog.Pick(node.Doors, new System.Random(seed));
                    DoorMask prefabDoors = match.Prefab.GetComponent<RoomTemplate>().Doors;
                    Require(DoorLayout.Rotate(prefabDoors, match.Turns) == node.Doors, "Incorrect prefab rotation.");

                    for (int side = 0; side < 4; side++)
                    {
                        if ((node.Doors & DoorLayout.Bit(side)) == 0) continue;
                        Require(graph.Index.TryGetValue(node.Cell + DoorLayout.Steps[side], out int neighbour), "Door opens into empty space.");
                        Require((graph.Nodes[neighbour].Doors & DoorLayout.Bit(DoorLayout.Opposite(side))) != 0, "Door has no matching neighbour.");
                    }
                }
            }
            Debug.Log("Generation checks: 1000 layouts passed.");
        }

        [MenuItem("EchoSound/Checks/Validate prefab walkways")]
        public static void ValidatePrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomCatalog>("Assets/EchoSound/Settings/DefaultRooms.asset");
            catalog.ValidateOrThrow();
            foreach (RoomCatalog.Entry entry in catalog.rooms)
            {
                // Use an isolated physics scene so objects in the user's scene cannot affect results.
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject instance = UnityEngine.Object.Instantiate(entry.prefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.transform.position = Vector3.zero;
                    Physics.SyncTransforms();
                    CheckWalkways(instance.GetComponent<RoomTemplate>(), scene.GetPhysicsScene());
                }
                finally
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            Debug.Log("Generation checks: every prefab anchor connects to every doorway, including pit walkways.");
        }

        private static void CheckWalkways(RoomTemplate room, PhysicsScene physics)
        {
            if (room.displayName == "Bridge" || room.displayName == "PitWalkways" || room.displayName == "Zigzag")
            {
                Vector3 pitProbe = room.displayName == "Bridge" ? new Vector3(3, 1, 0) : Vector3.up;
                Require(physics.Raycast(pitProbe, Vector3.down, out RaycastHit hit, 20) && hit.point.y < -7,
                    room.name + ": expected a physical pit below the walkways.");
            }
            const float spacing = 0.5f;
            float first = -room.footprint / 2 + spacing;
            int size = Mathf.RoundToInt(room.footprint / spacing) - 1;
            var walkable = new bool[size, size];
            var reached = new bool[size, size];
            var overlaps = new Collider[8];

            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    Vector3 point = new Vector3(first + x * spacing, 0, first + z * spacing);
                    bool hasFloor = physics.Raycast(point + Vector3.up * 0.3f, Vector3.down, out _, 0.6f);
                    int obstacles = physics.OverlapCapsule(point + Vector3.up * 0.4f,
                        point + Vector3.up * 1.45f, 0.3f, overlaps);
                    walkable[x, z] = hasFloor && obstacles == 0;
                }
            }

            Vector2Int ToGrid(Vector3 point) => new Vector2Int(
                Mathf.RoundToInt((point.x - first) / spacing), Mathf.RoundToInt((point.z - first) / spacing));
            Vector2Int start = ToGrid(room.AnchorPosition);
            Require(start.x >= 0 && start.y >= 0 && start.x < size && start.y < size, room.name + ": anchor outside footprint.");
            Require(walkable[start.x, start.y], room.name + ": anchor has no safe floor.");
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            reached[start.x, start.y] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in DoorLayout.Steps)
                {
                    Vector2Int next = current + step;
                    if (next.x < 0 || next.y < 0 || next.x >= size || next.y >= size) continue;
                    if (reached[next.x, next.y] || !walkable[next.x, next.y]) continue;
                    reached[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }

            foreach (RoomDoor door in room.GetComponentsInChildren<RoomDoor>())
            {
                Vector3 insideDoor = DoorLayout.Position(door.side, room.footprint - 1);
                Vector2Int cell = ToGrid(insideDoor);
                Require(reached[cell.x, cell.y], room.name + ": unreachable door " + door.side);
            }

            // Planned interaction and encounter locations must also be accessible on foot.
            foreach (RoomMarker marker in room.GetComponentsInChildren<RoomMarker>())
            {
                Vector2Int cell = ToGrid(marker.transform.position);
                Require(cell.x >= 0 && cell.y >= 0 && cell.x < size && cell.y < size,
                    room.name + ": marker outside the room: " + marker.name);
                Require(reached[cell.x, cell.y], room.name + ": unreachable marker: " + marker.name);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
