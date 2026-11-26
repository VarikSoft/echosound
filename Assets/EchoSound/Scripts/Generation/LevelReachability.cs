using System;
using System.Collections.Generic;
using EchoSound.Rooms;

namespace EchoSound.Generation
{
    /// <summary>
    /// Which rooms the player can get to from the spawn room, when some rooms can only be entered by their entrance:
    /// a one-sided puzzle stops anyone coming from another side, and once solved from inside every door of it is usable.
    /// Pure layout logic, no scene objects.
    /// </summary>
    public static class LevelReachability
    {
        /// <param name="entrances">Per room, the grid side of its entrance, or -1 for a room that can be entered from any door.</param>
        public static bool[] Reach(RoomGraph graph, IReadOnlyList<int> entrances)
        {
            var reached = new bool[graph.Nodes.Count];
            if (reached.Length == 0) return reached;

            // The player starts inside the spawn room, so its entrance does not matter.
            var roomsToVisit = new Queue<int>();
            roomsToVisit.Enqueue(0);
            reached[0] = true;

            while (roomsToVisit.Count > 0)
            {
                int current = roomsToVisit.Dequeue();
                RoomGraph.Node room = graph.Nodes[current];
                for (int side = 0; side < 4; side++)
                {
                    if ((room.Doors & DoorLayout.Bit(side)) == 0) continue;
                    int neighbour = graph.Index[room.Cell + DoorLayout.Steps[side]];
                    if (reached[neighbour]) continue;

                    // Entering the neighbour through its door on the opposite side.
                    int entrance = entrances[neighbour];
                    if (entrance >= 0 && entrance != DoorLayout.Opposite(side)) continue;

                    reached[neighbour] = true;
                    roomsToVisit.Enqueue(neighbour);
                }
            }
            return reached;
        }

        /// <summary>The neighbour behind a room's door on the given side, or -1 if there is no door there.</summary>
        public static int Neighbour(RoomGraph graph, int room, int side)
        {
            RoomGraph.Node node = graph.Nodes[room];
            if ((node.Doors & DoorLayout.Bit(side)) == 0) return -1;
            return graph.Index[node.Cell + DoorLayout.Steps[side]];
        }

        /// <summary>
        /// Turns or replaces rooms with an entrance until the spawn reaches every room. Each pass fixes the room
        /// at the edge of the reachable area, so it makes at least one more room reachable and the loop ends.
        /// Without a catalog, rooms are only turned, as in a fixed layout.
        /// </summary>
        public static void MakeEveryRoomReachable(RoomGraph graph, List<RoomCatalog.Match> rooms, RoomCatalog catalog, System.Random random)
        {
            for (int pass = 0; pass <= rooms.Count; pass++)
            {
                bool[] reached = Reach(graph, rooms.ConvertAll(room => room.EntranceSide));
                if (Array.TrueForAll(reached, isReached => isReached)) return;

                int blocked = FindBlockedRoom(graph, reached);
                // Any choice whose entrance faces a room the player can already get to; rooms without an entrance always fit.
                Predicate<RoomCatalog.Match> entersFromReachedSide = candidate => candidate.EntranceSide < 0 ||
                    reached[Neighbour(graph, blocked, candidate.EntranceSide)];
                DoorMask doors = graph.Nodes[blocked].Doors;

                if (catalog != null)
                {
                    rooms[blocked] = catalog.Pick(doors, random, entersFromReachedSide);
                }
                else if (RoomCatalog.TryFit(rooms[blocked].Prefab, doors, out RoomCatalog.Match turned, entersFromReachedSide))
                {
                    rooms[blocked] = turned;
                }
                else
                {
                    throw new InvalidOperationException($"Room {blocked + 1} ({rooms[blocked].Prefab.name}) " +
                        "cannot be turned so that its entrance faces the way from the spawn.");
                }
            }
            throw new InvalidOperationException("Could not make every room reachable.");
        }

        // An unreached room next to a reached one. It is unreached only because its entrance faces away.
        private static int FindBlockedRoom(RoomGraph graph, bool[] reached)
        {
            for (int room = 0; room < reached.Length; room++)
            {
                if (reached[room]) continue;
                for (int side = 0; side < 4; side++)
                {
                    int neighbour = Neighbour(graph, room, side);
                    if (neighbour >= 0 && reached[neighbour]) return room;
                }
            }
            throw new InvalidOperationException("The room graph is not connected.");
        }
    }
}
