using System;
using System.Collections.Generic;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Generation
{
    /// <summary>
    /// Places authored prefabs. Edit walls, bridges and walkways inside those prefabs.
    /// Flow: build connections, select prefabs, place rooms, publish the Generated event.
    /// </summary>
    public sealed class LevelGenerator : MonoBehaviour
    {
        [Header("Room assets")]
        public RoomCatalog catalog;
        [Tooltip("Optional north/south corridor prefab. Its length must equal the catalog room size.")]
        public GameObject corridorPrefab;
        [Tooltip("Two 18m modules create a 36m corridor between connected rooms.")]
        [Range(0, 4)] public int corridorModulesPerConnection = 2;
        [Tooltip("Approximate maximum share of links using long corridors. Other rooms connect directly.")]
        [Range(0, 1)] public float longCorridorFraction = 0.15f;
        public CorridorLayout Layout { get; private set; }

        [Header("Layout settings")]
        [Tooltip("The same seed and catalog settings produce the same layout.")]
        public int seed = 2026;
        [Range(2, 64)] public int roomCount = 12;
        [Tooltip("Probability of adding an extra connection between neighbouring rooms.")]
        [Range(0, 1)] public float loopChance = 0.2f;

        [SerializeField, HideInInspector] private Transform generatedRoot;
        public GeneratedLevel Current { get; private set; }
        public event Action<GeneratedLevel> Generated;

        public void Generate(int newSeed)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException("Assign a room catalog to the LevelGenerator.");
            }
            catalog.ValidateOrThrow();
            if (corridorPrefab != null)
            {
                var corridor = corridorPrefab.GetComponent<RoomTemplate>();
                if (corridor == null || !corridor.Validate(catalog.roomSize, out _) ||
                    corridor.Doors != (DoorMask.North | DoorMask.South))
                    throw new InvalidOperationException("Corridor must be a north/south RoomTemplate with matching footprint.");
            }

            var graph = new RoomGraph(newSeed, roomCount, loopChance);
            List<RoomCatalog.Match> selectedRooms = SelectPrefabs(graph, newSeed);
            var layout = new CorridorLayout(graph, newSeed, catalog.roomSize,
                corridorPrefab != null ? corridorModulesPerConnection : 0, longCorridorFraction);

            // Validate and select prefabs before removing the previous level.
            Clear();
            Layout = layout;
            seed = newSeed;
            generatedRoot = new GameObject("Generated rooms [seed " + seed + "]").transform;
            generatedRoot.SetParent(transform, false);

            var placedRooms = new List<RoomTemplate>();
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                RoomTemplate room = CreateRoomInstance(selectedRooms[index].Prefab);
                Vector2Int cell = graph.Nodes[index].Cell;
                room.transform.localPosition = Layout.Position(cell);
                room.transform.localRotation = Quaternion.Euler(0, selectedRooms[index].Turns * 90, 0);
                room.name = $"{index + 1:00} {room.displayName}";
                placedRooms.Add(room);
            }

            List<RoomTemplate> corridors = PlaceCorridors(graph);
            Current = new GeneratedLevel(graph, placedRooms, corridors);
            Physics.SyncTransforms();
            Generated?.Invoke(Current);
        }

        private List<RoomCatalog.Match> SelectPrefabs(RoomGraph graph, int layoutSeed)
        {
            // Separate random state keeps prefab selection independent of layout construction.
            var random = new System.Random(layoutSeed);
            var result = new List<RoomCatalog.Match>();
            foreach (RoomGraph.Node node in graph.Nodes)
            {
                result.Add(catalog.Pick(node.Doors, random));
            }
            return result;
        }

        private List<RoomTemplate> PlaceCorridors(RoomGraph graph)
        {
            var result = new List<RoomTemplate>();
            if (corridorPrefab == null) return result;
            int count = Mathf.Clamp(corridorModulesPerConnection, 0, 4);
            for (int roomIndex = 0; roomIndex < graph.Nodes.Count; roomIndex++)
            {
                var node = graph.Nodes[roomIndex];
                for (int side = 0; side < 4; side++)
                {
                    if ((node.Doors & DoorLayout.Bit(side)) == 0) continue;
                    var step = DoorLayout.Steps[side];
                    int neighbour = graph.Index[node.Cell + step];
                    if (neighbour <= roomIndex) continue;
                    if (!Layout.HasCorridor(node.Cell, side)) continue;
                    Vector3 start = Layout.Position(node.Cell);
                    Vector3 direction = new Vector3(step.x, 0, step.y);
                    for (int segment = 1; segment <= count; segment++)
                    {
                        var corridor = CreateRoomInstance(corridorPrefab);
                        corridor.transform.localPosition = start + direction * (catalog.roomSize * segment);
                        corridor.transform.localRotation = Quaternion.Euler(0, side * 90, 0);
                        corridor.name = $"Corridor {roomIndex + 1:00}-{neighbour + 1:00} / {segment}";
                        result.Add(corridor);
                    }
                }
            }
            return result;
        }

        private RoomTemplate CreateRoomInstance(GameObject prefab)
        {
#if UNITY_EDITOR
            // Keep prefab links intact when generating a preview in the editor.
            if (!Application.isPlaying)
            {
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, generatedRoot);
                return instance.GetComponent<RoomTemplate>();
            }
#endif
            return Instantiate(prefab, generatedRoot).GetComponent<RoomTemplate>();
        }

        public void Clear()
        {
            if (generatedRoot != null)
            {
                generatedRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generatedRoot.gameObject);
                else DestroyImmediate(generatedRoot.gameObject);
            }
            generatedRoot = null;
            Current = null;
            Layout = null;
        }
    }
}
