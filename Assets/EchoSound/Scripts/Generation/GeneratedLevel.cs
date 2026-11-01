using System.Collections.Generic;
using UnityEngine;
using EchoSound.Rooms;

namespace EchoSound.Generation
{
    /// <summary>Generation result: layout, placed room instances, and a safe spawn point.</summary>
    public sealed class GeneratedLevel
    {
        public readonly RoomGraph Graph;
        public readonly IReadOnlyList<RoomTemplate> Rooms;
        public readonly IReadOnlyList<RoomTemplate> Corridors;
        public Vector3 Spawn => Rooms[0].AnchorPosition;

        public GeneratedLevel(RoomGraph graph, List<RoomTemplate> rooms, List<RoomTemplate> corridors = null)
        {
            Graph = graph;
            Rooms = rooms;
            Corridors = corridors ?? new List<RoomTemplate>();
        }
    }
}
