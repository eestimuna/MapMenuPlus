using System;
using System.Collections.Generic;

namespace MapMenuPlus
{
    /// <summary>A picture as RGBA bytes, row 0 = BOTTOM (Unity's order).</summary>
    internal sealed class Pic
    {
        internal readonly int W, H;
        internal readonly byte[] Px;
        internal Pic(int w, int h) { W = w; H = h; Px = new byte[w * h * 4]; }
        internal Pic(int w, int h, byte[] px) { W = w; H = h; Px = px; }
    }

    /// <summary>An RGBA colour 0..1 (no Unity type, so this file is tested on its own).</summary>
    internal struct Rgba
    {
        internal float R, G, B, A;
        internal Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        internal static Rgba Bytes(int r, int g, int b, int a = 255) { return new Rgba(r / 255f, g / 255f, b / 255f, a / 255f); }
        internal static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);
        internal static Rgba Lerp(Rgba a, Rgba b, float t) { return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t); }
    }

    /// <summary>
    /// Draws the menu's shapes in code (tested on its own, no Unity): rounded rectangles with smooth (anti-aliased)
    /// corners, colour gradients, circles, rings, arcs and the little white icons (person, star, clock, list, back
    /// arrow, gear, magnifier, chevron, check, X, ...). White shapes are tinted in the game (RawImage colour), so one
    /// picture serves every colour. The game's own UI pieces have no round corners - these pictures give them.
    /// </summary>
    internal static class Shapes
    {
        // ---- rounded rectangles ---------------------------------------------------------------------------

        /// <summary>
        /// A rounded rectangle filling the whole picture. Radii in PIXELS per corner (top-left, top-right, bottom-right,
        /// bottom-left). Colour: top-left to right gradient (left, right) and optional vertical mix.
        /// </summary>
        internal static Pic RoundRect(int w, int h, float rTL, float rTR, float rBR, float rBL, Rgba left, Rgba right)
        {
            var pic = new Pic(w, h);
            float hw = w / 2f, hh = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - hw, py = y + 0.5f - hh;   // centre-relative, y up
                    float r = px < 0 ? (py > 0 ? rTL : rBL) : (py > 0 ? rTR : rBR);
                    float d = BoxSdf(px, py, hw, hh, r);
                    float cov = Clamp01(0.5f - d);
                    float t = w > 1 ? (x + 0.5f) / w : 0f;
                    Put(pic, x, y, Rgba.Lerp(left, right, t), cov);
                }
            return pic;
        }

        internal static Pic RoundRect(int w, int h, float r, Rgba c) { return RoundRect(w, h, r, r, r, r, c, c); }

        /// <summary>Only the outline of a rounded rectangle (checkbox, outlined button), thickness in pixels.</summary>
        internal static Pic RoundRectOutline(int w, int h, float r, float thickness, Rgba c)
        {
            var pic = new Pic(w, h);
            float hw = w / 2f, hh = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - hw, py = y + 0.5f - hh;
                    float d = BoxSdf(px, py, hw - thickness / 2f, hh - thickness / 2f, Math.Max(0f, r - thickness / 2f));
                    float cov = Clamp01(0.5f - (Math.Abs(d) - thickness / 2f));
                    Put(pic, x, y, c, cov);
                }
            return pic;
        }

        private static float BoxSdf(float px, float py, float hw, float hh, float r)
        {
            r = Math.Min(r, Math.Min(hw, hh));
            float qx = Math.Abs(px) - (hw - r), qy = Math.Abs(py) - (hh - r);
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        // ---- circles ----------------------------------------------------------------------------------------

        /// <summary>A filled circle (size x size pixels).</summary>
        internal static Pic Circle(int size, Rgba c)
        {
            var pic = new Pic(size, size);
            float R = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - R, dy = y + 0.5f - R;
                    float cov = Clamp01(0.5f - ((float)Math.Sqrt(dx * dx + dy * dy) - R + 0.5f));
                    Put(pic, x, y, c, cov);
                }
            return pic;
        }

        /// <summary>A ring; fraction &lt; 1 = an arc starting at the top, going clockwise, with round ends.</summary>
        internal static Pic Ring(int size, float thickness, float fraction, Rgba c)
        {
            var pic = new Pic(size, size);
            float C = size / 2f;
            float R = C - thickness / 2f - 0.5f;
            float span = Clamp01(fraction) * (float)(2 * Math.PI);
            float ax0 = 0f, ay0 = R;                                           // start: top
            float ax1 = (float)Math.Sin(span) * R, ay1 = (float)Math.Cos(span) * R;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - C, dy = y + 0.5f - C;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    float d;
                    if (fraction >= 0.999f) d = Math.Abs(len - R) - thickness / 2f;
                    else
                    {
                        double ang = Math.Atan2(dx, dy);                       // clockwise from the top
                        if (ang < 0) ang += 2 * Math.PI;
                        if (ang <= span) d = Math.Abs(len - R) - thickness / 2f;
                        else
                        {
                            float d0 = Dist(dx, dy, ax0, ay0), d1 = Dist(dx, dy, ax1, ay1);
                            d = Math.Min(d0, d1) - thickness / 2f;
                        }
                    }
                    float cov = Clamp01(0.5f - d);
                    Put(pic, x, y, c, cov);
                }
            return pic;
        }

        // ---- icons (drawn with 4x4 samples per pixel for smooth edges) -------------------------------------------

        internal delegate bool Inside(float x, float y);   // x, y in -1..1 (y up)

        internal static Pic Icon(int size, Inside inside, Rgba c)
        {
            var pic = new Pic(size, size);
            const int S = 4;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int n = 0;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / S) / size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / S) / size) * 2f - 1f;
                            if (inside(u, v)) n++;
                        }
                    Put(pic, x, y, c, n / (float)(S * S));
                }
            return pic;
        }

        // shape helpers for icons (coordinates -1..1)
        internal static bool Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float t = Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
            float dx = px - vx * t, dy = py - vy * t;
            return dx * dx + dy * dy <= r * r;
        }

        internal static bool Disc(float x, float y, float cx, float cy, float r) { float dx = x - cx, dy = y - cy; return dx * dx + dy * dy <= r * r; }

        internal static bool RingAt(float x, float y, float cx, float cy, float r, float t)
        {
            float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Math.Abs(d - r) <= t / 2f;
        }

        internal static bool Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            return BoxSdf(x - cx, y - cy, hw, hh, r) <= 0f;
        }

        private static readonly float[] StarPts = MakeStar();

        private static float[] MakeStar()
        {
            var p = new float[20];
            for (int i = 0; i < 10; i++)
            {
                double a = Math.PI / 2 + i * Math.PI / 5;   // top point first
                double r = (i % 2 == 0) ? 0.92 : 0.40;
                p[i * 2] = (float)(Math.Cos(a) * r);
                p[i * 2 + 1] = (float)(Math.Sin(a) * r) - 0.06f;
            }
            return p;
        }

        internal static bool InStar(float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                float xi = StarPts[i * 2], yi = StarPts[i * 2 + 1], xj = StarPts[j * 2], yj = StarPts[j * 2 + 1];
                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance from the star's edge (for the outlined star).</summary>
        internal static float StarEdge(float x, float y)
        {
            float best = float.MaxValue;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                float ax = StarPts[j * 2], ay = StarPts[j * 2 + 1], bx = StarPts[i * 2], by = StarPts[i * 2 + 1];
                float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
                float t = Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
                float dx = px - vx * t, dy = py - vy * t;
                best = Math.Min(best, dx * dx + dy * dy);
            }
            return (float)Math.Sqrt(best);
        }

        /// <summary>Every icon of the menu, by name (white, size x size pixels).</summary>
        internal static Pic NamedIcon(string name, int size)
        {
            Rgba w = Rgba.White;
            switch (name)
            {
                case "person":   // the "Jobs" icon: head + shoulders
                    return Icon(size, (x, y) => Disc(x, y, 0f, 0.38f, 0.36f) || (Box(x, y, 0f, -0.55f, 0.72f, 0.38f, 0.30f) && y < -0.18f), w);
                case "grid":     // dashboard: four tiles
                    return Icon(size, (x, y) => Box(x, y, -0.45f, 0.45f, 0.38f, 0.38f, 0.1f) || Box(x, y, 0.45f, 0.45f, 0.38f, 0.38f, 0.1f)
                                             || Box(x, y, -0.45f, -0.45f, 0.38f, 0.38f, 0.1f) || Box(x, y, 0.45f, -0.45f, 0.38f, 0.38f, 0.1f), w);
                case "star":
                    return Icon(size, InStar, w);
                case "star_outline":
                    return Icon(size, (x, y) => InStar(x, y) ? StarEdge(x, y) <= 0.13f : StarEdge(x, y) <= 0.03f, w);
                case "clock":
                    return Icon(size, (x, y) => RingAt(x, y, 0f, 0f, 0.78f, 0.18f) || Seg(x, y, 0f, 0f, 0f, 0.46f, 0.085f) || Seg(x, y, 0f, 0f, 0.34f, -0.2f, 0.085f), w);
                case "list":
                    return Icon(size, (x, y) => Disc(x, y, -0.72f, 0.55f, 0.12f) || Disc(x, y, -0.72f, 0f, 0.12f) || Disc(x, y, -0.72f, -0.55f, 0.12f)
                                             || Seg(x, y, -0.38f, 0.55f, 0.8f, 0.55f, 0.1f) || Seg(x, y, -0.38f, 0f, 0.8f, 0f, 0.1f) || Seg(x, y, -0.38f, -0.55f, 0.8f, -0.55f, 0.1f), w);
                case "back":     // arrow pointing left
                    return Icon(size, (x, y) => Seg(x, y, -0.7f, 0f, 0.75f, 0f, 0.1f) || Seg(x, y, -0.7f, 0f, -0.2f, 0.5f, 0.1f) || Seg(x, y, -0.7f, 0f, -0.2f, -0.5f, 0.1f), w);
                case "gear":     // 8 rounded teeth around a ring
                    return Icon(size, (x, y) =>
                    {
                        float r = (float)Math.Sqrt(x * x + y * y);
                        if (r >= 0.30f && r <= 0.66f) return true;
                        for (int k = 0; k < 8; k++)
                        {
                            double a = k * Math.PI / 4;
                            float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                            float rx = x * c + y * sn, ry = -x * sn + y * c;   // rotate so this tooth points along +x
                            if (Box(rx, ry, 0.74f, 0f, 0.2f, 0.15f, 0.06f)) return true;
                        }
                        return false;
                    }, w);
                case "search":   // magnifier
                    return Icon(size, (x, y) => RingAt(x, y, -0.15f, 0.15f, 0.52f, 0.17f) || Seg(x, y, 0.24f, -0.24f, 0.78f, -0.78f, 0.1f), w);
                case "chevron_up":
                    return Icon(size, (x, y) => Seg(x, y, -0.62f, -0.25f, 0f, 0.37f, 0.1f) || Seg(x, y, 0f, 0.37f, 0.62f, -0.25f, 0.1f), w);
                case "chevron_down":
                    return Icon(size, (x, y) => Seg(x, y, -0.62f, 0.25f, 0f, -0.37f, 0.1f) || Seg(x, y, 0f, -0.37f, 0.62f, 0.25f, 0.1f), w);
                case "chevron_left":
                    return Icon(size, (x, y) => Seg(x, y, 0.25f, -0.62f, -0.37f, 0f, 0.1f) || Seg(x, y, -0.37f, 0f, 0.25f, 0.62f, 0.1f), w);
                case "chevron_right":
                    return Icon(size, (x, y) => Seg(x, y, -0.25f, -0.62f, 0.37f, 0f, 0.1f) || Seg(x, y, 0.37f, 0f, -0.25f, 0.62f, 0.1f), w);
                case "check":
                    return Icon(size, (x, y) => Seg(x, y, -0.6f, 0.02f, -0.18f, -0.42f, 0.13f) || Seg(x, y, -0.18f, -0.42f, 0.62f, 0.45f, 0.13f), w);
                case "close":
                    return Icon(size, (x, y) => Seg(x, y, -0.62f, -0.62f, 0.62f, 0.62f, 0.07f) || Seg(x, y, -0.62f, 0.62f, 0.62f, -0.62f, 0.07f), w);
                case "eye":
                    return Icon(size, (x, y) =>
                    {
                        float lens = (x * x) / 0.81f + (y * y) / 0.25f;
                        return (lens <= 1f && lens >= 0.62f) || Disc(x, y, 0f, 0f, 0.24f);
                    }, w);
                case "play":     // triangle pointing right (slightly rounded by the sampling)
                    return Icon(size, (x, y) => InTri(x, y, -0.48f, 0.72f, -0.48f, -0.72f, 0.78f, 0f), w);
                case "download": // arrow down onto a line
                    return Icon(size, (x, y) => Seg(x, y, 0f, 0.78f, 0f, -0.28f, 0.11f) || Seg(x, y, 0f, -0.32f, -0.42f, 0.1f, 0.11f)
                                             || Seg(x, y, 0f, -0.32f, 0.42f, 0.1f, 0.11f) || Seg(x, y, -0.7f, -0.74f, 0.7f, -0.74f, 0.11f), w);
                case "dice":     // a die showing five
                    return Icon(size, (x, y) => (Box(x, y, 0f, 0f, 0.84f, 0.84f, 0.26f) && !Box(x, y, 0f, 0f, 0.66f, 0.66f, 0.12f))
                                             || Disc(x, y, 0f, 0f, 0.13f) || Disc(x, y, -0.34f, 0.34f, 0.13f) || Disc(x, y, 0.34f, 0.34f, 0.13f)
                                             || Disc(x, y, -0.34f, -0.34f, 0.13f) || Disc(x, y, 0.34f, -0.34f, 0.13f), w);
                case "map":      // a folded map: three slanted panels
                    return Icon(size, (x, y) =>
                    {
                        if (x < -0.86f || x > 0.86f) return false;
                        float top = 0.62f + (x < -0.29f ? (x + 0.86f) / 0.57f * 0.16f : x < 0.29f ? 0.16f - (x + 0.29f) / 0.58f * 0.16f : (x - 0.29f) / 0.57f * 0.16f);
                        float bottom = top - 1.32f;
                        if (y > top || y < bottom) return false;
                        return Math.Abs(x + 0.29f) > 0.045f && Math.Abs(x - 0.29f) > 0.045f;
                    }, w);
                case "cube":     // mods: an isometric cube outline
                    return Icon(size, (x, y) => Seg(x, y, 0f, 0.82f, 0.74f, 0.42f, 0.09f) || Seg(x, y, 0.74f, 0.42f, 0.74f, -0.42f, 0.09f)
                                             || Seg(x, y, 0.74f, -0.42f, 0f, -0.82f, 0.09f) || Seg(x, y, 0f, -0.82f, -0.74f, -0.42f, 0.09f)
                                             || Seg(x, y, -0.74f, -0.42f, -0.74f, 0.42f, 0.09f) || Seg(x, y, -0.74f, 0.42f, 0f, 0.82f, 0.09f)
                                             || Seg(x, y, -0.74f, 0.42f, 0f, 0f, 0.09f) || Seg(x, y, 0f, 0f, 0.74f, 0.42f, 0.09f) || Seg(x, y, 0f, 0f, 0f, -0.82f, 0.09f), w);
                case "spark":    // "not played yet": a four-pointed sparkle
                    return Icon(size, (x, y) => { float ax = Math.Abs(x), ay = Math.Abs(y); return (float)Math.Sqrt(ax) + (float)Math.Sqrt(ay) <= 0.95f; }, w);
                case "diamond":  // BONELAB's own maps
                    return Icon(size, (x, y) => Math.Abs(x) + Math.Abs(y) <= 0.82f && !(Math.Abs(x) + Math.Abs(y) <= 0.5f) || Math.Abs(x) + Math.Abs(y) <= 0.22f, w);
                case "heart":
                    return Icon(size, (x, y) => Disc(x, y, -0.36f, 0.28f, 0.4f) || Disc(x, y, 0.36f, 0.28f, 0.4f) || InTri(x, y, -0.74f, 0.12f, 0.74f, 0.12f, 0f, -0.8f), w);
                case "user":     // author: head + shoulders, thinner than "person"
                    return Icon(size, (x, y) => Disc(x, y, 0f, 0.42f, 0.32f) || (Disc(x, y, 0f, -0.62f, 0.68f) && y > -0.78f && y < -0.12f), w);
                default:
                    return Icon(size, (x, y) => Disc(x, y, 0f, 0f, 0.5f), w);
            }
        }

        internal static readonly string[] IconNames =
            { "person", "grid", "star", "star_outline", "clock", "list", "back", "gear", "search", "chevron_up", "chevron_down", "check", "close", "eye",
              "play", "download", "dice", "map", "cube", "spark", "diamond", "heart", "user", "chevron_left", "chevron_right" };

        /// <summary>Point in triangle (any winding).</summary>
        internal static bool InTri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---- round photos -----------------------------------------------------------------------------------------

        /// <summary>
        /// Cuts a square photo (RGBA, row 0 = bottom) into a circle with a smooth edge (outside = transparent).
        /// Done in place.
        /// </summary>
        internal static void CircleCut(byte[] px, int size)
        {
            float R = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - R, dy = y + 0.5f - R;
                    float cov = Clamp01(0.5f - ((float)Math.Sqrt(dx * dx + dy * dy) - R + 0.75f));
                    int p = (y * size + x) * 4;
                    px[p + 3] = (byte)(px[p + 3] * cov + 0.5f);
                }
        }

        /// <summary>Linear (0..1) to normal sRGB (0..1).</summary>
        internal static float Encode(float v)
        {
            if (v <= 0f) return 0f;
            if (v >= 1f) return 1f;
            return v <= 0.0031308f ? v * 12.92f : 1.055f * (float)Math.Pow(v, 1.0 / 2.4) - 0.055f;
        }

        internal static float Decode(float v)
        {
            if (v <= 0f) return 0f;
            if (v >= 1f) return 1f;
            return v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4);
        }

        /// <summary>
        /// Photos are rendered in BONELAB's linear colour space and can come back holding LINEAR values (too dark, too
        /// saturated). The studio background has a known colour, so a corner pixel tells which it is: true = linear.
        /// </summary>
        internal static bool LooksLinear(byte r, byte g, byte b, float br, float bg, float bb)
        {
            float dS = Math.Max(Math.Abs(r - br * 255f), Math.Max(Math.Abs(g - bg * 255f), Math.Abs(b - bb * 255f)));
            float dL = Math.Max(Math.Abs(r - Decode(br) * 255f), Math.Max(Math.Abs(g - Decode(bg) * 255f), Math.Abs(b - Decode(bb) * 255f)));
            return dL <= 6f && dL < dS;
        }

        /// <summary>Linear values to normal colours, in place (alpha untouched).</summary>
        internal static void LinearToNormal(byte[] px)
        {
            var lut = new byte[256];
            for (int i = 0; i < 256; i++) lut[i] = (byte)(Encode(i / 255f) * 255f + 0.5f);
            for (int p = 0; p < px.Length; p += 4)
            {
                px[p] = lut[px[p]]; px[p + 1] = lut[px[p + 1]]; px[p + 2] = lut[px[p + 2]];
            }
        }

        /// <summary>The columns x0 .. x0+w of a picture (e.g. the left edge of a rounded card = its colour stripe).</summary>
        internal static Pic Crop(Pic src, int x0, int w)
        {
            var pic = new Pic(w, src.H);
            for (int y = 0; y < src.H; y++)
                Buffer.BlockCopy(src.Px, (y * src.W + x0) * 4, pic.Px, y * w * 4, w * 4);
            return pic;
        }

        /// <summary>A top-to-bottom colour gradient (row 0 = bottom), e.g. the dark fade under the text on a picture.</summary>
        internal static Pic VGradient(int w, int h, Rgba top, Rgba bottom)
        {
            var pic = new Pic(w, h);
            for (int y = 0; y < h; y++)
            {
                float t = h > 1 ? (y + 0.5f) / h : 0f;       // 0 = bottom row
                Rgba c = Rgba.Lerp(bottom, top, t);
                for (int x = 0; x < w; x++) Put(pic, x, y, c, 1f);
            }
            return pic;
        }

        /// <summary>
        /// The picture of a map that has none (BONELAB's own maps, or no mod.io picture): a soft diagonal light with faint
        /// contour lines, in white/grey so it can be tinted with the map's own colour in the game.
        /// </summary>
        internal static Pic MapArt(int w, int h)
        {
            var pic = new Pic(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    // light from the top left
                    float light = 0.55f + 0.45f * Clamp01(1.15f - (u * 0.75f + (1f - v) * 0.85f));
                    // contour lines of a few soft "hills"
                    double hgt = Math.Sin(u * 7.1 + 1.3) * 0.5 + Math.Cos(v * 5.3 - u * 2.2) * 0.6 + Math.Sin((u + v) * 9.7) * 0.18;
                    double f = hgt * 6.0 - Math.Floor(hgt * 6.0);
                    float line = (float)Math.Max(0.0, 1.0 - Math.Abs(f - 0.5) * 2.0 / 0.16);   // thin band around .5
                    float g = Clamp01(light * (1f - 0.13f * line));
                    // vignette
                    float dx = u - 0.5f, dy = v - 0.5f;
                    g *= 1f - 0.35f * Clamp01((dx * dx + dy * dy) * 2.2f);
                    Put(pic, x, y, new Rgba(g, g, g, 1f), 1f);
                }
            return pic;
        }

        /// <summary>A left-to-right colour gradient (stretched over a whole area in the game).</summary>
        internal static Pic Gradient(int w, Rgba left, Rgba right) { return RoundRect(w, 4, 0f, 0f, 0f, 0f, left, right); }

        // ---- helpers ---------------------------------------------------------------------------------------------

        private static float Dist(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return (float)Math.Sqrt(dx * dx + dy * dy); }
        internal static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

        /// <summary>
        /// Colour with coverage, stored straight (not premultiplied) as Unity expects. Every pixel gets the colour, even
        /// fully transparent ones - otherwise smooth edges would fade through black when the texture is filtered.
        /// </summary>
        private static void Put(Pic pic, int x, int y, Rgba c, float cov)
        {
            int p = (y * pic.W + x) * 4;
            pic.Px[p] = ToByte(c.R);
            pic.Px[p + 1] = ToByte(c.G);
            pic.Px[p + 2] = ToByte(c.B);
            pic.Px[p + 3] = ToByte(c.A * cov);
        }

        private static byte ToByte(float v) { return (byte)(v <= 0f ? 0 : v >= 1f ? 255 : (int)(v * 255f + 0.5f)); }
    }
}
