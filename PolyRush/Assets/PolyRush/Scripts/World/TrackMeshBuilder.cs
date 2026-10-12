using System;
using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// Sweeps the road cross-section along the sampled path: asphalt deck, red/white barriers,
    /// concrete slab sides and underside, end caps at jumps, support pillars, gates and decals.
    /// </summary>
    public static class TrackMeshBuilder
    {
        struct Edge
        {
            public Vector2 A, B, N;
            public int Sub;
            public int UvMode; // 0 road, 1 barrier face, 2 barrier top, 3 concrete
            public Edge(float ax, float ay, float bx, float by, float nx, float ny, int sub, int uv)
            {
                A = new Vector2(ax, ay);
                B = new Vector2(bx, by);
                N = new Vector2(nx, ny);
                Sub = sub;
                UvMode = uv;
            }
        }

        const int ChunkSamples = 160;

        public static void Build(TrackPath path, Theme theme, Func<float, float, float> groundHeight, Transform parent, List<UnityEngine.Object> created)
        {
            BuildRoad(path, parent, created);
            BuildDecals(path, parent, created);
            BuildPillars(path, groundHeight, parent, created);
            BuildGates(path, theme, parent, created);
        }

        // ------------------------------------------------------------------ road

        static void BuildRoad(TrackPath path, Transform parent, List<UnityEngine.Object> created)
        {
            float hw = TrackPath.HalfWidth, T = TrackPath.WallThickness, H = TrackPath.WallHeight, D = TrackPath.SlabDepth;
            var edges = new[]
            {
                new Edge(-hw, 0, hw, 0, 0, 1, 0, 0),             // deck
                new Edge(hw, 0, hw, H, -1, 0, 1, 1),             // right barrier inner
                new Edge(hw, H, hw + T, H, 0, 1, 1, 2),          // right barrier top
                new Edge(hw + T, H, hw + T, -D, 1, 0, 2, 3),     // right outer side
                new Edge(-hw, H, -hw, 0, 1, 0, 1, 1),            // left barrier inner
                new Edge(-hw - T, H, -hw, H, 0, 1, 1, 2),        // left barrier top
                new Edge(-hw - T, -D, -hw - T, H, -1, 0, 2, 3),  // left outer side
                new Edge(hw + T, -D, -hw - T, -D, 0, -1, 2, 3),  // underside
            };

            int n = path.Count;
            int chunkIndex = 0;
            for (int c0 = 0; c0 < n - 1; c0 += ChunkSamples)
            {
                int c1 = Mathf.Min(n - 1, c0 + ChunkSamples);
                var mb = new MeshBuilder(3);
                bool any = false;

                foreach (var e in edges)
                {
                    int rowStart = mb.VertexCount;
                    for (int i = c0; i <= c1; i++)
                    {
                        Vector3 p = path.Pos[i], r = path.Right[i], u = path.Up[i];
                        Vector3 nrm = (r * e.N.x + u * e.N.y).normalized;
                        float dist = path.Dist[i];
                        Vector2 ua, ub;
                        switch (e.UvMode)
                        {
                            case 0:
                                ua = new Vector2(0f, dist / 12f);
                                ub = new Vector2(1f, dist / 12f);
                                break;
                            case 1:
                                bool left = e.A.x < 0;
                                ua = new Vector2(left ? 1f : 0f, dist / 8f);
                                ub = new Vector2(left ? 0f : 1f, dist / 8f);
                                break;
                            case 2:
                                ua = new Vector2(0.5f, dist / 8f);
                                ub = new Vector2(0.5f, dist / 8f);
                                break;
                            default:
                                ua = new Vector2(e.A.x * 0.25f + e.A.y * 0.25f, dist / 4f);
                                ub = new Vector2(e.B.x * 0.25f + e.B.y * 0.25f, dist / 4f);
                                break;
                        }
                        mb.AddVertex(p + r * e.A.x + u * e.A.y, nrm, ua, Color.white);
                        mb.AddVertex(p + r * e.B.x + u * e.B.y, nrm, ub, Color.white);
                    }
                    for (int i = c0; i < c1; i++)
                    {
                        if (!path.HasRoad(i)) continue;
                        int k = rowStart + (i - c0) * 2;
                        Vector3 expected = mb.Normals[k];
                        mb.AddQuadIdx(k, k + 2, k + 3, k + 1, expected, e.Sub);
                        any = true;
                    }
                }

                // End caps where the road starts or stops (track ends, jump take-off and landing).
                for (int i = c0; i <= c1; i++)
                {
                    bool before = path.HasRoad(i - 1);
                    bool after = path.HasRoad(i);
                    if (before == after) continue;
                    if (i == c1 && after) continue;       // handled by the next chunk
                    if (i == c0 && before && c0 != 0) continue;
                    AddCap(mb, path, i, after ? -path.Fwd[i] : path.Fwd[i]);
                }

                if (!any) continue;
                var mesh = mb.ToMesh("Road_" + chunkIndex);
                created.Add(mesh);
                var go = new GameObject("Road_" + chunkIndex);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { MaterialLibrary.Road, MaterialLibrary.Barrier, MaterialLibrary.Concrete };
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                chunkIndex++;
            }
        }

        static void AddCap(MeshBuilder mb, TrackPath path, int i, Vector3 facing)
        {
            float hw = TrackPath.HalfWidth, T = TrackPath.WallThickness, H = TrackPath.WallHeight, D = TrackPath.SlabDepth;
            Vector3 p = path.Pos[i], r = path.Right[i], u = path.Up[i];
            Action<float, float, float, float, int> rect = (x0, y0, x1, y1, sub) =>
            {
                int a = mb.AddVertex(p + r * x0 + u * y0, facing, new Vector2(x0 * 0.25f, y0 * 0.25f), Color.white);
                int b = mb.AddVertex(p + r * x1 + u * y0, facing, new Vector2(x1 * 0.25f, y0 * 0.25f), Color.white);
                int c = mb.AddVertex(p + r * x1 + u * y1, facing, new Vector2(x1 * 0.25f, y1 * 0.25f), Color.white);
                int d = mb.AddVertex(p + r * x0 + u * y1, facing, new Vector2(x0 * 0.25f, y1 * 0.25f), Color.white);
                mb.AddQuadIdx(a, b, c, d, facing, sub);
            };
            rect(-hw - T, -D, hw + T, 0f, 2);
            rect(-hw - T, 0f, -hw, H, 2);
            rect(hw, 0f, hw + T, H, 2);
        }

        // ------------------------------------------------------------------ decals

        static void BuildDecals(TrackPath path, Transform parent, List<UnityEngine.Object> created)
        {
            const float lift = 0.03f;
            float hw = TrackPath.HalfWidth;

            // Start + finish (checker), checkpoints (stripes)
            var check = new MeshBuilder(1);
            LineDecal(check, path, path.StartLineIndex, 1.6f, hw, lift, 1.5f);
            LineDecal(check, path, path.FinishIndex, 3.2f, hw, lift, 1.5f);
            AddDecalObject("StartFinishLines", check, MaterialLibrary.Checker, parent, created);

            var cps = new MeshBuilder(1);
            foreach (int cp in path.Checkpoints) LineDecal(cps, path, cp, 2.0f, hw, lift, 1f);
            AddDecalObject("CheckpointLines", cps, MaterialLibrary.CheckpointStripe, parent, created);

            // Boost pads follow the road (also through curves)
            var boost = new MeshBuilder(1);
            const float padHalf = 4.2f;
            Color glow = new Color(1f, 1f, 1f, 0.45f);
            foreach (var b in path.Boosts)
            {
                int rowStart = boost.VertexCount;
                for (int i = b.Start; i <= b.End; i++)
                {
                    float v = (path.Dist[i] - path.Dist[b.Start]) / 6f;
                    boost.AddVertex(path.SurfacePoint(i, -padHalf, lift), path.Up[i], new Vector2(0f, v), glow);
                    boost.AddVertex(path.SurfacePoint(i, padHalf, lift), path.Up[i], new Vector2(1f, v), glow);
                }
                for (int i = b.Start; i < b.End; i++)
                {
                    int k = rowStart + (i - b.Start) * 2;
                    boost.AddQuadIdx(k, k + 2, k + 3, k + 1, path.Up[i], 0);
                }
            }
            AddDecalObject("BoostPads", boost, MaterialLibrary.BoostPad, parent, created);
        }

        static void LineDecal(MeshBuilder mb, TrackPath path, int i, float length, float hw, float lift, float uRepeat)
        {
            Vector3 f = path.Fwd[i], u = path.Up[i];
            Vector3 c = path.Pos[i] + u * lift;
            Vector3 r = path.Right[i];
            Vector3 a = c - r * hw - f * (length * 0.5f);
            Vector3 b = c + r * hw - f * (length * 0.5f);
            Vector3 d = c - r * hw + f * (length * 0.5f);
            Vector3 e = c + r * hw + f * (length * 0.5f);
            int i0 = mb.AddVertex(a, u, new Vector2(0, 0), Color.white);
            int i1 = mb.AddVertex(b, u, new Vector2(uRepeat, 0), Color.white);
            int i2 = mb.AddVertex(e, u, new Vector2(uRepeat, 1), Color.white);
            int i3 = mb.AddVertex(d, u, new Vector2(0, 1), Color.white);
            mb.AddQuadIdx(i0, i1, i2, i3, u, 0);
        }

        static void AddDecalObject(string name, MeshBuilder mb, Material mat, Transform parent, List<UnityEngine.Object> created)
        {
            if (mb.VertexCount == 0) return;
            var mesh = mb.ToMesh(name);
            created.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ pillars

        static void BuildPillars(TrackPath path, Func<float, float, float> groundHeight, Transform parent, List<UnityEngine.Object> created)
        {
            var mb = new MeshBuilder(1);
            Color col = new Color(0.82f, 0.82f, 0.80f);
            Color capCol = new Color(0.70f, 0.71f, 0.70f);
            Color footCol = new Color(0.55f, 0.55f, 0.53f);
            float lastDist = -100f;
            for (int i = 2; i < path.Count - 2; i++)
            {
                if (!path.HasRoad(i) || !path.HasRoad(i - 1)) continue;
                if (path.Dist[i] - lastDist < 22f) continue;
                Vector3 bottom = path.Pos[i] - path.Up[i] * TrackPath.SlabDepth;
                float g = groundHeight(bottom.x, bottom.z);
                float h = bottom.y - g;
                if (h < 1.2f) continue;
                if (OverOtherRoad(path, i, bottom)) continue;
                lastDist = path.Dist[i];

                Vector3 f = path.Fwd[i];
                f.y = 0;
                f.Normalize();
                mb.Matrix = Matrix4x4.TRS(new Vector3(bottom.x, g, bottom.z), Quaternion.LookRotation(f, Vector3.up), Vector3.one);
                mb.Cylinder(new Vector3(0, -0.6f, 0), 1.7f, 1.55f, 1.0f, 8, footCol, false, true, 0, Mathf.PI / 8f);
                mb.Cylinder(new Vector3(0, 0.3f, 0), 1.15f, 1.0f, h - 0.75f, 8, col, false, false, 0, Mathf.PI / 8f);
                // pier cap under the deck, rolled with the road so it also fits banked sections
                mb.Matrix = Matrix4x4.TRS(bottom, path.RotationAt(i), Vector3.one);
                mb.TaperBox(new Vector3(0, -0.35f, 0), new Vector3(TrackPath.HalfWidth * 1.3f, 0.7f, 2.2f), 1.25f, 1.1f, capCol);
            }
            mb.Matrix = Matrix4x4.identity;
            if (mb.VertexCount == 0) return;
            var mesh = mb.ToMesh("Pillars");
            created.Add(mesh);
            var go = new GameObject("Pillars");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.VertexLit;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>True if a lower part of the track passes under this point (no pillar in the middle of a road).</summary>
        static bool OverOtherRoad(TrackPath path, int i, Vector3 p)
        {
            float clear = TrackPath.HalfWidth + TrackPath.WallThickness + 2.5f;
            for (int j = 0; j < path.Count; j += 2)
            {
                if (Mathf.Abs(j - i) < 40) continue;
                Vector3 q = path.Pos[j];
                if (q.y > p.y) continue;
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < clear * clear) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ gates

        enum GateKind { Start, Checkpoint, Finish }

        static void BuildGates(TrackPath path, Theme theme, Transform parent, List<UnityEngine.Object> created)
        {
            var mb = new MeshBuilder(2);
            AddGate(mb, path, path.StartLineIndex, GateKind.Start, theme);
            foreach (int cp in path.Checkpoints) AddGate(mb, path, cp, GateKind.Checkpoint, theme);
            AddGate(mb, path, path.FinishIndex, GateKind.Finish, theme);
            AddEndWall(mb, path);
            mb.Matrix = Matrix4x4.identity;
            var mesh = mb.ToMesh("Gates");
            created.Add(mesh);
            var go = new GameObject("Gates");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { MaterialLibrary.VertexLit, MaterialLibrary.Checker };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Red / white tyre-wall block across the road at the very end of the run-off.</summary>
        static void AddEndWall(MeshBuilder mb, TrackPath path)
        {
            int i = path.Count - 1;
            mb.Matrix = Matrix4x4.TRS(path.Pos[i], path.RotationAt(i), Vector3.one);
            float hw = TrackPath.HalfWidth + TrackPath.WallThickness;
            const int blocks = 8;
            float w = hw * 2f / blocks;
            for (int k = 0; k < blocks; k++)
            {
                Color c = k % 2 == 0 ? new Color(0.88f, 0.16f, 0.14f) : new Color(0.96f, 0.96f, 0.96f);
                mb.Box(new Vector3(-hw + w * (k + 0.5f), 0.8f, -1.0f), new Vector3(w, 1.6f, 1.8f), c);
            }
            mb.Box(new Vector3(0f, 1.7f, -1.0f), new Vector3(hw * 2f, 0.2f, 1.9f), new Color(0.2f, 0.2f, 0.23f));
        }

        static void AddGate(MeshBuilder mb, TrackPath path, int i, GateKind kind, Theme theme)
        {
            float hw = TrackPath.HalfWidth;
            // Posts stand on top of the barriers so gates also work on elevated sections.
            float px = hw + TrackPath.WallThickness * 0.5f;
            float wallTop = TrackPath.WallHeight;
            const float height = 8.2f;
            mb.Matrix = Matrix4x4.TRS(path.Pos[i], path.RotationAt(i), Vector3.one);

            Color frame, beam, lights;
            switch (kind)
            {
                case GateKind.Start:
                    frame = new Color(0.93f, 0.93f, 0.95f);
                    beam = theme.AccentColor;
                    lights = new Color(0.35f, 1f, 0.45f, 0f);
                    break;
                case GateKind.Checkpoint:
                    frame = new Color(0.93f, 0.95f, 0.98f);
                    beam = new Color(0.12f, 0.58f, 0.98f);
                    lights = new Color(0.75f, 0.95f, 1f, 0f);
                    break;
                default:
                    frame = new Color(0.16f, 0.17f, 0.2f);
                    beam = new Color(0.95f, 0.95f, 0.95f);
                    lights = new Color(1f, 0.85f, 0.3f, 0f);
                    break;
            }
            Color dark = new Color(0.22f, 0.23f, 0.26f);

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * px;
                float postH = height - wallTop;
                mb.Box(new Vector3(x, wallTop + 0.2f, 0f), new Vector3(0.95f, 0.4f, 1.8f), dark);
                mb.TaperBox(new Vector3(x, wallTop + postH * 0.5f, 0f), new Vector3(0.7f, postH, 1.0f), 0.85f, 0.85f, frame);
                // light strip on the inner face of each post
                mb.Box(new Vector3(x - s * 0.36f, wallTop + postH * 0.45f, 0f), new Vector3(0.06f, postH * 0.7f, 0.3f), lights);
            }
            float beamY = height + 0.2f;
            float beamW = px * 2f + 1.0f;
            mb.Box(new Vector3(0f, beamY, 0f), new Vector3(beamW, 1.9f, 1.1f), kind == GateKind.Finish ? dark : beam);
            mb.Box(new Vector3(0f, beamY + 1.05f, 0f), new Vector3(beamW + 0.3f, 0.2f, 1.3f), frame);
            mb.Box(new Vector3(0f, beamY - 1.05f, 0f), new Vector3(beamW + 0.3f, 0.2f, 1.3f), frame);

            if (kind == GateKind.Finish || kind == GateKind.Start)
            {
                // checkered banners on both faces
                float bw = px - 0.4f, y0 = beamY - 0.75f, y1 = beamY + 0.75f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * 0.56f;
                    float rep = kind == GateKind.Finish ? 2f : 1f;
                    mb.QuadUV(new Vector3(-bw, y0, z), new Vector3(bw, y0, z), new Vector3(bw, y1, z), new Vector3(-bw, y1, z),
                              new Vector2(0, 0), new Vector2(rep, 0), new Vector2(rep, 1), new Vector2(0, 1),
                              Color.white, new Vector3(0, 0, side), 1);
                }
                if (kind == GateKind.Start)
                {
                    // start lights
                    for (int k = -2; k <= 2; k++)
                        for (int side = -1; side <= 1; side += 2)
                            mb.Box(new Vector3(k * 1.6f, beamY - 1.38f, side * 0.3f), new Vector3(0.7f, 0.5f, 0.3f), lights);
                }
            }
            else
            {
                // checkpoint: row of glowing chevrons on the beam
                for (int k = -3; k <= 3; k++)
                    for (int side = -1; side <= 1; side += 2)
                        mb.Box(new Vector3(k * 1.8f, beamY, side * 0.58f), new Vector3(0.9f, 0.9f, 0.08f), lights);
            }
        }
    }
}
