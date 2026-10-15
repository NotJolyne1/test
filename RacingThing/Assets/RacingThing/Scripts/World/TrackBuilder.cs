using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingThing
{
    // Sweeps the road profile along the path and adds the bits that sit on it:
    // pillars, gates, line markings and boost pads.
    public static class TrackBuilder
    {
        struct Edge
        {
            public Vector2 A, B, N;
            public int Sub, Uv;
            public Edge(float ax, float ay, float bx, float by, float nx, float ny, int sub, int uv)
            {
                A = new Vector2(ax, ay); B = new Vector2(bx, by); N = new Vector2(nx, ny);
                Sub = sub; Uv = uv;
            }
        }

        const int ChunkSize = 150;

        public static void Build(TrackPath path, Theme theme, Func<float, float, float> ground, Transform parent, List<UnityEngine.Object> owned)
        {
            Road(path, parent, owned);
            Markings(path, parent, owned);
            Pillars(path, ground, parent, owned);
            Gates(path, theme, parent, owned);
        }

        static void Road(TrackPath path, Transform parent, List<UnityEngine.Object> owned)
        {
            float w = TrackPath.HalfWidth, t = TrackPath.WallThickness, h = TrackPath.WallHeight, d = TrackPath.SlabDepth;
            // uv: 0 road, 1 wall face, 2 wall top, 3 concrete
            var profile = new[]
            {
                new Edge(-w, 0, w, 0, 0, 1, 0, 0),
                new Edge(w, 0, w, h, -1, 0, 1, 1),
                new Edge(w, h, w + t, h, 0, 1, 1, 2),
                new Edge(w + t, h, w + t, -d, 1, 0, 2, 3),
                new Edge(-w, h, -w, 0, 1, 0, 1, 1),
                new Edge(-w - t, h, -w, h, 0, 1, 1, 2),
                new Edge(-w - t, -d, -w - t, h, -1, 0, 2, 3),
                new Edge(w + t, -d, -w - t, -d, 0, -1, 2, 3),
            };

            int n = path.Count, chunk = 0;
            for (int c0 = 0; c0 < n - 1; c0 += ChunkSize)
            {
                int c1 = Mathf.Min(n - 1, c0 + ChunkSize);
                var mb = new MeshBuilder(3);
                bool any = false;

                foreach (var e in profile)
                {
                    int row = mb.VertexCount;
                    for (int i = c0; i <= c1; i++)
                    {
                        Vector3 p = path.Pos[i], r = path.Right[i], u = path.Up[i];
                        Vector3 nrm = (r * e.N.x + u * e.N.y).normalized;
                        float v = path.Dist[i] / TrackPath.Tile;
                        Vector2 ua, ub;
                        switch (e.Uv)
                        {
                            case 0: ua = new Vector2(0, v); ub = new Vector2(1, v); break;
                            case 1:
                                bool left = e.A.x < 0;
                                ua = new Vector2(left ? 1 : 0, v);
                                ub = new Vector2(left ? 0 : 1, v);
                                break;
                            case 2: ua = ub = new Vector2(0.5f, v); break;
                            default:
                                ua = new Vector2((e.A.x + e.A.y) * 0.25f, path.Dist[i] * 0.25f);
                                ub = new Vector2((e.B.x + e.B.y) * 0.25f, path.Dist[i] * 0.25f);
                                break;
                        }
                        mb.AddVertex(p + r * e.A.x + u * e.A.y, nrm, ua, Color.white);
                        mb.AddVertex(p + r * e.B.x + u * e.B.y, nrm, ub, Color.white);
                    }
                    for (int i = c0; i < c1; i++)
                    {
                        if (!path.HasRoad(i)) continue;
                        int k = row + (i - c0) * 2;
                        mb.AddQuadIdx(k, k + 2, k + 3, k + 1, mb.Normals[k], e.Sub);
                        any = true;
                    }
                }

                // close off the ends of the road (track ends, both sides of a gap)
                for (int i = c0; i <= c1; i++)
                {
                    bool before = path.HasRoad(i - 1), after = path.HasRoad(i);
                    if (before == after) continue;
                    if (i == c1 && after) continue;
                    if (i == c0 && before && c0 != 0) continue;
                    Cap(mb, path, i, after ? -path.Fwd[i] : path.Fwd[i]);
                }

                if (!any) continue;
                var mesh = mb.ToMesh("Road" + chunk);
                owned.Add(mesh);
                var go = new GameObject("Road" + chunk);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Materials.Road, Materials.Barrier, Materials.Concrete };
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                chunk++;
            }
        }

        static void Cap(MeshBuilder mb, TrackPath path, int i, Vector3 facing)
        {
            float w = TrackPath.HalfWidth, t = TrackPath.WallThickness, h = TrackPath.WallHeight, d = TrackPath.SlabDepth;
            Vector3 p = path.Pos[i], r = path.Right[i], u = path.Up[i];
            Action<float, float, float, float> rect = (x0, y0, x1, y1) =>
            {
                int a = mb.AddVertex(p + r * x0 + u * y0, facing, new Vector2(x0, y0) * 0.25f, Color.white);
                int b = mb.AddVertex(p + r * x1 + u * y0, facing, new Vector2(x1, y0) * 0.25f, Color.white);
                int c = mb.AddVertex(p + r * x1 + u * y1, facing, new Vector2(x1, y1) * 0.25f, Color.white);
                int e = mb.AddVertex(p + r * x0 + u * y1, facing, new Vector2(x0, y1) * 0.25f, Color.white);
                mb.AddQuadIdx(a, b, c, e, facing, 2);
            };
            rect(-w - t, -d, w + t, 0f);
            rect(-w - t, 0f, -w, h);
            rect(w, 0f, w + t, h);
        }

        static void Markings(TrackPath path, Transform parent, List<UnityEngine.Object> owned)
        {
            const float lift = 0.03f;
            float w = TrackPath.HalfWidth;

            var lines = new MeshBuilder(1);
            Stripe(lines, path, path.StartLineIndex, 1.6f, w, lift, 9f / 8f);
            Stripe(lines, path, path.FinishIndex, 3.2f, w, lift, 9f / 8f);
            Add("StartFinish", lines, Materials.Checker, parent, owned);

            var cps = new MeshBuilder(1);
            foreach (int cp in path.Checkpoints) Stripe(cps, path, cp, 2f, w, lift, 1f);
            Add("CheckpointLines", cps, Materials.CheckpointStripe, parent, owned);

            var pads = new MeshBuilder(1);
            float pad = w - 0.6f;
            var glow = new Color(1f, 1f, 1f, 0.45f);
            foreach (var b in path.Boosts)
            {
                int row = pads.VertexCount;
                for (int i = b.Start; i <= b.End; i++)
                {
                    float v = (path.Dist[i] - path.Dist[b.Start]) / 5f;
                    pads.AddVertex(path.SurfacePoint(i, -pad, lift), path.Up[i], new Vector2(0, v), glow);
                    pads.AddVertex(path.SurfacePoint(i, pad, lift), path.Up[i], new Vector2(1, v), glow);
                }
                for (int i = b.Start; i < b.End; i++)
                {
                    int k = row + (i - b.Start) * 2;
                    pads.AddQuadIdx(k, k + 2, k + 3, k + 1, path.Up[i]);
                }
            }
            Add("BoostPads", pads, Materials.BoostPad, parent, owned);
        }

        static void Stripe(MeshBuilder mb, TrackPath path, int i, float length, float w, float lift, float uRepeat)
        {
            Vector3 f = path.Fwd[i] * (length * 0.5f), r = path.Right[i] * w, u = path.Up[i];
            Vector3 c = path.Pos[i] + u * lift;
            int a = mb.AddVertex(c - r - f, u, new Vector2(0, 0), Color.white);
            int b = mb.AddVertex(c + r - f, u, new Vector2(uRepeat, 0), Color.white);
            int d = mb.AddVertex(c + r + f, u, new Vector2(uRepeat, 1), Color.white);
            int e = mb.AddVertex(c - r + f, u, new Vector2(0, 1), Color.white);
            mb.AddQuadIdx(a, b, d, e, u);
        }

        static void Add(string name, MeshBuilder mb, Material mat, Transform parent, List<UnityEngine.Object> owned)
        {
            if (mb.VertexCount == 0) return;
            var mesh = mb.ToMesh(name);
            owned.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Pillars(TrackPath path, Func<float, float, float> ground, Transform parent, List<UnityEngine.Object> owned)
        {
            var mb = new MeshBuilder(1);
            var body = new Color(0.86f, 0.86f, 0.84f);
            var cap = new Color(0.74f, 0.75f, 0.74f);
            var foot = new Color(0.6f, 0.6f, 0.58f);
            float last = -100f;
            for (int i = 2; i < path.Count - 2; i++)
            {
                if (!path.HasRoad(i) || !path.HasRoad(i - 1) || path.InLoop(i)) continue;
                if (path.Dist[i] - last < 20f) continue;
                Vector3 bottom = path.Pos[i] - path.Up[i] * TrackPath.SlabDepth;
                float g = ground(bottom.x, bottom.z);
                float h = bottom.y - g;
                if (h < 1.2f || UnderneathIsRoad(path, i, bottom)) continue;
                last = path.Dist[i];

                Vector3 f = path.Fwd[i];
                f.y = 0f;
                mb.Matrix = Matrix4x4.TRS(new Vector3(bottom.x, g, bottom.z), Quaternion.LookRotation(f.normalized), Vector3.one);
                mb.Cylinder(new Vector3(0, -0.6f, 0), 1.4f, 1.25f, 1.0f, 8, foot, false, true, 0, Mathf.PI / 8f);
                mb.Cylinder(new Vector3(0, 0.3f, 0), 0.9f, 0.8f, h - 0.75f, 8, body, false, false, 0, Mathf.PI / 8f);
                mb.Matrix = Matrix4x4.TRS(bottom, path.RotationAt(i), Vector3.one);
                mb.TaperBox(new Vector3(0, -0.35f, 0), new Vector3(TrackPath.HalfWidth * 1.2f, 0.7f, 2f), 1.3f, 1.1f, cap);
            }
            mb.Matrix = Matrix4x4.identity;
            if (mb.VertexCount == 0) return;
            var mesh = mb.ToMesh("Pillars");
            owned.Add(mesh);
            var go = new GameObject("Pillars");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.VertexLit;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // don't drop a pillar onto a lower bit of road
        static bool UnderneathIsRoad(TrackPath path, int i, Vector3 at)
        {
            float clear = TrackPath.HalfWidth + TrackPath.WallThickness + 2f;
            for (int j = 0; j < path.Count; j += 2)
            {
                if (Mathf.Abs(j - i) < 30) continue;
                Vector3 q = path.Pos[j];
                if (q.y > at.y) continue;
                float dx = q.x - at.x, dz = q.z - at.z;
                if (dx * dx + dz * dz < clear * clear) return true;
            }
            return false;
        }

        enum Gate { Start, Checkpoint, Finish }

        static void Gates(TrackPath path, Theme theme, Transform parent, List<UnityEngine.Object> owned)
        {
            var mb = new MeshBuilder(2);
            AddGate(mb, path, path.StartLineIndex, Gate.Start, theme);
            foreach (int cp in path.Checkpoints) AddGate(mb, path, cp, Gate.Checkpoint, theme);
            AddGate(mb, path, path.FinishIndex, Gate.Finish, theme);
            EndWall(mb, path);
            mb.Matrix = Matrix4x4.identity;
            var mesh = mb.ToMesh("Gates");
            owned.Add(mesh);
            var go = new GameObject("Gates");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Materials.VertexLit, Materials.Checker };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void EndWall(MeshBuilder mb, TrackPath path)
        {
            int i = path.Count - 1;
            mb.Matrix = Matrix4x4.TRS(path.Pos[i], path.RotationAt(i), Vector3.one);
            float w = TrackPath.HalfWidth + TrackPath.WallThickness;
            const int blocks = 6;
            float bw = w * 2f / blocks;
            for (int k = 0; k < blocks; k++)
            {
                var c = k % 2 == 0 ? new Color(0.88f, 0.16f, 0.14f) : new Color(0.96f, 0.96f, 0.96f);
                mb.Box(new Vector3(-w + bw * (k + 0.5f), 0.7f, -0.9f), new Vector3(bw, 1.4f, 1.6f), c);
            }
        }

        static void AddGate(MeshBuilder mb, TrackPath path, int i, Gate kind, Theme theme)
        {
            float x = TrackPath.HalfWidth + TrackPath.WallThickness * 0.5f;
            float top = 7.2f, wall = TrackPath.WallHeight;
            mb.Matrix = Matrix4x4.TRS(path.Pos[i], path.RotationAt(i), Vector3.one);

            Color post, beam, light;
            if (kind == Gate.Start) { post = new Color(0.94f, 0.94f, 0.95f); beam = theme.AccentColor; light = new Color(0.4f, 1f, 0.5f, 0f); }
            else if (kind == Gate.Checkpoint) { post = new Color(0.94f, 0.95f, 0.98f); beam = new Color(0.14f, 0.55f, 0.98f); light = new Color(0.8f, 0.95f, 1f, 0f); }
            else { post = new Color(0.17f, 0.18f, 0.21f); beam = new Color(0.17f, 0.18f, 0.21f); light = new Color(1f, 0.85f, 0.35f, 0f); }
            var dark = new Color(0.22f, 0.23f, 0.26f);

            for (int s = -1; s <= 1; s += 2)
            {
                float px = s * x, ph = top - wall;
                mb.Box(new Vector3(px, wall + 0.2f, 0), new Vector3(0.8f, 0.4f, 1.6f), dark);
                mb.TaperBox(new Vector3(px, wall + ph * 0.5f, 0), new Vector3(0.6f, ph, 0.9f), 0.85f, 0.85f, post);
                mb.Box(new Vector3(px - s * 0.31f, wall + ph * 0.45f, 0), new Vector3(0.05f, ph * 0.7f, 0.26f), light);
            }

            float by = top + 0.2f, bw = x * 2f + 0.9f;
            mb.Box(new Vector3(0, by, 0), new Vector3(bw, 1.7f, 1f), beam);
            mb.Box(new Vector3(0, by + 0.95f, 0), new Vector3(bw + 0.3f, 0.2f, 1.2f), post);
            mb.Box(new Vector3(0, by - 0.95f, 0), new Vector3(bw + 0.3f, 0.2f, 1.2f), post);

            if (kind == Gate.Checkpoint)
            {
                for (int k = -2; k <= 2; k++)
                    for (int s = -1; s <= 1; s += 2)
                        mb.Box(new Vector3(k * 1.7f, by, s * 0.53f), new Vector3(0.8f, 0.8f, 0.07f), light);
                return;
            }

            float hw = x - 0.4f, y0 = by - 0.7f, y1 = by + 0.7f;
            float rep = kind == Gate.Finish ? 2f : 1f;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * 0.51f;
                mb.QuadUV(new Vector3(-hw, y0, z), new Vector3(hw, y0, z), new Vector3(hw, y1, z), new Vector3(-hw, y1, z),
                          new Vector2(0, 0), new Vector2(rep, 0), new Vector2(rep, 1), new Vector2(0, 1), Color.white, new Vector3(0, 0, s), 1);
            }
            if (kind == Gate.Start)
                for (int k = -2; k <= 2; k++)
                    mb.Box(new Vector3(k * 1.5f, by - 1.3f, 0), new Vector3(0.6f, 0.45f, 0.3f), light);
        }
    }
}
