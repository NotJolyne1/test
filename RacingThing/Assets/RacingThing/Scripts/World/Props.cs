using UnityEngine;

namespace RacingThing
{
    // Trees, rocks, flags and so on. They're all written into shared MeshBuilders so a whole
    // area of scenery ends up as one mesh.
    public static class Props
    {
        static Color Vary(Color c, System.Random rng, float amount)
        {
            float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        // darken/brighten a colour without touching alpha (alpha < 1 means glow).
        public static Color Shade(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static readonly Color Bark = new Color(0.45f, 0.30f, 0.18f);
        static readonly Color LeafDark = new Color(0.20f, 0.50f, 0.18f);
        static readonly Color LeafLight = new Color(0.47f, 0.75f, 0.25f);

        public static void Tree(MeshBuilder mb, TreeKind kind, Vector3 pos, float scale, System.Random rng)
        {
            Quaternion rot = Quaternion.Euler(0f, R(rng, 0f, 360f), 0f);
            mb.Matrix = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
            switch (kind)
            {
                case TreeKind.Pine: Pine(mb, rng); break;
                case TreeKind.Palm: Palm(mb, pos, rot, scale, rng); break;
                case TreeKind.Bush: Bush(mb, rng); break;
                case TreeKind.Cactus: Cactus(mb, rng); break;
                case TreeKind.Birch: Birch(mb, rng); break;
                default: Round(mb, rng); break;
            }
            mb.Matrix = Matrix4x4.identity;
        }

        static void Round(MeshBuilder mb, System.Random rng)
        {
            float h = R(rng, 2.2f, 3.0f);
            mb.Cylinder(new Vector3(0, -0.3f, 0), 0.32f, 0.22f, h + 0.3f, 6, Vary(Bark, rng, 0.1f), false, false);
            Color dark = Vary(LeafDark, rng, 0.12f), light = Vary(LeafLight, rng, 0.12f);
            System.Func<Vector3, Color> shade = n => Color.Lerp(dark, light, n.y * 0.5f + 0.5f);
            float s = R(rng, 1.9f, 2.5f);
            mb.Blob(new Vector3(0, h + s * 0.7f, 0), new Vector3(s, s * 0.95f, s), 0, 0.18f, rng, dark, shade);
            if (rng.NextDouble() < 0.7)
            {
                float s2 = s * R(rng, 0.55f, 0.75f);
                mb.Blob(new Vector3(R(rng, -0.9f, 0.9f), h + s * 1.35f, R(rng, -0.9f, 0.9f)), new Vector3(s2, s2, s2), 0, 0.2f, rng, dark, shade);
            }
        }

        static void Birch(MeshBuilder mb, System.Random rng)
        {
            float h = R(rng, 3.2f, 4.2f);
            Color white = new Color(0.92f, 0.91f, 0.86f), band = new Color(0.25f, 0.24f, 0.22f);
            mb.Cylinder(new Vector3(0, -0.3f, 0), 0.22f, 0.18f, h * 0.45f, 6, white, false, false);
            mb.Cylinder(new Vector3(0, h * 0.45f - 0.3f, 0), 0.19f, 0.18f, 0.25f, 6, band, false, false);
            mb.Cylinder(new Vector3(0, h * 0.45f - 0.05f, 0), 0.18f, 0.12f, h * 0.55f, 6, white, false, false);
            Color dark = Vary(new Color(0.42f, 0.66f, 0.20f), rng, 0.1f), light = Vary(new Color(0.72f, 0.85f, 0.30f), rng, 0.1f);
            System.Func<Vector3, Color> shade = n => Color.Lerp(dark, light, n.y * 0.5f + 0.5f);
            mb.Blob(new Vector3(0, h + 0.6f, 0), new Vector3(1.5f, 2.3f, 1.5f), 0, 0.15f, rng, dark, shade);
        }

        static void Pine(MeshBuilder mb, System.Random rng)
        {
            mb.Cylinder(new Vector3(0, -0.3f, 0), 0.28f, 0.2f, 1.9f, 6, Vary(Bark, rng, 0.1f), false, false);
            Color baseG = Vary(new Color(0.16f, 0.42f, 0.22f), rng, 0.12f);
            float y = 1.1f, r = R(rng, 2.0f, 2.4f), h = R(rng, 2.6f, 3.0f);
            int layers = rng.NextDouble() < 0.5 ? 3 : 4;
            for (int i = 0; i < layers; i++)
            {
                Color c = Color.Lerp(baseG, new Color(0.3f, 0.6f, 0.3f), i * 0.12f);
                mb.Cylinder(new Vector3(0, y, 0), r, 0f, h, 7, c, true, false, 0, i * 0.4f);
                y += h * 0.5f;
                r *= 0.76f;
                h *= 0.85f;
            }
        }

        static void Bush(MeshBuilder mb, System.Random rng)
        {
            Color dark = Vary(new Color(0.26f, 0.55f, 0.2f), rng, 0.12f), light = Vary(new Color(0.5f, 0.78f, 0.28f), rng, 0.1f);
            System.Func<Vector3, Color> shade = n => Color.Lerp(dark, light, n.y * 0.5f + 0.5f);
            float s = R(rng, 1.0f, 1.5f);
            mb.Blob(new Vector3(0, s * 0.45f, 0), new Vector3(s * 1.3f, s * 0.9f, s * 1.3f), 0, 0.2f, rng, dark, shade);
            if (rng.NextDouble() < 0.5)
            {
                // little flowers
                Color[] flowers = { new Color(1f, 0.35f, 0.4f), new Color(1f, 0.9f, 0.3f), new Color(0.95f, 0.95f, 1f), new Color(0.8f, 0.5f, 1f) };
                Color fc = flowers[rng.Next(flowers.Length)];
                for (int i = 0; i < 5; i++)
                {
                    float a = R(rng, 0f, Mathf.PI * 2f);
                    Vector3 p = new Vector3(Mathf.Cos(a) * s * 0.9f, s * R(rng, 0.5f, 1.1f), Mathf.Sin(a) * s * 0.9f);
                    mb.Box(p, Vector3.one * 0.22f, fc);
                }
            }
        }

        static void Cactus(MeshBuilder mb, System.Random rng)
        {
            Color g = Vary(new Color(0.35f, 0.58f, 0.28f), rng, 0.1f);
            float h = R(rng, 3.0f, 4.5f);
            mb.Cylinder(new Vector3(0, -0.3f, 0), 0.42f, 0.36f, h, 8, g, false, true);
            int arms = rng.Next(1, 3);
            for (int i = 0; i < arms; i++)
            {
                float side = i == 0 ? 1f : -1f;
                float y = h * R(rng, 0.35f, 0.6f);
                mb.Box(new Vector3(side * 0.7f, y, 0), new Vector3(1.0f, 0.5f, 0.5f), g);
                mb.Cylinder(new Vector3(side * 1.05f, y - 0.2f, 0), 0.26f, 0.22f, R(rng, 1.0f, 1.8f), 6, g, false, true);
            }
        }

        static void Palm(MeshBuilder mb, Vector3 pos, Quaternion rot, float scale, System.Random rng)
        {
            Matrix4x4 baseM = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
            Color trunk = Vary(new Color(0.62f, 0.48f, 0.32f), rng, 0.08f);
            Color ring = Shade(trunk, 0.82f);
            float lean = R(rng, 0.25f, 0.55f);
            Vector3 p = new Vector3(0, -0.3f, 0);
            Vector3 dir = Vector3.up;
            int segs = 6;
            float segLen = R(rng, 1.15f, 1.4f);
            for (int i = 0; i < segs; i++)
            {
                Vector3 nd = (Vector3.up + Vector3.forward * lean * (i + 1) / segs).normalized;
                mb.Matrix = baseM * Matrix4x4.TRS(p, Quaternion.FromToRotation(Vector3.up, nd), Vector3.one);
                float r0 = 0.3f - i * 0.025f;
                mb.Cylinder(Vector3.zero, r0, r0 - 0.03f, segLen, 6, (i % 2 == 0) ? trunk : ring, false, false);
                p += nd * segLen;
                dir = nd;
            }
            mb.Matrix = baseM;
            Vector3 top = p;
            Color frondA = Vary(new Color(0.24f, 0.6f, 0.2f), rng, 0.1f);
            Color frondB = Vary(new Color(0.18f, 0.45f, 0.16f), rng, 0.1f);
            int fronds = 8;
            for (int i = 0; i < fronds; i++)
            {
                float a = i * Mathf.PI * 2f / fronds + R(rng, -0.2f, 0.2f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, d) * 0.55f;
                float len = R(rng, 3.0f, 3.8f);
                Vector3 mid = top + d * (len * 0.5f) + Vector3.up * 0.55f;
                Vector3 tip = top + d * len - Vector3.up * R(rng, 0.8f, 1.4f);
                mb.DoubleTri(top, mid + side, mid - side, frondA, frondB);
                mb.DoubleTri(mid + side, tip, mid - side, frondA, frondB);
            }
            Color nut = new Color(0.4f, 0.28f, 0.14f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                mb.Box(top + new Vector3(Mathf.Cos(a) * 0.35f, -0.35f, Mathf.Sin(a) * 0.35f), Vector3.one * 0.32f, nut);
            }
        }

        public static void Rock(MeshBuilder mb, Vector3 pos, float size, Color baseColor, System.Random rng)
        {
            mb.Matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), Vector3.one);
            Color dark = Vary(Shade(baseColor, 0.78f), rng, 0.06f), light = Vary(Shade(baseColor, 1.08f), rng, 0.06f);
            System.Func<Vector3, Color> shade = n => Color.Lerp(dark, light, n.y * 0.5f + 0.5f);
            mb.Blob(new Vector3(0, size * 0.25f, 0), new Vector3(size, size * R(rng, 0.55f, 0.85f), size * R(rng, 0.7f, 1.1f)), 0, 0.28f, rng, baseColor, shade);
            mb.Matrix = Matrix4x4.identity;
        }

