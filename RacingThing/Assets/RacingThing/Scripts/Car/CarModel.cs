using UnityEngine;

namespace RacingThing
{
    // The player car. Ground is at y = 0 and the nose points along +Z.
    public static class CarModel
    {
        public const float WheelRadius = 0.36f;
        public static readonly Vector3[] Wheels =
        {
            new Vector3(-0.86f, WheelRadius, 1.38f),
            new Vector3(0.86f, WheelRadius, 1.38f),
            new Vector3(-0.88f, WheelRadius, -1.32f),
            new Vector3(0.88f, WheelRadius, -1.32f),
        };

        public class Parts
        {
            public GameObject Root;
            public Transform[] Wheels = new Transform[4];
            public Renderer[] Renderers;
        }

        // z, bottom y, bottom w, sill y, sill w, shoulder y, shoulder w, deck y, deck w
        static readonly float[,] Body =
        {
            { -2.20f, 0.32f, 0.70f, 0.42f, 0.86f, 0.70f, 0.90f, 0.86f, 0.74f },
            { -1.95f, 0.26f, 0.78f, 0.36f, 0.95f, 0.72f, 0.99f, 0.92f, 0.84f },
            { -1.10f, 0.22f, 0.80f, 0.32f, 0.97f, 0.66f, 1.01f, 0.90f, 0.86f },
            { 0.00f, 0.22f, 0.80f, 0.32f, 0.95f, 0.62f, 0.98f, 0.84f, 0.86f },
            { 1.00f, 0.22f, 0.80f, 0.32f, 0.96f, 0.60f, 1.00f, 0.78f, 0.86f },
            { 1.80f, 0.24f, 0.76f, 0.30f, 0.90f, 0.50f, 0.94f, 0.62f, 0.80f },
            { 2.30f, 0.28f, 0.62f, 0.32f, 0.74f, 0.40f, 0.78f, 0.46f, 0.62f },
        };

        // z, base y, base w, top y, top w
        static readonly float[,] Canopy =
        {
            { -1.50f, 0.88f, 0.56f, 0.93f, 0.46f },
            { -1.00f, 0.90f, 0.72f, 1.17f, 0.52f },
            { -0.20f, 0.86f, 0.75f, 1.23f, 0.56f },
            { 0.45f, 0.82f, 0.73f, 1.12f, 0.52f },
            { 0.98f, 0.77f, 0.64f, 0.81f, 0.50f },
        };

        public static Parts Build(Transform parent, Color paint, Material gloss, Material matte, bool shadows = true)
        {
            var parts = new Parts();
            parts.Root = new GameObject("Car");
            parts.Root.transform.SetParent(parent, false);

            var mb = new MeshBuilder(2);
            Shell(mb, paint);
            var body = new GameObject("Body");
            body.transform.SetParent(parts.Root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("CarBody");
            var bodyR = body.AddComponent<MeshRenderer>();
            bodyR.sharedMaterials = new[] { gloss, matte };

            var wheelMesh = WheelMesh();
            parts.Renderers = new Renderer[5];
            parts.Renderers[0] = bodyR;
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(parts.Root.transform, false);
                pivot.localPosition = Wheels[i];
                var m = new GameObject("Mesh");
                m.transform.SetParent(pivot, false);
                if (Wheels[i].x < 0) m.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                m.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
                var r = m.AddComponent<MeshRenderer>();
                r.sharedMaterial = matte;
                parts.Renderers[i + 1] = r;
                parts.Wheels[i] = pivot;
            }
            var mode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in parts.Renderers) r.shadowCastingMode = mode;
            return parts;
        }

        static Vector3[] Ring(int s)
        {
            float z = Body[s, 0];
            float yb = Body[s, 1], wb = Body[s, 2], ys = Body[s, 3], ws = Body[s, 4];
            float ym = Body[s, 5], wm = Body[s, 6], yt = Body[s, 7], wt = Body[s, 8];
            return new[]
            {
                new Vector3(-wb, yb, z), new Vector3(wb, yb, z), new Vector3(ws, ys, z), new Vector3(wm, ym, z),
                new Vector3(wt, yt, z), new Vector3(-wt, yt, z), new Vector3(-wm, ym, z), new Vector3(-ws, ys, z),
            };
        }

