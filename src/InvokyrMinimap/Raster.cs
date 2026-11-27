using System;

namespace InvokyrMinimap
{
    internal readonly struct Rgba
    {
        public readonly byte R, G, B, A;
        public Rgba(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }
        public static Rgba Of(float r, float g, float b, float a) =>
            new Rgba((byte)(r * 255f + 0.5f), (byte)(g * 255f + 0.5f), (byte)(b * 255f + 0.5f), (byte)(a * 255f + 0.5f));
    }

    /// <summary>
    /// An RGBA32 pixel buffer drawn on the CPU, so the map can be turned without IMGUI clipping. Coordinates are GUI-style
    /// (y down) but rows are stored bottom-up, the way Texture2D.LoadRawTextureData expects them.
    /// </summary>
    internal sealed class Raster
    {
        public readonly int Width, Height;
        public readonly byte[] Pixels;

        public Raster(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        public void Clear() => Array.Clear(Pixels, 0, Pixels.Length);

        private int Offset(int x, int y) => ((Height - 1 - y) * Width + x) * 4;

        public Rgba Get(int x, int y)
        {
            var o = Offset(x, y);
            return new Rgba(Pixels[o], Pixels[o + 1], Pixels[o + 2], Pixels[o + 3]);
        }

        public void Set(int x, int y, Rgba c)
        {
            var o = Offset(x, y);
            Pixels[o] = c.R;
            Pixels[o + 1] = c.G;
            Pixels[o + 2] = c.B;
            Pixels[o + 3] = c.A;
        }

        /// <summary>Draws c over the pixel ("over" blending, straight alpha).</summary>
        public void Blend(int x, int y, Rgba c)
        {
            if (c.A == 255)
            {
                Set(x, y, c);
                return;
            }
            if (c.A == 0)
                return;
            var o = Offset(x, y);
            var sa = c.A / 255f;
            var da = Pixels[o + 3] / 255f;
            var oa = sa + da * (1f - sa);
            Pixels[o] = Mix(c.R, Pixels[o], sa, da, oa);
            Pixels[o + 1] = Mix(c.G, Pixels[o + 1], sa, da, oa);
            Pixels[o + 2] = Mix(c.B, Pixels[o + 2], sa, da, oa);
            Pixels[o + 3] = (byte)(oa * 255f + 0.5f);
        }

        private static byte Mix(byte s, byte d, float sa, float da, float oa) =>
            (byte)((s * sa + d * da * (1f - sa)) / oa + 0.5f);

        /// <summary>Fills a convex polygon (any winding), sampling pixel centres.</summary>
        public void FillConvex(ReadOnlySpan<float> xs, ReadOnlySpan<float> ys, Rgba c)
        {
            var n = xs.Length;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (var i = 0; i < n; i++)
            {
                minY = Math.Min(minY, ys[i]);
                maxY = Math.Max(maxY, ys[i]);
            }
            var y0 = Math.Max(0, (int)Math.Ceiling(minY - 0.5f));
            var y1 = Math.Min(Height - 1, (int)Math.Floor(maxY - 0.5f));
            for (var y = y0; y <= y1; y++)
            {
                var sy = y + 0.5f;
                float left = float.MaxValue, right = float.MinValue;
                for (var i = 0; i < n; i++)
                {
                    float ax = xs[i], ay = ys[i], bx = xs[(i + 1) % n], by = ys[(i + 1) % n];
                    if ((sy < ay && sy < by) || (sy > ay && sy > by) || ay == by)
                        continue;
                    var x = ax + (sy - ay) / (by - ay) * (bx - ax);
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
                if (left > right)
                    continue;
                var x0 = Math.Max(0, (int)Math.Ceiling(left - 0.5f));
                var x1 = Math.Min(Width - 1, (int)Math.Floor(right - 0.5f));
                for (var x = x0; x <= x1; x++)
                    Blend(x, y, c);
            }
        }

        public void FillCircle(float cx, float cy, float radius, Rgba c)
        {
            var y0 = Math.Max(0, (int)Math.Floor(cy - radius));
            var y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + radius));
            var x0 = Math.Max(0, (int)Math.Floor(cx - radius));
            var x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + radius));
            var r2 = radius * radius;
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2)
                        Blend(x, y, c);
                }
            }
        }

        public void FillSquare(float cx, float cy, float half, Rgba c)
        {
            Span<float> xs = stackalloc float[] { cx - half, cx + half, cx + half, cx - half };
            Span<float> ys = stackalloc float[] { cy - half, cy - half, cy + half, cy + half };
            FillConvex(xs, ys, c);
        }

        public void FillDiamond(float cx, float cy, float half, Rgba c)
        {
            Span<float> xs = stackalloc float[] { cx, cx + half, cx, cx - half };
            Span<float> ys = stackalloc float[] { cy - half, cy, cy + half, cy };
            FillConvex(xs, ys, c);
        }

        /// <summary>
        /// An arrowhead at (cx, cy) pointing along screen direction (dx, dy), <paramref name="length"/> pixels from tip
        /// to tail, with a notch at the back. Drawn as two triangles since the shape isn't convex.
        /// </summary>
        public void FillArrow(float cx, float cy, float dx, float dy, float length, Rgba c)
        {
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f)
            {
                dx = 0f;
                dy = -1f;
                len = 1f;
            }
            dx /= len;
            dy /= len;
            float px = -dy, py = dx; // perpendicular
            var tipX = cx + dx * length * 0.55f;
            var tipY = cy + dy * length * 0.55f;
            var backX = cx - dx * length * 0.45f;
            var backY = cy - dy * length * 0.45f;
            var w = length * 0.42f;
            var notchX = cx - dx * length * 0.15f;
            var notchY = cy - dy * length * 0.15f;
            Span<float> xs = stackalloc float[3];
            Span<float> ys = stackalloc float[3];
            xs[0] = tipX; ys[0] = tipY;
            xs[1] = backX + px * w; ys[1] = backY + py * w;
            xs[2] = notchX; ys[2] = notchY;
            FillConvex(xs, ys, c);
            xs[1] = backX - px * w; ys[1] = backY - py * w;
            FillConvex(xs, ys, c);
        }
    }
}
