using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>
    /// Bounded 3D first-arrival field. Fast marching solves |grad(distance)| = 1 in
    /// air voxels; occupied voxels stop the front. This is geometric acoustics,
    /// not a pressure-wave solver: diffraction is approximated by paths around solids.
    /// Geometry is sampled at emission, so the next sound sees moved doors.
    /// </summary>
    public sealed class AcousticField
    {
        public const int Size = 41;
        public const int CellCount = Size * Size * Size;
        public const float Unreachable = 10000f;
        public const float SpeedOfSound = 343f;
        public readonly float[] Distances = new float[CellCount];
        public readonly Vector3 Minimum;
        public readonly float CellSize;
        public readonly float Range;
        public readonly Vector3 Origin;
        private readonly byte[] state = new byte[CellCount]; // unknown, air, solid, accepted
        private readonly bool[] nearGeometry = new bool[CellCount];
        private readonly byte[] checkedEdges = new byte[CellCount];
        private readonly byte[] blockedEdges = new byte[CellCount];
        private readonly Collider[] overlaps = new Collider[32];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Transform source;
        private readonly int mask;
        private readonly List<Entry> heap = new List<Entry>(2048);
        private struct Entry { public int Index; public float Distance; }

        public AcousticField(in SoundEvent sound, float range, int obstacleMask = Physics.DefaultRaycastLayers)
        {
            Origin = sound.Position;
            Range = Mathf.Clamp(range, 0.1f, 40f);
            CellSize = Mathf.Max(0.35f, Range / (Size / 2 - 2));
            Minimum = Origin - Vector3.one * (Size / 2 * CellSize);
            source = sound.Source != null ? sound.Source.transform : null;
            mask = obstacleMask;
            for (int i = 0; i < CellCount; i++) Distances[i] = Unreachable;
            Build();
        }

        private bool Ignore(Collider collider)
        {
            return collider is CharacterController || (source != null && collider.transform.IsChildOf(source));
        }

        public bool IsClear(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            if (delta.sqrMagnitude < 0.00001f) return true;
            int count = Physics.RaycastNonAlloc(a, delta.normalized, hits, delta.magnitude,
                mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false; // fail closed if the buffer saturates
            for (int i = 0; i < count; i++) if (!Ignore(hits[i].collider)) return false;
            // Reverse cast also detects an endpoint embedded in a solid.
            count = Physics.RaycastNonAlloc(b, -delta.normalized, hits, delta.magnitude,
                mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignore(hits[i].collider)) return false;
            return true;
        }

        private Vector3 Position(int index)
        {
            return Minimum + new Vector3(index % Size, index / Size % Size, index / (Size * Size)) * CellSize;
        }

        private bool IsAir(int index)
        {
            if (state[index] != 0) return state[index] != 2;
            if ((Position(index) - Origin).sqrMagnitude > Range * Range) { state[index] = 2; return false; }
            int count = Physics.OverlapBoxNonAlloc(Position(index), Vector3.one * (CellSize * 0.501f),
                overlaps, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            bool nearby = count == overlaps.Length;
            for (int i = 0; i < count && !nearby; i++) nearby = !Ignore(overlaps[i]);
            nearGeometry[index] = nearby;
            // Sample air at nodes, not inflated wall-sized cubes. Connections near geometry
            // are tested separately, preserving door clearance and thin-wall occlusion.
            bool solid = false;
            if (nearby)
            {
                count = Physics.OverlapBoxNonAlloc(Position(index), Vector3.one * 0.025f,
                    overlaps, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                solid = count == overlaps.Length;
                for (int i = 0; i < count && !solid; i++) solid = !Ignore(overlaps[i]);
            }
            state[index] = solid ? (byte)2 : (byte)1;
            return !solid;
        }

        private void Build()
        {
            int middle = Size / 2;
            // An emitter may sit on the floor: seed nearby air without tunnelling through it.
            for (int z = middle - 1; z <= middle + 1; z++)
            for (int y = middle - 1; y <= middle + 1; y++)
            for (int x = middle - 1; x <= middle + 1; x++)
            {
                int i = x + Size * (y + Size * z);
                if (!IsAir(i) || !IsClear(Origin, Position(i))) continue;
                Distances[i] = Vector3.Distance(Origin, Position(i));
                Push(i, Distances[i]);
            }
            while (heap.Count > 0)
            {
                Entry current = Pop();
                int i = current.Index;
                if (state[i] == 3 || current.Distance > Distances[i]) continue;
                state[i] = 3;
                int x = i % Size, y = i / Size % Size, z = i / (Size * Size);
                if (x > 0) Visit(i - 1);
                if (x < Size - 1) Visit(i + 1);
                if (y > 0) Visit(i - Size);
                if (y < Size - 1) Visit(i + Size);
                if (z > 0) Visit(i - Size * Size);
                if (z < Size - 1) Visit(i + Size * Size);
            }
        }

        private float Accepted(int from, int to, int direction)
        {
            if (state[to] != 3) return Unreachable;
            if (!nearGeometry[from] && !nearGeometry[to]) return Distances[to];
            int bit = 1 << direction;
            if ((checkedEdges[from] & bit) == 0)
            {
                int reverseBit = 1 << (direction ^ 1);
                checkedEdges[from] |= (byte)bit;
                checkedEdges[to] |= (byte)reverseBit;
                if (!IsClear(Position(from), Position(to)))
                {
                    blockedEdges[from] |= (byte)bit;
                    blockedEdges[to] |= (byte)reverseBit;
                }
            }
            return (blockedEdges[from] & bit) == 0 ? Distances[to] : Unreachable;
        }

        private void Visit(int i)
        {
            if (state[i] == 3 || !IsAir(i)) return;
            int x = i % Size, y = i / Size % Size, z = i / (Size * Size);
            float a = Mathf.Min(x > 0 ? Accepted(i, i - 1, 0) : Unreachable, x < Size - 1 ? Accepted(i, i + 1, 1) : Unreachable);
            float b = Mathf.Min(y > 0 ? Accepted(i, i - Size, 2) : Unreachable, y < Size - 1 ? Accepted(i, i + Size, 3) : Unreachable);
            float c = Mathf.Min(z > 0 ? Accepted(i, i - Size * Size, 4) : Unreachable, z < Size - 1 ? Accepted(i, i + Size * Size, 5) : Unreachable);
            if (a > b) Swap(ref a, ref b);
            if (b > c) Swap(ref b, ref c);
            if (a > b) Swap(ref a, ref b);
            float d = a + CellSize;
            if (d > b) d = (a + b + Mathf.Sqrt(Mathf.Max(0f, 2f * CellSize * CellSize - (a - b) * (a - b)))) * 0.5f;
            if (d > c) d = (a + b + c + Mathf.Sqrt(Mathf.Max(0f, 3f * CellSize * CellSize -
                (a - b) * (a - b) - (a - c) * (a - c) - (b - c) * (b - c)))) / 3f;
            if (d >= Distances[i] || d > Range) return;
            Distances[i] = d;
            Push(i, d);
        }

        private static void Swap(ref float a, ref float b) { float t = a; a = b; b = t; }

        /// <summary>Exact direct path when visible, otherwise a conservative reachable voxel path.</summary>
        public float PathDistance(Vector3 target)
        {
            float direct = Vector3.Distance(Origin, target);
            if (direct > Range) return Unreachable;
            if (IsClear(Origin, target)) return direct;
            Vector3 cell = (target - Minimum) / CellSize;
            int cx = Mathf.RoundToInt(cell.x), cy = Mathf.RoundToInt(cell.y), cz = Mathf.RoundToInt(cell.z);
            float best = Unreachable;
            for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(Size - 1, cz + 1); z++)
            for (int y = Mathf.Max(0, cy - 1); y <= Mathf.Min(Size - 1, cy + 1); y++)
            for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(Size - 1, cx + 1); x++)
            {
                int i = x + Size * (y + Size * z);
                float d = Distances[i] + Vector3.Distance(Position(i), target);
                if (d < best && IsClear(Position(i), target)) best = d;
            }
            return Mathf.Max(direct, best);
        }

        // Pressure falls as 1/r (energy as 1/r²), plus air absorption and extra loss on bent paths.
        public static float Pressure(float path, float direct)
        {
            return Mathf.Exp(-0.008f * path - 0.18f * Mathf.Max(0f, path - direct)) / Mathf.Max(1f, path);
        }

        private void Push(int index, float distance)
        {
            var e = new Entry { Index = index, Distance = distance };
            int i = heap.Count;
            heap.Add(e);
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].Distance <= distance) break;
                heap[i] = heap[parent]; i = parent;
            }
            heap[i] = e;
        }

        private Entry Pop()
        {
            Entry root = heap[0], last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return root;
            int i = 0;
            while (i * 2 + 1 < heap.Count)
            {
                int child = i * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].Distance < heap[child].Distance) child++;
                if (last.Distance <= heap[child].Distance) break;
                heap[i] = heap[child]; i = child;
            }
            heap[i] = last;
            return root;
        }
    }
}