        static void Shell(MeshBuilder mb, Color paint)
        {
            var black = new Color(0.08f, 0.08f, 0.09f);
            var trim = new Color(0.16f, 0.16f, 0.18f);
            var glass = new Color(0.12f, 0.16f, 0.22f);
            var shade = new Color(paint.r * 0.78f, paint.g * 0.78f, paint.b * 0.78f);
            bool light = paint.r + paint.g + paint.b > 2.2f;
            var stripe = light ? new Color(0.14f, 0.14f, 0.16f) : new Color(0.95f, 0.95f, 0.95f);

            int sections = Body.GetLength(0);
            for (int s = 0; s < sections - 1; s++)
            {
                var a = Ring(s);
                var b = Ring(s + 1);
                var inside = new Vector3(0, 0.5f, (Body[s, 0] + Body[s + 1, 0]) * 0.5f);
                for (int k = 0; k < 8; k++)
                {
                    int k2 = (k + 1) % 8;
                    Color c;
                    int sub = 0;
                    if (k == 0) { c = black; sub = 1; }
                    else if (k == 1 || k == 7) { c = trim; sub = 1; }
                    else if (k == 2 || k == 6) c = shade;
                    else c = paint;
                    mb.FlatQuad(a[k], a[k2], b[k2], b[k], c, inside, sub);
                }
            }
            Fan(mb, Ring(0), new Vector3(0, 0.55f, Body[0, 0] + 0.4f), shade);
            Fan(mb, Ring(sections - 1), new Vector3(0, 0.4f, Body[sections - 1, 0] - 0.4f), shade);

            // canopy
            int cs = Canopy.GetLength(0);
            var cin = new Vector3(0, 0.95f, -0.3f);
            for (int s = 0; s < cs - 1; s++)
            {
                Vector3 l0 = new Vector3(-Canopy[s, 2], Canopy[s, 1], Canopy[s, 0]), r0 = new Vector3(Canopy[s, 2], Canopy[s, 1], Canopy[s, 0]);
                Vector3 l1 = new Vector3(-Canopy[s, 4], Canopy[s, 3], Canopy[s, 0]), r1 = new Vector3(Canopy[s, 4], Canopy[s, 3], Canopy[s, 0]);
                int n = s + 1;
                Vector3 nl0 = new Vector3(-Canopy[n, 2], Canopy[n, 1], Canopy[n, 0]), nr0 = new Vector3(Canopy[n, 2], Canopy[n, 1], Canopy[n, 0]);
                Vector3 nl1 = new Vector3(-Canopy[n, 4], Canopy[n, 3], Canopy[n, 0]), nr1 = new Vector3(Canopy[n, 4], Canopy[n, 3], Canopy[n, 0]);
                mb.FlatQuad(l0, nl0, nl1, l1, glass, cin);
                mb.FlatQuad(r0, nr0, nr1, r1, glass, cin);
                mb.FlatQuad(l1, r1, nr1, nl1, s == 1 ? paint : glass, cin);
            }

            // stripes over the nose, roof and tail
            mb.Box(new Vector3(0, 1.235f, -0.6f), new Vector3(0.3f, 0.02f, 0.75f), stripe);
            mb.Hexa(new[]
            {
                new Vector3(-0.15f, 0.785f, 1.0f), new Vector3(0.15f, 0.785f, 1.0f), new Vector3(0.15f, 0.475f, 2.3f), new Vector3(-0.15f, 0.475f, 2.3f),
                new Vector3(-0.15f, 0.8f, 1.0f), new Vector3(0.15f, 0.8f, 1.0f), new Vector3(0.15f, 0.49f, 2.3f), new Vector3(-0.15f, 0.49f, 2.3f),
            }, stripe);
            mb.Box(new Vector3(0, 0.925f, -1.75f), new Vector3(0.3f, 0.02f, 0.4f), stripe);

            // wing
            mb.Box(new Vector3(-0.55f, 1.0f, -1.95f), new Vector3(0.07f, 0.22f, 0.2f), black, 1);
            mb.Box(new Vector3(0.55f, 1.0f, -1.95f), new Vector3(0.07f, 0.22f, 0.2f), black, 1);
            mb.Hexa(new[]
            {
                new Vector3(-0.95f, 1.1f, -2.18f), new Vector3(0.95f, 1.1f, -2.18f), new Vector3(0.95f, 1.12f, -1.85f), new Vector3(-0.95f, 1.12f, -1.85f),
                new Vector3(-0.95f, 1.17f, -2.2f), new Vector3(0.95f, 1.17f, -2.2f), new Vector3(0.95f, 1.15f, -1.87f), new Vector3(-0.95f, 1.15f, -1.87f),
            }, paint);
            mb.Box(new Vector3(-0.97f, 1.12f, -2.02f), new Vector3(0.03f, 0.18f, 0.42f), black, 1);
            mb.Box(new Vector3(0.97f, 1.12f, -2.02f), new Vector3(0.03f, 0.18f, 0.42f), black, 1);

            // aero bits
            mb.Box(new Vector3(0, 0.22f, 2.2f), new Vector3(1.4f, 0.05f, 0.35f), black, 1);       // splitter
            mb.Box(new Vector3(0, 0.3f, -2.17f), new Vector3(1.2f, 0.16f, 0.12f), black, 1);       // diffuser
            mb.Box(new Vector3(-1.0f, 0.55f, -0.75f), new Vector3(0.04f, 0.16f, 0.6f), black, 1);  // side intakes
            mb.Box(new Vector3(1.0f, 0.55f, -0.75f), new Vector3(0.04f, 0.16f, 0.6f), black, 1);
            mb.Box(new Vector3(-0.85f, 0.92f, 0.62f), new Vector3(0.14f, 0.08f, 0.1f), paint);     // mirrors
            mb.Box(new Vector3(0.85f, 0.92f, 0.62f), new Vector3(0.14f, 0.08f, 0.1f), paint);

            // lights - alpha below 1 makes them glow
            var head = new Color(1f, 0.97f, 0.88f, 0.1f);
            var tail = new Color(1f, 0.1f, 0.08f, 0.2f);
            mb.Box(new Vector3(-0.5f, 0.45f, 2.26f), new Vector3(0.38f, 0.06f, 0.08f), head, 1);
            mb.Box(new Vector3(0.5f, 0.45f, 2.26f), new Vector3(0.38f, 0.06f, 0.08f), head, 1);
            mb.Box(new Vector3(0, 0.72f, -2.2f), new Vector3(1.5f, 0.06f, 0.05f), tail, 1);
        }

