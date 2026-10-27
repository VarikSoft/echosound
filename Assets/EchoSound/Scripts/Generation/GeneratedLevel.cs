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
        public Vector3 Spawn => Rooms[0].AnchorPosition;

        public GeneratedLevel(RoomGraph graph, List<RoomTemplate> rooms)
        {
            Graph = graph;
            Rooms = rooms;
        }
    }
}
