using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// All textures are generated at start-up (no external art needed, nothing copied).
    /// Noise is periodic so every texture tiles seamlessly.
    /// </summary>
    public static class ProceduralTextures
    {
        // ---------------------------------------------------------------- tileable noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Wrap(int v, int p) { int r = v % p; return r < 0 ? r + p : r; }

        /// <summary>Periodic value noise in [0,1]; period in lattice cells.</summary>
        public static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fBm; u,v in [0,1).</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += TileNoise(u * period, v * period, period, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static Texture2D NewTex(int w, int h, string name, bool mips = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, false);
            t.name = name;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            return t;
        }

        static Texture2D Finish(Texture2D t, Color[] px)
        {
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        // ---------------------------------------------------------------- textures

        /// <summary>Road surface. U = across the road (0..1), V = along (repeats every 12 m).</summary>
        public static Texture2D Asphalt()
        {
            const int W = 512, H = 512;
            var t = NewTex(W, H, "Asphalt");
            var px = new Color[W * H];
            var rng = new System.Random(7);
            float edge = 0.6f / (TrackPath.HalfWidth * 2f); // 0.6 m line
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)H;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float n = Fbm(u, v, 8, 5, 3);
                    float grain = TileNoise(u * 256, v * 256, 256, 11);
                    float g = 0.40f + (n - 0.5f) * 0.10f + (grain - 0.5f) * 0.06f;
                    // darker tyre wear lanes
                    float lane = Mathf.Exp(-Mathf.Pow((Mathf.Abs(u - 0.5f) - 0.22f) / 0.06f, 2f));
                    g -= lane * 0.035f;
                    if (rng.NextDouble() < 0.004) g += 0.12f;
                    var c = new Color(g, g * 1.01f, g * 1.04f, 1f);

                    // white edge lines
                    float distEdge = Mathf.Min(u, 1f - u);
                    if (distEdge > 0.02f && distEdge < 0.02f + edge)
                    {
                        float wear = Fbm(u * 3f, v, 16, 3, 9);
                        c = Color.Lerp(c, new Color(0.95f, 0.95f, 0.93f), 0.85f + wear * 0.15f);
                    }
                    // dashed centre line
                    if (Mathf.Abs(u - 0.5f) < edge * 0.35f && (v % 0.5f) < 0.25f)
                        c = Color.Lerp(c, new Color(0.96f, 0.84f, 0.25f), 0.9f);
                    px[y * W + x] = c;
                }
            }
            return Finish(t, px);
        }

        /// <summary>Red / white barrier stripes. V runs along the track (repeats every 8 m).</summary>
        public static Texture2D BarrierStripes()
        {
            const int W = 64, H = 256;
            var t = NewTex(W, H, "Barrier");
            var px = new Color[W * H];
            Color red = new Color(0.86f, 0.15f, 0.14f), white = new Color(0.96f, 0.96f, 0.96f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)H;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    Color c = v < 0.5f ? red : white;
                    float n = Fbm(u, v, 4, 3, 5);
                    c *= 0.94f + n * 0.08f;
                    // dark trim at the top and bottom of the barrier face
                    if (u > 0.93f || u < 0.04f) c *= 0.72f;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }
            return Finish(t, px);
        }

        /// <summary>Light concrete with panel seams (pillars, track underside, gate bases).</summary>
        public static Texture2D Concrete()
        {
            const int S = 256;
            var t = NewTex(S, S, "Concrete");
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 8, 5, 21);
                    float g = 0.80f + (n - 0.5f) * 0.14f;
                    if (y % 128 < 2) g *= 0.82f;
                    if (x % 256 < 2) g *= 0.86f;
                    px[y * S + x] = new Color(g, g * 0.99f, g * 0.97f, 1f);
                }
            return Finish(t, px);
        }

        /// <summary>Black / white checkers (start, finish).</summary>
        public static Texture2D Checker(int cellsX, int cellsY)
        {
            int W = cellsX * 32, H = cellsY * 32;
            var t = NewTex(W, H, "Checker");
            t.filterMode = FilterMode.Bilinear;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool w = ((x / 32) + (y / 32)) % 2 == 0;
                    float g = w ? 0.95f : 0.08f;
                    px[y * W + x] = new Color(g, g, g, 1f);
                }
            return Finish(t, px);
        }

        /// <summary>Boost pad: glowing chevrons pointing along +V.</summary>
        public static Texture2D BoostPad()
        {
            const int S = 256;
            var t = NewTex(S, S, "Boost");
            var px = new Color[S * S];
            Color baseC = new Color(0.16f, 0.13f, 0.20f);
            Color a = new Color(1f, 0.42f, 0.05f), b = new Color(1f, 0.88f, 0.25f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float cx = Mathf.Abs(u - 0.5f);
                    // chevron: v phase shifted by distance from centre
                    float phase = (v + cx * 0.9f) * 2f;
                    phase -= Mathf.Floor(phase);
                    Color c = baseC;
                    if (cx < 0.42f && phase < 0.42f)
                        c = Color.Lerp(a, b, phase / 0.42f);
                    if (cx > 0.46f) c = new Color(0.95f, 0.95f, 0.95f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Checkpoint stripe on the road (cyan / white).</summary>
        public static Texture2D CheckpointStripe()
        {
            const int W = 256, H = 64;
            var t = NewTex(W, H, "CheckpointStripe");
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool s = ((x + y) / 16) % 2 == 0;
                    Color c = s ? new Color(0.15f, 0.75f, 1f) : new Color(0.95f, 0.98f, 1f);
                    px[y * W + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Subtle grey-scale detail multiplied over vertex-coloured ground.</summary>
        public static Texture2D GroundDetail()
        {
            const int S = 256;
            var t = NewTex(S, S, "GroundDetail");
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 4, 5, 41);
                    float fine = TileNoise(u * 128, v * 128, 128, 43);
                    float g = 0.86f + (n - 0.5f) * 0.18f + (fine - 0.5f) * 0.08f;
                    px[y * S + x] = new Color(g, g, g, 1f);
                }
            return Finish(t, px);
        }

        /// <summary>Soft round particle sprite.</summary>
        public static Texture2D SoftDot()
        {
            const int S = 64;
            var t = NewTex(S, S, "SoftDot", false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3 - 2 * a);
                    px[y * S + x] = new Color(1, 1, 1, a);
                }
            return Finish(t, px);
        }

        public static Texture2D Solid(Color c)
        {
            var t = NewTex(4, 4, "Solid", false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            return Finish(t, px);
        }
    }
}
