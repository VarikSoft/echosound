using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EchoSound.Generation;
using EchoSound.Rooms;
using Object = UnityEngine.Object;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// Rooms with a one-sided entrance must never cut the level: every room stays reachable from the spawn.
    /// Uses an in-memory catalog, so it does not depend on which puzzle prefabs exist.
    /// </summary>
    public static class EntranceChecks
    {
        [MenuItem("EchoSound/Checks/Validate one-sided rooms")]
        public static void Validate()
        {
            var created = new List<Object>();
            try
            {
                RoomCatalog catalog = CreateCatalog(created);
                CheckReachRules();
                CheckRandomLayouts(catalog);
                CheckFixedChain(catalog);
                Debug.Log("Entrance checks passed: every room is reachable in 1000 layouts with one-sided rooms.");
            }
            finally
            {
                foreach (Object item in created) Object.DestroyImmediate(item);
            }
        }

        // A chain of three rooms: the middle one entered only from the spawn side, or only from the far side.
        private static void CheckReachRules()
        {
            RoomGraph chain = RoomGraph.Chain(3);
            int south = (int)DoorSide.South;
            int north = (int)DoorSide.North;
            Require(Array.TrueForAll(LevelReachability.Reach(chain, new[] { -1, south, -1 }), reached => reached),
                "A room entered from the spawn side must open the way on.");
            bool[] blocked = LevelReachability.Reach(chain, new[] { -1, north, -1 });
            Require(!blocked[1] && !blocked[2], "A room entered only from the far side must block the way.");
        }

        private static void CheckRandomLayouts(RoomCatalog catalog)
        {
            int placedOneSided = 0;
            for (int seed = -500; seed < 500; seed++)
            {
                var graph = new RoomGraph(seed, 2 + Math.Abs(seed) % 40, Math.Abs(seed) % 3 * 0.25f);
                var random = new System.Random(seed);
                var rooms = new List<RoomCatalog.Match>();
                foreach (RoomGraph.Node node in graph.Nodes) rooms.Add(catalog.Pick(node.Doors, random));

                LevelReachability.MakeEveryRoomReachable(graph, rooms, catalog, random);

                bool[] reached = LevelReachability.Reach(graph, rooms.ConvertAll(room => room.EntranceSide));
                Require(Array.TrueForAll(reached, isReached => isReached), $"Seed {seed}: a room is unreachable.");
                for (int index = 0; index < rooms.Count; index++)
                {
                    Require(DoorLayout.Rotate(rooms[index].Prefab.GetComponent<RoomTemplate>().Doors, rooms[index].Turns)
                        == graph.Nodes[index].Doors, $"Seed {seed}: room {index + 1} has the wrong doors.");
                    if (rooms[index].EntranceSide >= 0) placedOneSided++;
                }
            }
            // Otherwise the check above would pass without testing anything.
            Require(placedOneSided > 0, "No one-sided room was ever placed.");
        }

        // Fixed layouts may only turn rooms: a one-sided straight room facing away must be turned around.
        private static void CheckFixedChain(RoomCatalog catalog)
        {
            RoomGraph chain = RoomGraph.Chain(3);
            GameObject oneSided = catalog.rooms.Find(entry => entry.prefab.name == "One-sided straight").prefab;
            GameObject deadEnd = catalog.rooms.Find(entry => entry.prefab.name == "Dead end").prefab;
            var rooms = new List<RoomCatalog.Match>();
            foreach (GameObject prefab in new[] { deadEnd, oneSided, deadEnd })
            {
                int index = rooms.Count;
                // Start from the turn that faces the entrance away from the spawn.
                Require(RoomCatalog.TryFit(prefab, chain.Nodes[index].Doors, out RoomCatalog.Match match,
                    candidate => index != 1 || candidate.EntranceSide == (int)DoorSide.North), "Test room does not fit.");
                rooms.Add(match);
            }

            LevelReachability.MakeEveryRoomReachable(chain, rooms, null, null);
            Require(rooms[1].EntranceSide == (int)DoorSide.South, "The one-sided room was not turned to face the spawn.");
        }

        private static RoomCatalog CreateCatalog(List<Object> created)
        {
            var catalog = ScriptableObject.CreateInstance<RoomCatalog>();
            created.Add(catalog);
            void Add(string name, DoorMask doors, DoorSide? entrance)
            {
                var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                created.Add(root);
                RoomTemplate template = root.AddComponent<RoomTemplate>();
                template.hasEntrance = entrance.HasValue;
                template.entrance = entrance.GetValueOrDefault();
                for (int side = 0; side < 4; side++)
                {
                    if ((doors & DoorLayout.Bit(side)) == 0) continue;
                    var marker = new GameObject("Door") { hideFlags = HideFlags.HideAndDontSave };
                    marker.transform.SetParent(root.transform, false);
                    marker.AddComponent<RoomDoor>().side = (DoorSide)side;
                }
                catalog.rooms.Add(new RoomCatalog.Entry { prefab = root, weight = 1, allowRotation = true });
            }

            Add("Dead end", DoorMask.North, null);
            Add("Straight", DoorMask.North | DoorMask.South, null);
            Add("Corner", DoorMask.North | DoorMask.East, null);
            Add("Three way", DoorMask.North | DoorMask.East | DoorMask.South, null);
            Add("Four way", DoorMask.North | DoorMask.East | DoorMask.South | DoorMask.West, null);
            // Heavier than the others, so they often land facing the wrong way and must be repaired.
            Add("One-sided straight", DoorMask.North | DoorMask.South, DoorSide.South);
            Add("One-sided corner", DoorMask.North | DoorMask.East, DoorSide.East);
            catalog.rooms[5].weight = 5;
            catalog.rooms[6].weight = 5;
            return catalog;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
