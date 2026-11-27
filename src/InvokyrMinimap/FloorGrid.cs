using System;
using System.Collections.Generic;

namespace InvokyrMinimap
{
    internal enum CellKind : byte { None, Floor, Wall, Pit }

    /// <summary>What the map knows about one spot of the level.</summary>
    internal struct Cell
    {
        public CellKind Kind;
        public byte Flags;   // MapPainter.Exit / MapPainter.Shop
        public ushort Room;  // which room claimed it, for outlines between rooms
        public float Y;      // floor height
        public float Depth;  // how far inside its room's box the spot is; the deepest room owns it
    }

    /// <summary>
    /// The scanned level: a grid of <see cref="CellSize"/> cells over the X/Z plane, with up to two floors per cell
    /// (stacked floors further apart than <see cref="FloorGap"/>). Stored in 16×16 chunks so it only grows where rooms
    /// were scanned. Free of UnityEngine so it can be tested.
    /// </summary>
    internal sealed class FloorGrid
    {
        public const float CellSize = 0.4f;
        public const float FloorGap = 2f;
        private const int ChunkBits = 4;
        private const int ChunkSize = 1 << ChunkBits;
        private const int ChunkMask = ChunkSize - 1;

        private sealed class Chunk
        {
            public readonly Cell[] Lower = new Cell[ChunkSize * ChunkSize];
            public readonly Cell[] Upper = new Cell[ChunkSize * ChunkSize];
        }

        private readonly Dictionary<long, Chunk> _chunks = new Dictionary<long, Chunk>();
        private long _lastKey = long.MinValue;
        private Chunk _last;

        public int Version { get; private set; }

        public static int Index(float world) => (int)Math.Floor(world / CellSize);

        public static float Center(int index) => (index + 0.5f) * CellSize;

        public void Clear()
        {
            _chunks.Clear();
            _lastKey = long.MinValue;
            _last = null;
            Version++;
        }

        /// <summary>Records what a room's scan found at cell (ix, iz). A deeper claim on the same floor replaces a shallower one.</summary>
        public void Add(int ix, int iz, Cell cell)
        {
            if (cell.Kind == CellKind.None)
                return;
            var chunk = GetChunk(ix, iz, create: true);
            var i = ((iz & ChunkMask) << ChunkBits) | (ix & ChunkMask);
            if (!Merge(ref chunk.Lower[i], cell))
                Merge(ref chunk.Upper[i], cell);
            Version++;
        }

        private static bool Merge(ref Cell slot, Cell cell)
        {
            if (slot.Kind == CellKind.None)
            {
                slot = cell;
                return true;
            }
            if (Math.Abs(slot.Y - cell.Y) > FloorGap)
                return false;
            if (cell.Depth > slot.Depth)
                slot = cell;
            return true;
        }

        /// <summary>The cell at world X/Z on the floor closest to height <paramref name="y"/>.</summary>
        public bool TryGet(float x, float z, float y, out Cell cell)
        {
            var ix = Index(x);
            var iz = Index(z);
            var chunk = GetChunk(ix, iz, create: false);
            if (chunk == null)
            {
                cell = default;
                return false;
            }
            var i = ((iz & ChunkMask) << ChunkBits) | (ix & ChunkMask);
            var lower = chunk.Lower[i];
            var upper = chunk.Upper[i];
            if (lower.Kind == CellKind.None)
            {
                cell = default;
                return false;
            }
            cell = upper.Kind != CellKind.None && Math.Abs(upper.Y - y) < Math.Abs(lower.Y - y) ? upper : lower;
            return true;
        }

        private Chunk GetChunk(int ix, int iz, bool create)
        {
            var key = ((long)(ix >> ChunkBits) << 32) ^ (uint)(iz >> ChunkBits);
            if (key == _lastKey && _last != null)
                return _last;
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                if (!create)
                    return null;
                chunk = new Chunk();
                _chunks[key] = chunk;
            }
            _lastKey = key;
            _last = chunk;
            return chunk;
        }
    }

    /// <summary>
    /// Turns one box's raycast samples into floor, pit and wall. A room's floor level is the median of each spot's lowest
    /// upward-facing hit; a spot is floor if it has a surface near that level, a pit if its only surfaces are well below
    /// it, and solid (wall) if its only surfaces are well above it.
    /// </summary>
    internal static class FloorClassifier
    {
        public const float Below = 0.6f, Above = 1.0f;

        /// <summary>How far a surface may be from where a player stood and still count as the room's floor.</summary>
        public const float StoodRange = 1.5f;

        /// <summary>
        /// The box's floor level. <paramref name="nearStood"/> holds, per spot, the surface closest to where a player stood
        /// in the room; when enough spots have one, that is the floor (a box reaching down into the room below would
        /// otherwise take the lower floor). Otherwise the median lowest hit.
        /// </summary>
        public static float Level(List<float> lowestHits, List<float> nearStood) =>
            nearStood.Count >= 3 && nearStood.Count * 4 >= lowestHits.Count ? Level(nearStood) : Level(lowestHits);

        public static float Level(List<float> lowestHits)
        {
            if (lowestHits.Count == 0)
                return 0f;
            var sorted = new List<float>(lowestHits);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }

        /// <summary>The kind of spot whose upward-facing hit heights are <paramref name="ys"/>; y is the surface used.</summary>
        public static CellKind Classify(List<float> ys, int start, int count, float level, out float y)
        {
            y = level;
            var best = float.MaxValue;
            bool below = false, above = false;
            for (var i = start; i < start + count; i++)
            {
                var d = ys[i] - level;
                if (d < -Below)
                    below = true;
                else if (d > Above)
                    above = true;
                else if (Math.Abs(d) < best)
                {
                    best = Math.Abs(d);
                    y = ys[i];
                }
            }
            if (best < float.MaxValue)
                return CellKind.Floor;
            if (below)
                return CellKind.Pit;
            return above ? CellKind.Wall : CellKind.None;
        }
    }
}
