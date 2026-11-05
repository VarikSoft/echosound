using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EchoSound.Companion;
using EchoSound.Generation;
using EchoSound.Interaction;
using EchoSound.Rooms;

namespace EchoSound.Testing
{
    /// <summary>
    /// Companion test-scene wiring: after each generation, bake a NavMesh, put doors into doorways
    /// and place the companion next to the spawn point. The player uses doors through PlayerInteractor.
    /// </summary>
    public sealed class CompanionPlayground : MonoBehaviour
    {
        public LevelGenerator generator;
        public FirstPersonPlayer player;
        public CompanionBrain companion;
        public Material doorMaterial;
        [Tooltip("Share of doorways that start with a closed door.")]
        [Range(0, 1)] public float closedDoorChance = 0.6f;
        [Tooltip("Keep equal to the companion's NavMeshAgent radius. Pit walkways are 1.6 m wide.")]
        [Min(0.1f)] public float agentRadius = 0.3f;

        private NavMeshData navMeshData;
        private NavMeshDataInstance navMesh;

        private void OnEnable()
        {
            if (generator != null) generator.Generated += PrepareLevel;
        }

        private void OnDisable()
        {
            if (generator != null) generator.Generated -= PrepareLevel;
            if (navMesh.valid) navMesh.Remove();
        }

        private void PrepareLevel(GeneratedLevel level)
        {
            BakeNavMesh(level);
            // Doors appear after baking: a closed door carves the NavMesh instead of being baked in as a wall.
            SpawnDoors(level);
            if (companion != null)
            {
                if (player != null) companion.Body.curator = player.transform;
                // The previous level's doors no longer exist.
                companion.ResetConversation();
                companion.Body.Place(FindPlaceBesideSpawn(level.Spawn));
            }
        }

        private void BakeNavMesh(GeneratedLevel level)
        {
            if (navMesh.valid) navMesh.Remove();
            if (navMeshData != null) Destroy(navMeshData);

            // Only room geometry is collected: the player and the companion are not children of the generator.
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(generator.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            float roomSize = generator.catalog.roomSize;
            var bounds = new Bounds(level.Spawn, Vector3.zero);
            foreach (RoomTemplate room in level.Rooms)
            {
                // Tall enough to include pit bottoms.
                bounds.Encapsulate(new Bounds(room.transform.position, new Vector3(roomSize, 24, roomSize)));
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            navMesh = NavMesh.AddNavMeshData(navMeshData);
        }

        private void SpawnDoors(GeneratedLevel level)
        {
            RoomGraph graph = level.Graph;
            float roomSize = generator.catalog.roomSize;
            // The same seed gives the same open and closed doors.
            var random = new System.Random(generator.seed);
            int doorCount = 0;

            for (int roomIndex = 0; roomIndex < graph.Nodes.Count; roomIndex++)
            {
                RoomGraph.Node node = graph.Nodes[roomIndex];
                for (int side = 0; side < 4; side++)
                {
                    if ((node.Doors & DoorLayout.Bit(side)) == 0) continue;
                    int neighbourIndex = graph.Index[node.Cell + DoorLayout.Steps[side]];
                    if (neighbourIndex <= roomIndex) continue; // Both rooms list the doorway; place one door.

                    Vector2Int step = DoorLayout.Steps[side];
                    var direction = new Vector3(step.x, 0, step.y);
                    Vector3 localPosition = (new Vector3(node.Cell.x, 0, node.Cell.y) + direction * 0.5f) * roomSize;
                    Vector3 position = generator.transform.TransformPoint(localPosition);
                    Quaternion rotation = generator.transform.rotation * Quaternion.LookRotation(direction);

                    RoomTemplate room = level.Rooms[roomIndex];
                    RoomTemplate neighbour = level.Rooms[neighbourIndex];
                    RoomDoor doorway = NearestDoorway(room, position);
                    bool open = random.NextDouble() >= closedDoorChance;

                    doorCount++;
                    Door door = Door.Create("door_" + doorCount, room.transform, position, rotation,
                        doorway != null ? doorway.width : 3, doorway != null ? doorway.height : 2.6f, doorMaterial, open);
                    door.locationHint = $"between room {roomIndex + 1} ({room.displayName}) " +
                        $"and room {neighbourIndex + 1} ({neighbour.displayName})";
                }
            }
        }

        private static RoomDoor NearestDoorway(RoomTemplate room, Vector3 position)
        {
            RoomDoor nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (RoomDoor doorway in room.GetComponentsInChildren<RoomDoor>())
            {
                float distance = Vector3.Distance(doorway.transform.position, position);
                if (distance < nearestDistance)
                {
                    nearest = doorway;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        // The player spawns on the anchor; stand a step away along open floor, not inside the player.
        private static Vector3 FindPlaceBesideSpawn(Vector3 spawn)
        {
            if (!NavMesh.SamplePosition(spawn, out NavMeshHit start, 2f, NavMesh.AllAreas)) return spawn;

            Vector3[] directions = { Vector3.back, Vector3.right, Vector3.left, Vector3.forward };
            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = start.position + direction * 1.5f;
                if (!NavMesh.Raycast(start.position, candidate, out _, NavMesh.AllAreas)) return candidate;
            }
            return start.position;
        }
    }
}
