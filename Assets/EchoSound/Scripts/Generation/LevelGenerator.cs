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

            var graph = new RoomGraph(newSeed, roomCount, loopChance);
            List<RoomCatalog.Match> selectedRooms = SelectPrefabs(graph, newSeed);

            // Validate and select prefabs before removing the previous level.
            Clear();
            seed = newSeed;
            generatedRoot = new GameObject("Generated rooms [seed " + seed + "]").transform;
            generatedRoot.SetParent(transform, false);

            var placedRooms = new List<RoomTemplate>();
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                RoomTemplate room = CreateRoomInstance(selectedRooms[index].Prefab);
                Vector2Int cell = graph.Nodes[index].Cell;
                room.transform.localPosition = new Vector3(cell.x, 0, cell.y) * catalog.roomSize;
                room.transform.localRotation = Quaternion.Euler(0, selectedRooms[index].Turns * 90, 0);
                room.name = $"{index + 1:00} {room.displayName}";
                placedRooms.Add(room);
            }

            Current = new GeneratedLevel(graph, placedRooms);
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
        }
    }
}
