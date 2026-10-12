using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// Builds the low-poly sports car: lofted body, glass cabin, spoiler, lights and 4 separate wheels.
    /// Ground is at local y = 0, the car faces +Z.
    /// </summary>
    public static class CarModelBuilder
    {
        public const float WheelRadius = 0.37f;
        public static readonly Vector3[] WheelPositions =
        {
            new Vector3(-0.86f, WheelRadius, 1.33f),  // front left
            new Vector3(0.86f, WheelRadius, 1.33f),   // front right
            new Vector3(-0.88f, WheelRadius, -1.30f), // rear left
            new Vector3(0.88f, WheelRadius, -1.30f),  // rear right
        };

        struct Section
        {
            public float Z, Bottom, Mid, MidW, Top, TopW, BottomW;
            public Section(float z, float bottom, float bottomW, float mid, float midW, float top, float topW)
            {
                Z = z; Bottom = bottom; BottomW = bottomW; Mid = mid; MidW = midW; Top = top; TopW = topW;
            }
        }

        public class CarVisual
        {
            public GameObject Root;
            public Transform[] Wheels = new Transform[4];
            public Renderer[] Renderers;
        }

        public static CarVisual Build(Transform parent, Color paint, Material paintMat, Material matteMat, bool shadows = true)
        {
            var visual = new CarVisual();
            var root = new GameObject("CarModel");
            root.transform.SetParent(parent, false);
            visual.Root = root;

            var body = new MeshBuilder(2); // 0 paint (glossy), 1 matte details
            BuildBody(body, paint);
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = body.ToMesh("CarBody");
            var bodyR = bodyGo.AddComponent<MeshRenderer>();
            bodyR.sharedMaterials = new[] { paintMat, matteMat };

            var wheelMesh = BuildWheelMesh();
            var renderers = new Renderer[5];
            renderers[0] = bodyR;
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("Wheel" + i);
                w.transform.SetParent(root.transform, false);
                w.transform.localPosition = WheelPositions[i];
                var mesh = new GameObject("Mesh");
                mesh.transform.SetParent(w.transform, false);
                if (WheelPositions[i].x < 0) mesh.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                mesh.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
                var r = mesh.AddComponent<MeshRenderer>();
                r.sharedMaterial = matteMat;
                renderers[i + 1] = r;
                visual.Wheels[i] = w.transform;
            }
            visual.Renderers = renderers;
            foreach (var r in renderers)
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return visual;
        }

        static void BuildBody(MeshBuilder mb, Color paint)
        {
            Color dark = new Color(0.10f, 0.10f, 0.12f);
            Color glass = new Color(0.16f, 0.22f, 0.30f);
            Color trim = new Color(0.22f, 0.22f, 0.25f);
            Color paintDark = new Color(paint.r * 0.72f, paint.g * 0.72f, paint.b * 0.72f);
            Color stripe = (paint.r + paint.g + paint.b) > 2.2f ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.96f, 0.96f, 0.96f);

            // ---- lower body: lofted sections from rear to front
            var secs = new[]
            {
                new Section(-2.12f, 0.30f, 0.80f, 0.58f, 0.94f, 0.80f, 0.82f),
                new Section(-1.80f, 0.24f, 0.86f, 0.58f, 0.99f, 0.88f, 0.88f),
                new Section(-0.95f, 0.22f, 0.88f, 0.56f, 1.01f, 0.90f, 0.90f),
                new Section(0.55f, 0.22f, 0.88f, 0.55f, 1.00f, 0.84f, 0.90f),
                new Section(1.55f, 0.23f, 0.86f, 0.50f, 0.98f, 0.70f, 0.86f),
                new Section(2.18f, 0.28f, 0.74f, 0.40f, 0.86f, 0.52f, 0.70f),
            };
            for (int i = 0; i < secs.Length - 1; i++)
            {
                var a = secs[i];
                var b = secs[i + 1];
                Vector3[] ra = Ring(a), rb = Ring(b);
                for (int k = 0; k < 6; k++)
                {
                    int k2 = (k + 1) % 6;
                    Color c;
                    int sub = 0;
                    if (k == 0) { c = dark; sub = 1; }                  // underside
                    else if (k == 1 || k == 5) c = paintDark;           // lower sides
                    else c = paint;                                     // upper sides + top
                    Vector3 lc = new Vector3(0, 0.45f, (a.Z + b.Z) * 0.5f);
                    mb.FlatQuad(ra[k], ra[k2], rb[k2], rb[k], c, lc, sub);
                }
            }
            // caps: rear and nose
            CapRing(mb, Ring(secs[0]), new Vector3(0, 0.55f, secs[0].Z + 0.3f), paintDark);
            CapRing(mb, Ring(secs[secs.Length - 1]), new Vector3(0, 0.45f, secs[secs.Length - 1].Z - 0.3f), paintDark);

            // ---- cabin (glass sides, painted roof)
            float[] cz = { -1.30f, -0.95f, 0.10f, 0.80f };
            float[] cy0 = { 0.86f, 0.88f, 0.88f, 0.82f };
            float[] cw0 = { 0.80f, 0.84f, 0.84f, 0.82f };
            float[] cy1 = { 0.94f, 1.30f, 1.34f, 0.88f };
            float[] cw1 = { 0.72f, 0.64f, 0.64f, 0.74f };
            Vector3 cabinCentre = new Vector3(0, 1.0f, -0.3f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 l0 = new Vector3(-cw0[i], cy0[i], cz[i]), r0 = new Vector3(cw0[i], cy0[i], cz[i]);
                Vector3 l1 = new Vector3(-cw1[i], cy1[i], cz[i]), r1 = new Vector3(cw1[i], cy1[i], cz[i]);
                Vector3 nl0 = new Vector3(-cw0[i + 1], cy0[i + 1], cz[i + 1]), nr0 = new Vector3(cw0[i + 1], cy0[i + 1], cz[i + 1]);
                Vector3 nl1 = new Vector3(-cw1[i + 1], cy1[i + 1], cz[i + 1]), nr1 = new Vector3(cw1[i + 1], cy1[i + 1], cz[i + 1]);
                // sides = windows
                mb.FlatQuad(l0, nl0, nl1, l1, glass, cabinCentre, 0);
                mb.FlatQuad(r0, nr0, nr1, r1, glass, cabinCentre, 0);
                // top: rear window (i=0), roof (i=1), windscreen (i=2)
                mb.FlatQuad(l1, r1, nr1, nl1, i == 1 ? paint : glass, cabinCentre, 0);
            }
            // B-pillar accent and roof stripe
            mb.Box(new Vector3(0, 1.345f, -0.42f), new Vector3(0.32f, 0.02f, 1.0f), stripe, 0);
            mb.Box(new Vector3(0, 0.905f, 1.35f), new Vector3(0.32f, 0.02f, 1.1f), stripe, 0);
            mb.Box(new Vector3(0, 0.855f, -1.85f), new Vector3(0.32f, 0.02f, 0.5f), stripe, 0);

            // ---- spoiler
            mb.Box(new Vector3(-0.62f, 0.98f, -1.92f), new Vector3(0.1f, 0.26f, 0.22f), dark, 1);
            mb.Box(new Vector3(0.62f, 0.98f, -1.92f), new Vector3(0.1f, 0.26f, 0.22f), dark, 1);
            mb.Hexa(new[]
            {
                new Vector3(-0.95f, 1.08f, -2.10f), new Vector3(0.95f, 1.08f, -2.10f), new Vector3(0.95f, 1.10f, -1.78f), new Vector3(-0.95f, 1.10f, -1.78f),
                new Vector3(-0.95f, 1.16f, -2.12f), new Vector3(0.95f, 1.16f, -2.12f), new Vector3(0.95f, 1.14f, -1.80f), new Vector3(-0.95f, 1.14f, -1.80f),
            }, paint, null, 0);
            mb.Box(new Vector3(-0.96f, 1.12f, -1.95f), new Vector3(0.04f, 0.2f, 0.42f), dark, 1);
            mb.Box(new Vector3(0.96f, 1.12f, -1.95f), new Vector3(0.04f, 0.2f, 0.42f), dark, 1);

            // ---- lights (alpha 0 = glow)
            Color head = new Color(1f, 0.97f, 0.85f, 0.15f);
            Color tail = new Color(1f, 0.12f, 0.1f, 0.25f);
            mb.Box(new Vector3(-0.55f, 0.47f, 2.17f), new Vector3(0.36f, 0.09f, 0.06f), head, 1);
            mb.Box(new Vector3(0.55f, 0.47f, 2.17f), new Vector3(0.36f, 0.09f, 0.06f), head, 1);
            mb.Box(new Vector3(-0.58f, 0.70f, -2.12f), new Vector3(0.40f, 0.1f, 0.05f), tail, 1);
            mb.Box(new Vector3(0.58f, 0.70f, -2.12f), new Vector3(0.40f, 0.1f, 0.05f), tail, 1);
            // grille / diffuser / side intakes / mirrors
            mb.Box(new Vector3(0, 0.33f, 2.17f), new Vector3(0.9f, 0.1f, 0.05f), dark, 1);
            mb.Box(new Vector3(0, 0.33f, -2.11f), new Vector3(1.2f, 0.12f, 0.06f), dark, 1);
            mb.Box(new Vector3(-0.62f, 0.33f, -2.13f), new Vector3(0.12f, 0.08f, 0.05f), trim, 1);
            mb.Box(new Vector3(0.62f, 0.33f, -2.13f), new Vector3(0.12f, 0.08f, 0.05f), trim, 1);
            mb.Box(new Vector3(-1.005f, 0.52f, -0.55f), new Vector3(0.03f, 0.14f, 0.55f), dark, 1);
            mb.Box(new Vector3(1.005f, 0.52f, -0.55f), new Vector3(0.03f, 0.14f, 0.55f), dark, 1);
            mb.Box(new Vector3(-0.9f, 0.98f, 0.62f), new Vector3(0.16f, 0.1f, 0.12f), paint, 0);
            mb.Box(new Vector3(0.9f, 0.98f, 0.62f), new Vector3(0.16f, 0.1f, 0.12f), paint, 0);

        }

        static Vector3[] Ring(Section s)
        {
            // 6-point cross section, bottom -> right -> top -> left
            return new[]
            {
                new Vector3(-s.BottomW, s.Bottom, s.Z),
                new Vector3(s.BottomW, s.Bottom, s.Z),
                new Vector3(s.MidW, s.Mid, s.Z),
                new Vector3(s.TopW, s.Top, s.Z),
                new Vector3(-s.TopW, s.Top, s.Z),
                new Vector3(-s.MidW, s.Mid, s.Z),
            };
        }

        static void CapRing(MeshBuilder mb, Vector3[] ring, Vector3 inside, Color c)
        {
            Vector3 centre = Vector3.zero;
            foreach (var p in ring) centre += p;
            centre /= ring.Length;
            for (int i = 0; i < ring.Length; i++)
                mb.FlatTri(centre, ring[i], ring[(i + 1) % ring.Length], c, inside, 0);
        }

        static Mesh BuildWheelMesh()
        {
            var mb = new MeshBuilder(1);
            Color tyre = new Color(0.09f, 0.09f, 0.10f);
            Color rim = new Color(0.78f, 0.80f, 0.84f);
            Color hub = new Color(0.30f, 0.31f, 0.34f);
            // tyre (axis = X), outer face at +X
            mb.CylinderAxis(Vector3.zero, Vector3.right, WheelRadius, 0.30f, 12, tyre, tyre);
            // rim disc + spokes on the outer side
            mb.CylinderAxis(new Vector3(0.12f, 0, 0), Vector3.right, WheelRadius * 0.68f, 0.08f, 10, rim, rim);
            mb.CylinderAxis(new Vector3(0.165f, 0, 0), Vector3.right, WheelRadius * 0.22f, 0.04f, 6, hub, hub);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 d = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                Vector3 c = new Vector3(0.165f, 0, 0) + d * WheelRadius * 0.42f;
                Vector3 side = Vector3.Cross(Vector3.right, d) * 0.045f;
                mb.Hexa(new[]
                {
                    c - d * 0.14f - side + Vector3.left * 0.02f, c - d * 0.14f + side + Vector3.left * 0.02f,
                    c + d * 0.14f + side + Vector3.left * 0.02f, c + d * 0.14f - side + Vector3.left * 0.02f,
                    c - d * 0.14f - side + Vector3.right * 0.02f, c - d * 0.14f + side + Vector3.right * 0.02f,
                    c + d * 0.14f + side + Vector3.right * 0.02f, c + d * 0.14f - side + Vector3.right * 0.02f,
                }, hub);
            }
            return mb.ToMesh("Wheel");
        }
    }
}