        public static void Mesa(MeshBuilder mb, Vector3 pos, float size, Theme theme, System.Random rng)
        {
            Color[] layers = { theme.Rock, theme.Dirt, Shade(theme.Rock, 1.1f), Shade(theme.Sand, 0.9f) };
            float y = -4f;
            float w = size;
            int count = rng.Next(3, 5);
            for (int i = 0; i < count; i++)
            {
                float h = R(rng, 7f, 14f);
                mb.Matrix = Matrix4x4.TRS(pos + new Vector3(R(rng, -2f, 2f), 0, R(rng, -2f, 2f)), Quaternion.Euler(0, R(rng, 0, 360), 0), Vector3.one);
                Color c = layers[i % layers.Length];
                c.a = 1f;
                mb.TaperBox(new Vector3(0, y + h * 0.5f, 0), new Vector3(w, h, w * R(rng, 0.6f, 1f)), R(rng, 0.82f, 0.95f), R(rng, 0.82f, 0.95f), Vary(c, rng, 0.05f));
                y += h;
                w *= R(rng, 0.7f, 0.88f);
            }
            mb.Matrix = Matrix4x4.identity;
        }

        public static void Umbrella(MeshBuilder mb, Vector3 pos, System.Random rng)
        {
            mb.Matrix = Matrix4x4.TRS(pos, Quaternion.Euler(R(rng, -8f, 8f), R(rng, 0f, 360f), 0f), Vector3.one);
            mb.Cylinder(new Vector3(0, -0.3f, 0), 0.05f, 0.05f, 2.7f, 5, new Color(0.9f, 0.9f, 0.9f), false, false);
            Color[] cols = { new Color(1f, 0.3f, 0.3f), new Color(0.2f, 0.6f, 1f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.85f, 0.5f) };
            Color a = cols[rng.Next(cols.Length)], b = Color.white;
            const int sides = 10;
            Vector3 apex = new Vector3(0, 2.9f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 1.7f, 2.25f, Mathf.Sin(a0) * 1.7f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 1.7f, 2.25f, Mathf.Sin(a1) * 1.7f);
                mb.DoubleTri(apex, p0, p1, i % 2 == 0 ? a : b, i % 2 == 0 ? Shade(a, 0.8f) : Shade(b, 0.8f));
            }
            // towel
            mb.Box(new Vector3(1.6f, -0.25f, 0.2f), new Vector3(0.9f, 0.05f, 1.8f), cols[rng.Next(cols.Length)]);
            mb.Matrix = Matrix4x4.identity;
        }

