using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoSound.Generation;
using EchoSound.Rooms;

namespace EchoSound.Editor.Tests
{
    /// <summary>Checks corridor placement and physical transitions in an isolated scene.</summary>
    public static class CorridorChecks
    {
        [MenuItem("EchoSound/Checks/Validate long corridors")]
        public static void Validate()
        {
            ValidateRarity();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Corridor checks");
                SceneManager.MoveGameObjectToScene(root, scene);
                var generator = root.AddComponent<LevelGenerator>();
                generator.catalog = AssetDatabase.LoadAssetAtPath<RoomCatalog>("Assets/EchoSound/Settings/DefaultRooms.asset");
                generator.corridorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoSound/Prefabs/Corridors/LongCorridor.prefab");
                generator.corridorModulesPerConnection = 2;
                generator.roomCount = 12;
                var overlaps = new Collider[16];
                foreach (float fraction in new[] { 0f, 0.15f, 1f })
                foreach (int seed in new[] { 0, 2030, -13 })
                {
                    generator.longCorridorFraction = fraction;
                    generator.Generate(seed);
                    Physics.SyncTransforms();
                    var graph = generator.Current.Graph;
                    var physics = scene.GetPhysicsScene();
                    int edges = 0, longEdges = 0;
                    var occupied = new HashSet<Vector3>();
                    foreach (var corridor in generator.Current.Corridors)
                        Require(occupied.Add(corridor.transform.position), "Overlapping corridor modules.");
                    foreach (var node in graph.Nodes)
                    {
                        foreach (int side in new[] { 0, 1, 2, 3 })
                        {
                            if ((node.Doors & DoorLayout.Bit(side)) == 0) continue;
                            var step = DoorLayout.Steps[side];
                            if (graph.Index[node.Cell + step] <= graph.Index[node.Cell]) continue;
                            edges++;
                            if (generator.Layout.HasCorridor(node.Cell, side)) longEdges++;
                            var direction = new Vector3(step.x, 0, step.y);
                            var centre = generator.Layout.Position(node.Cell);
                            float first = generator.catalog.roomSize * 0.5f - 0.5f;
                            float last = Vector3.Distance(centre, generator.Layout.Position(node.Cell + step)) - first;
                            for (float distance = first; distance <= last; distance += 0.5f)
                            {
                                var point = centre + direction * distance;
                                Require(physics.Raycast(point + Vector3.up * 0.3f, Vector3.down, out _, 0.7f), "Gap in a corridor transition.");
                                Require(physics.OverlapCapsule(point + Vector3.up * 0.4f,
                                    point + Vector3.up * 1.45f, 0.3f, overlaps) == 0, "Blocked corridor transition.");
                            }
                        }
                    }
                    Require(generator.Current.Corridors.Count == longEdges * 2, "Wrong corridor module count.");
                    Require(longEdges <= Mathf.RoundToInt(edges * fraction), "Too many long corridor links.");
                    if (fraction == 1) Require(longEdges == edges, "Full corridor setting omitted a link.");
                }
                Debug.Log("Corridor checks passed: module counts, unique positions and traversable room/corridor seams.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void ValidateRarity()
        {
            int links = 0, longLinks = 0;
            for (int seed = 0; seed < 1000; seed++)
            {
                var graph = new RoomGraph(seed, 18, 0.35f);
                var layout = new CorridorLayout(graph, seed, 18, 2, 0.15f);
                var repeat = new CorridorLayout(graph, seed, 18, 2, 0.15f);
                var compact = new CorridorLayout(graph, seed, 18, 2, 0);
                var full = new CorridorLayout(graph, seed, 18, 2, 1);
                Require(compact.LongConnectionCount == 0, "Zero frequency must produce no corridors.");
                Require(full.LongConnectionCount == full.ConnectionCount, "Full frequency must extend all links.");
                Require(layout.LongConnectionCount <= Mathf.RoundToInt(layout.ConnectionCount * 0.15f), "Corridor budget exceeded.");
                foreach (var first in graph.Nodes)
                {
                    Vector3 position = layout.Position(first.Cell);
                    Require(position == repeat.Position(first.Cell), "Corridor layout is not deterministic.");
                    foreach (var second in graph.Nodes)
                    {
                        if (first == second) continue;
                        Vector3 delta = layout.Position(second.Cell) - position;
                        Require(Mathf.Abs(delta.x) >= 18 || Mathf.Abs(delta.z) >= 18, "Rooms overlap after compacting.");
                    }
                }
                links += layout.ConnectionCount;
                longLinks += layout.LongConnectionCount;
            }
            float ratio = longLinks / (float)links;
            Require(ratio > 0.05f && ratio <= 0.17f, "Rare corridors are missing or too frequent.");
            Debug.Log($"Corridor rarity: {ratio:P1} long links across 1000 layouts; deterministic and overlap-free.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
