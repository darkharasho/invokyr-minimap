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

        public Box Intersect(Box o)
        {
            var x = Math.Max(X, o.X);
            var y = Math.Max(Y, o.Y);
            return new Box(x, y, Math.Min(XMax, o.XMax) - x, Math.Min(YMax, o.YMax) - y);
        }
    }

    /// <summary>
    /// Top-down projection of the world's X/Z plane into a screen box: world X goes right, world Z (north) goes up.
    /// <see cref="Scale"/> is pixels per metre around the world point <see cref="CenterX"/>/<see cref="CenterZ"/>.
    /// </summary>
    internal readonly struct MapView
    {
        public readonly Box Screen;
        public readonly float CenterX, CenterZ, Scale;

        public MapView(Box screen, float centerX, float centerZ, float scale)
        {
            Screen = screen;
            CenterX = centerX;
            CenterZ = centerZ;
            Scale = scale;
        }

        /// <summary>A view showing world X/Z from min to max with a margin, keeping metres square.</summary>
        public static MapView Fit(Box screen, float minX, float minZ, float maxX, float maxZ, float margin = 0.06f)
        {
            var w = Math.Max(1f, maxX - minX);
            var h = Math.Max(1f, maxZ - minZ);
            var scale = Math.Min(screen.W / w, screen.H / h) * (1f - 2f * margin);
            return new MapView(screen, (minX + maxX) / 2f, (minZ + maxZ) / 2f, scale);
        }

        public float ScreenX(float worldX) => Screen.X + Screen.W / 2f + (worldX - CenterX) * Scale;

        public float ScreenY(float worldZ) => Screen.Y + Screen.H / 2f - (worldZ - CenterZ) * Scale;

        /// <summary>The screen box of a world X/Z rectangle, clipped to the view (Empty if outside).</summary>
        public Box Project(float minX, float minZ, float maxX, float maxZ)
        {
            var box = new Box(ScreenX(minX), ScreenY(maxZ), (maxX - minX) * Scale, (maxZ - minZ) * Scale);
            return box.Intersect(Screen);
        }
    }
}