        public static void Flag(MeshBuilder mb, Vector3 pos, Quaternion rot, Color color)
        {
            mb.Matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            mb.Cylinder(new Vector3(0, -0.4f, 0), 0.09f, 0.07f, 6.6f, 6, new Color(0.85f, 0.85f, 0.88f), false, true);
            Vector3 a = new Vector3(0, 6.1f, 0), b = new Vector3(0, 4.5f, 0), c = new Vector3(0.1f, 5.2f, 2.3f);
            Color back = Shade(color, 0.8f);
            mb.DoubleTri(a, b, c, color, back);
            mb.Matrix = Matrix4x4.identity;
        }

        public static void Cloud(MeshBuilder mb, Vector3 pos, float size, System.Random rng)
        {
            mb.Matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0, R(rng, 0, 360), 0), Vector3.one);
            Color top = new Color(1f, 1f, 1f, 0.55f), bottom = new Color(0.82f, 0.87f, 0.93f, 0.75f);
            System.Func<Vector3, Color> shade = n => Color.Lerp(bottom, top, n.y * 0.5f + 0.5f);
            int puffs = rng.Next(4, 8);
            for (int i = 0; i < puffs; i++)
            {
                float s = size * R(rng, 0.5f, 1f);
                Vector3 c = new Vector3(R(rng, -1.6f, 1.6f) * size, R(rng, -0.1f, 0.3f) * size, R(rng, -0.8f, 0.8f) * size);
                mb.Blob(c, new Vector3(s, s * 0.55f, s * 0.9f), 1, 0.08f, rng, top, shade);
            }
            mb.Matrix = Matrix4x4.identity;
        }

        // spectator grandstand with crowd and roof. Local +Z runs along the track, -X faces it.
        public static void Grandstand(MeshBuilder mb, Matrix4x4 m, float length, Color accent, System.Random rng)
        {
            mb.Matrix = m;
            Color concrete = new Color(0.82f, 0.82f, 0.8f), seat = new Color(0.32f, 0.36f, 0.45f);
            const int tiers = 5;
            const float tierH = 1.1f, tierD = 1.8f;
            for (int t = 0; t < tiers; t++)
            {
                float x = t * tierD;
                float h = (t + 1) * tierH;
                mb.Box(new Vector3(x + tierD * 0.5f, h * 0.5f - 0.5f, 0), new Vector3(tierD, h + 1f, length), t % 2 == 0 ? concrete : Shade(concrete, 0.95f));
                mb.Box(new Vector3(x + tierD * 0.65f, h + 0.25f, 0), new Vector3(0.6f, 0.5f, length - 0.6f), seat);
                // crowd
                Color[] shirts = { new Color(1f, 0.3f, 0.3f), new Color(0.2f, 0.55f, 1f), new Color(1f, 0.85f, 0.2f), Color.white, new Color(0.3f, 0.85f, 0.4f), accent, new Color(1f, 0.55f, 0.15f) };
                for (float z = -length * 0.5f + 0.8f; z < length * 0.5f - 0.8f; z += 0.9f)
                {
                    if (rng.NextDouble() < 0.22) continue;
                    Color shirt = shirts[rng.Next(shirts.Length)];
                    Vector3 p = new Vector3(x + tierD * 0.55f, h + 0.75f, z + R(rng, -0.15f, 0.15f));
                    mb.Box(p, new Vector3(0.45f, 0.7f, 0.5f), shirt);
                    Color skin = Color.Lerp(new Color(1f, 0.85f, 0.7f), new Color(0.45f, 0.3f, 0.2f), (float)rng.NextDouble());
                    mb.Box(p + new Vector3(0, 0.52f, 0), Vector3.one * 0.32f, skin);
                }
            }
            float backX = tiers * tierD;
            float roofY = tiers * tierH + 4.2f;
            mb.Box(new Vector3(backX + 0.2f, roofY * 0.5f - 0.5f, 0), new Vector3(0.4f, roofY + 1f, length), concrete);
            for (float z = -length * 0.5f + 1f; z <= length * 0.5f - 1f; z += length / 4f)
                mb.Box(new Vector3(0.4f, roofY * 0.5f, z), new Vector3(0.3f, roofY, 0.3f), new Color(0.9f, 0.9f, 0.92f));
            mb.TaperBox(new Vector3(backX * 0.5f - 0.6f, roofY + 0.25f, 0), new Vector3(backX + 3f, 0.5f, length + 2f), 1f, 1f, accent);
            mb.Matrix = Matrix4x4.identity;
        }
    }
}
