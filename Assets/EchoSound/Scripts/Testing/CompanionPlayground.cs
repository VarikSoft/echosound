using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EchoSound.Companion;
using EchoSound.Generation;
using EchoSound.Interaction;
using EchoSound.Puzzles;
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
            NameRoomObjects(level);
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
                    Vector3 direction = generator.transform.rotation * new Vector3(step.x, 0, step.y);
                    RoomTemplate room = level.Rooms[roomIndex];
                    RoomTemplate neighbour = level.Rooms[neighbourIndex];

                    // Rooms may be far apart with a corridor between them, so use their real doorways, not grid cells.
                    // One door per connection: in the neighbour's doorway if that is a locked puzzle exit, otherwise in this room's.
                    RoomDoor ownDoorway = DoorwayFacing(room, direction, roomSize);
                    RoomDoor neighbourDoorway = DoorwayFacing(neighbour, -direction, roomSize);
                    bool inNeighbour = ownDoorway == null || (LockOf(ownDoorway) == null && LockOf(neighbourDoorway) != null);
                    RoomDoor doorway = inNeighbour ? neighbourDoorway : ownDoorway;
                    RoomTemplate owner = inNeighbour ? neighbour : room;
                    bool open = random.NextDouble() >= closedDoorChance;
                    if (doorway == null)
                    {
                        Debug.LogWarning($"Room {owner.name} has no doorway towards its neighbour; no door placed.", owner);
                        continue;
                    }
                    DoorwayLock exitLock = LockOf(doorway);
                    // A one-sided puzzle room has a door only at its locked exit, not at its entrance.
                    if (exitLock == null && (IsEntrance(room, ownDoorway) || IsEntrance(neighbour, neighbourDoorway))) continue;

                    doorCount++;
                    Door door = Door.Create("door_" + doorCount, owner.transform, doorway.transform.position,
                        Quaternion.LookRotation(direction), doorway.WorldWidth, doorway.WorldHeight, doorMaterial,
                        open && exitLock == null, locked: exitLock != null);
                    door.locationHint = $"between room {roomIndex + 1} ({room.displayName}) " +
                        $"and room {neighbourIndex + 1} ({neighbour.displayName})";
                    if (exitLock != null) door.gameObject.AddComponent<DoorLock>().Input = exitLock.unlockedBy;
                }
            }
        }

        // Room prefabs carry objects such as puzzle buttons with prefab-wide ids; the same room can appear twice.
        private static void NameRoomObjects(GeneratedLevel level)
        {
            for (int roomIndex = 0; roomIndex < level.Rooms.Count; roomIndex++)
            {
                RoomTemplate room = level.Rooms[roomIndex];
                foreach (Interactable item in room.GetComponentsInChildren<Interactable>())
                {
                    if (item is Door) continue; // Doors are named by SpawnDoors.
                    item.id = $"{item.id}_room{roomIndex + 1}";
                    item.locationHint = $"in room {roomIndex + 1} ({room.displayName}), {item.locationHint}";
                }
            }
        }

        // The doorway on the room's wall that faces the given world direction.
        private static RoomDoor DoorwayFacing(RoomTemplate room, Vector3 direction, float roomSize)
        {
            RoomDoor doorway = NearestDoorway(room, room.transform.position + direction * roomSize * 0.5f);
            if (doorway == null) return null;
            Vector3 offset = doorway.transform.position - room.transform.position;
            // Nearest is not enough: a room without a doorway on that side would return one on another wall.
            return Vector3.Dot(new Vector3(offset.x, 0, offset.z).normalized, direction) > 0.9f ? doorway : null;
        }

        // Both sides are in the prefab's own coordinates, so turning the room does not matter.
        private static bool IsEntrance(RoomTemplate room, RoomDoor doorway) =>
            doorway != null && room.hasEntrance && doorway.side == room.entrance;

        private static DoorwayLock LockOf(RoomDoor doorway)
        {
            // TryGetComponent returns a true null when missing; GetComponent in the editor returns a fake null object.
            return doorway != null && doorway.TryGetComponent(out DoorwayLock found) ? found : null;
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
