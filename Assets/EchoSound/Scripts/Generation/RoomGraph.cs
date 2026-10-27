using System.Collections.Generic;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Generation
{
    /// <summary>
    /// Level layout on a square grid. Stores cells and connections, not scene objects.
    /// First grow a connected tree of rooms, then add optional alternate routes.
    /// </summary>
    public sealed class RoomGraph
    {
        public sealed class Node
        {
            public readonly Vector2Int Cell;
            public DoorMask Doors { get; internal set; }

            public Node(Vector2Int cell)
            {
                Cell = cell;
            }
        }

        // A possible expansion: the source room and the direction of the next room.
        private readonly struct AvailableConnection
        {
            public readonly int RoomIndex;
            public readonly int Direction;

            public AvailableConnection(int roomIndex, int direction)
            {
                RoomIndex = roomIndex;
                Direction = direction;
            }
        }

        public readonly List<Node> Nodes = new List<Node>();
        public readonly Dictionary<Vector2Int, int> Index = new Dictionary<Vector2Int, int>();

        public RoomGraph(int seed, int roomCount, float loopChance)
        {
            var random = new System.Random(seed);
            roomCount = Mathf.Clamp(roomCount, 2, 64);
            BuildConnectedRooms(roomCount, random);
            AddExtraConnections(Mathf.Clamp01(loopChance), random);
        }

        private void BuildConnectedRooms(int roomCount, System.Random random)
        {
            var availableConnections = new List<AvailableConnection>();
            AddRoom(Vector2Int.zero, availableConnections);

            while (Nodes.Count < roomCount)
            {
                int choice = random.Next(availableConnections.Count);
                AvailableConnection connection = availableConnections[choice];
                availableConnections.RemoveAt(choice);

                Vector2Int sourceCell = Nodes[connection.RoomIndex].Cell;
                Vector2Int nextCell = sourceCell + DoorLayout.Steps[connection.Direction];
                if (Index.ContainsKey(nextCell)) continue;

                int newRoomIndex = Nodes.Count;
                AddRoom(nextCell, availableConnections);
                ConnectRooms(connection.RoomIndex, newRoomIndex, connection.Direction);
            }
        }

        private void AddRoom(Vector2Int cell, List<AvailableConnection> availableConnections)
        {
            int roomIndex = Nodes.Count;
            Index.Add(cell, roomIndex);
            Nodes.Add(new Node(cell));

            for (int direction = 0; direction < 4; direction++)
            {
                availableConnections.Add(new AvailableConnection(roomIndex, direction));
            }
        }

        private void AddExtraConnections(float probability, System.Random random)
        {
            for (int roomIndex = 0; roomIndex < Nodes.Count; roomIndex++)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int neighbourCell = Nodes[roomIndex].Cell + DoorLayout.Steps[direction];
                    if (!Index.TryGetValue(neighbourCell, out int neighbourIndex)) continue;
                    if (neighbourIndex <= roomIndex) continue; // Visit each pair only once.

                    if (random.NextDouble() < probability)
                    {
                        ConnectRooms(roomIndex, neighbourIndex, direction);
                    }
                }
            }
        }

        private void ConnectRooms(int firstIndex, int secondIndex, int direction)
        {
            Nodes[firstIndex].Doors |= DoorLayout.Bit(direction);
            Nodes[secondIndex].Doors |= DoorLayout.Bit(DoorLayout.Opposite(direction));
        }

        /// <summary>Breadth-first traversal. A distance of -1 means the room is unreachable.</summary>
        public int[] Distances()
        {
            var distances = new int[Nodes.Count];
            for (int index = 0; index < distances.Length; index++) distances[index] = -1;

            var roomsToVisit = new Queue<int>();
            roomsToVisit.Enqueue(0);
            distances[0] = 0;

            while (roomsToVisit.Count > 0)
            {
                int currentIndex = roomsToVisit.Dequeue();
                Node currentRoom = Nodes[currentIndex];

                for (int direction = 0; direction < 4; direction++)
                {
                    if ((currentRoom.Doors & DoorLayout.Bit(direction)) == 0) continue;

                    Vector2Int neighbourCell = currentRoom.Cell + DoorLayout.Steps[direction];
                    int neighbourIndex = Index[neighbourCell];
                    if (distances[neighbourIndex] >= 0) continue;

                    distances[neighbourIndex] = distances[currentIndex] + 1;
                    roomsToVisit.Enqueue(neighbourIndex);
                }
            }
            return distances;
        }
    }
}
