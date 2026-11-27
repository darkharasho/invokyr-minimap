using System;

namespace InvokyrMinimap
{
    /// <summary>Paints the scanned floor plan into a raster through a (possibly turned) view.</summary>
    internal static class MapPainter
    {
        public const byte Exit = 1, Shop = 2;

        public static readonly Rgba Floor = Rgba.Of(0.42f, 0.38f, 0.48f, 0.9f);
        public static readonly Rgba ExitFloor = Rgba.Of(0.25f, 0.62f, 0.35f, 0.92f);
        public static readonly Rgba ShopFloor = Rgba.Of(0.25f, 0.45f, 0.75f, 0.92f);
        public static readonly Rgba Wall = Rgba.Of(0.88f, 0.85f, 0.93f, 0.95f);
        public static readonly Rgba Edge = Rgba.Of(0.85f, 0.82f, 0.9f, 0.9f);
        public static readonly Rgba Pit = Rgba.Of(0.1f, 0.04f, 0.06f, 0.95f);
        public static readonly Rgba OtherFloor = Rgba.Of(0.42f, 0.38f, 0.48f, 0.28f);
        public static readonly Rgba OtherWall = Rgba.Of(0.88f, 0.85f, 0.93f, 0.3f);

        /// <summary>
        /// Fills every pixel of <paramref name="raster"/> from <paramref name="grid"/>. The view's screen is the raster
        /// (0, 0, Width, Height). Cells on the floor at height <paramref name="y"/> are solid; other floors are faint.
        /// Floor <paramref name="outline"/> pixels from empty space or another room is drawn as an outline.
        /// </summary>
        public static void Paint(Raster raster, FloorGrid grid, MapView view, float y, float outline = 1f)
        {
            raster.Clear();
            var w = raster.Width;
            var h = raster.Height;
            // World position of pixel (0, 0)'s centre and the world step per pixel right and down.
            float rx = view.UpZ / view.Scale, rz = -view.UpX / view.Scale;   // one pixel right
            float dx = -view.UpX / view.Scale, dz = -view.UpZ / view.Scale;  // one pixel down
            var ox = view.CenterX + (0.5f - w / 2f) * rx + (0.5f - h / 2f) * dx;
            var oz = view.CenterZ + (0.5f - w / 2f) * rz + (0.5f - h / 2f) * dz;
            for (var py = 0; py < h; py++)
            {
                var x = ox + py * dx;
                var z = oz + py * dz;
                for (var px = 0; px < w; px++, x += rx, z += rz)
                {
                    if (!grid.TryGet(x, z, y, out var cell))
                        continue;
                    var same = Math.Abs(cell.Y - y) <= FloorGrid.FloorGap;
                    if (same && cell.Kind != CellKind.Wall && outline > 0f &&
                        (Border(grid, cell, x + rx * outline, z + rz * outline, y) || Border(grid, cell, x - rx * outline, z - rz * outline, y) ||
                         Border(grid, cell, x + dx * outline, z + dz * outline, y) || Border(grid, cell, x - dx * outline, z - dz * outline, y)))
                        raster.Set(px, py, Edge);
                    else
                        raster.Set(px, py, Color(cell, same));
                }
            }
        }

        // True if the spot at x/z is outside cell's room: nothing there, another floor, or another room's floor.
        private static bool Border(FloorGrid grid, Cell cell, float x, float z, float y)
        {
            if (!grid.TryGet(x, z, y, out var other) || Math.Abs(other.Y - y) > FloorGrid.FloorGap)
                return true;
            return other.Kind != CellKind.Wall && other.Room != cell.Room;
        }

        private static Rgba Color(Cell cell, bool sameFloor)
        {
            if (cell.Kind == CellKind.Wall)
                return sameFloor ? Wall : OtherWall;
            if (!sameFloor)
                return OtherFloor;
            if (cell.Kind == CellKind.Pit)
                return Pit;
            return (cell.Flags & Exit) != 0 ? ExitFloor : (cell.Flags & Shop) != 0 ? ShopFloor : Floor;
        }
    }
}