        static void Fan(MeshBuilder mb, Vector3[] ring, Vector3 inside, Color c)
        {
            Vector3 mid = Vector3.zero;
            foreach (var p in ring) mid += p;
            mid /= ring.Length;
            for (int i = 0; i < ring.Length; i++)
                mb.FlatTri(mid, ring[i], ring[(i + 1) % ring.Length], c, inside);
        }

        static Mesh WheelMesh()
        {
            var mb = new MeshBuilder(1);
            var tyre = new Color(0.09f, 0.09f, 0.1f);
            var rim = new Color(0.8f, 0.82f, 0.86f);
            var hub = new Color(0.25f, 0.26f, 0.29f);
            mb.CylinderAxis(Vector3.zero, Vector3.right, WheelRadius, 0.32f, 14, tyre, tyre);
            mb.CylinderAxis(new Vector3(0.13f, 0, 0), Vector3.right, WheelRadius * 0.7f, 0.08f, 12, hub, hub);
            mb.CylinderAxis(new Vector3(0.17f, 0, 0), Vector3.right, WheelRadius * 0.2f, 0.04f, 6, rim, rim);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var d = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                var c = new Vector3(0.17f, 0, 0) + d * WheelRadius * 0.42f;
                var side = Vector3.Cross(Vector3.right, d) * 0.035f;
                var l = Vector3.left * 0.02f;
                var r = Vector3.right * 0.02f;
                var len = d * 0.15f;
                mb.Hexa(new[]
                {
                    c - len - side + l, c - len + side + l, c + len + side + l, c + len - side + l,
                    c - len - side + r, c - len + side + r, c + len + side + r, c + len - side + r,
                }, rim);
            }
            return mb.ToMesh("Wheel");
        }
    }
}
