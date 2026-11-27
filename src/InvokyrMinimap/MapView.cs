using System;

namespace InvokyrMinimap
{
    /// <summary>A screen rectangle (GUI space: x right, y down). Kept free of UnityEngine so it can be tested.</summary>
    internal readonly struct Box
    {
        public readonly float X, Y, W, H;
        public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float XMax => X + W;
        public float YMax => Y + H;
        public bool Empty => W <= 0f || H <= 0f;
        public bool Contains(float x, float y) => x >= X && x <= XMax && y >= Y && y <= YMax;
    }

    /// <summary>The corner the minimap sits in.</summary>
    public enum MapCorner { TopRight, TopLeft, BottomRight, BottomLeft }

    public enum MapBackground { Dark, Black, Tan }

    internal static class Layout
    {
        /// <summary>A size×size box in <paramref name="corner"/> of a width×height screen, offset inwards from both edges.</summary>
        public static Box Corner(MapCorner corner, float size, float offsetX, float offsetY, float width, float height)
        {
            var right = corner == MapCorner.TopRight || corner == MapCorner.BottomRight;
            var bottom = corner == MapCorner.BottomRight || corner == MapCorner.BottomLeft;
            var x = right ? width - size - offsetX : offsetX;
            var y = bottom ? height - size - offsetY : offsetY;
            // Keep it on screen whatever the offsets are.
            x = Math.Max(0f, Math.Min(x, width - size));
            y = Math.Max(0f, Math.Min(y, height - size));
            return new Box(x, y, size, size);
        }
    }

    /// <summary>
    /// Top-down projection of the world's X/Z plane into a screen box, centred on world point
    /// <see cref="CenterX"/>/<see cref="CenterZ"/> at <see cref="Scale"/> pixels per metre. The world direction
    /// (<see cref="UpX"/>, <see cref="UpZ"/>) points up on screen: north (0, 1) for a fixed map, or the way you look for
    /// a map that turns with you.
    /// </summary>
    internal readonly struct MapView
    {
        public readonly Box Screen;
        public readonly float CenterX, CenterZ, Scale, UpX, UpZ;

        public MapView(Box screen, float centerX, float centerZ, float scale) : this(screen, centerX, centerZ, scale, 0f, 1f) { }

        public MapView(Box screen, float centerX, float centerZ, float scale, float upX, float upZ)
        {
            Screen = screen;
            CenterX = centerX;
            CenterZ = centerZ;
            Scale = scale;
            var len = (float)Math.Sqrt(upX * upX + upZ * upZ);
            if (len < 1e-4f)
            {
                upX = 0f;
                upZ = 1f;
                len = 1f;
            }
            UpX = upX / len;
            UpZ = upZ / len;
        }

        // Screen right is the up direction turned a quarter clockwise (seen from above).
        private float RightX => UpZ;
        private float RightZ => -UpX;

        public float ScreenX(float worldX, float worldZ) =>
            Screen.X + Screen.W / 2f + ((worldX - CenterX) * RightX + (worldZ - CenterZ) * RightZ) * Scale;

        public float ScreenY(float worldX, float worldZ) =>
            Screen.Y + Screen.H / 2f - ((worldX - CenterX) * UpX + (worldZ - CenterZ) * UpZ) * Scale;

        /// <summary>A world direction as a screen direction (x right, y down), not normalised.</summary>
        public void Direction(float dirX, float dirZ, out float sx, out float sy)
        {
            sx = dirX * RightX + dirZ * RightZ;
            sy = -(dirX * UpX + dirZ * UpZ);
        }

        /// <summary>
        /// A view of everything between min and max (world X/Z), turned so (upX, upZ) is up, with a margin, keeping
        /// metres square.
        /// </summary>
        public static MapView Fit(Box screen, float minX, float minZ, float maxX, float maxZ, float upX = 0f, float upZ = 1f, float margin = 0.06f)
        {
            // Measure the box's corners along the view's right and up axes.
            var probe = new MapView(screen, 0f, 0f, 1f, upX, upZ);
            float r0 = float.MaxValue, r1 = float.MinValue, u0 = float.MaxValue, u1 = float.MinValue;
            foreach (var (x, z) in new[] { (minX, minZ), (minX, maxZ), (maxX, minZ), (maxX, maxZ) })
            {
                var r = x * probe.RightX + z * probe.RightZ;
                var u = x * probe.UpX + z * probe.UpZ;
                r0 = Math.Min(r0, r);
                r1 = Math.Max(r1, r);
                u0 = Math.Min(u0, u);
                u1 = Math.Max(u1, u);
            }
            var w = Math.Max(1f, r1 - r0);
            var h = Math.Max(1f, u1 - u0);
            var scale = Math.Min(screen.W / w, screen.H / h) * (1f - 2f * margin);
            var cr = (r0 + r1) / 2f;
            var cu = (u0 + u1) / 2f;
            var cx = cr * probe.RightX + cu * probe.UpX;
            var cz = cr * probe.RightZ + cu * probe.UpZ;
            return new MapView(screen, cx, cz, scale, probe.UpX, probe.UpZ);
        }
    }
}
