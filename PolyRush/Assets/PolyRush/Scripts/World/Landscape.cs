using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    // Everything around the track: terrain, water, trees, rocks, clouds, sky and lighting.
    public class Landscape
    {
        readonly TrackPath path;
        readonly Theme theme;
        readonly DistanceField field;
        readonly float detail;
        readonly int seed;
        readonly Vector2 offs1, offs2, offs3, offs4;
        readonly Vector2 centre;
        readonly Vector2 oceanDir;
        readonly float oceanEdge;
        Rect terrainRect;

        const float ChunkSize = 192f;
        const float InnerMargin = 460f;
        const float OuterMargin = 900f;

        public DistanceField Field { get { return field; } }
        public Rect TerrainRect { get { return terrainRect; } }

        public Landscape(TrackPath path, Theme theme, float detail, int seed)
        {
            this.path = path;
            this.theme = theme;
            this.detail = detail;
            this.seed = seed;
            field = new DistanceField(path);
            var rng = new System.Random(seed);
            offs1 = new Vector2(rng.Next(1000, 9000), rng.Next(1000, 9000));
            offs2 = new Vector2(rng.Next(1000, 9000), rng.Next(1000, 9000));
            offs3 = new Vector2(rng.Next(1000, 9000), rng.Next(1000, 9000));
            offs4 = new Vector2(rng.Next(1000, 9000), rng.Next(1000, 9000));
            centre = new Vector2(path.Bounds.center.x, path.Bounds.center.z);

            if (theme.OceanAngle >= 0f)
            {
                float a = theme.OceanAngle * Mathf.Deg2Rad;
                oceanDir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                oceanEdge = float.MinValue;
                for (int i = 0; i < path.Count; i++)
                {
                    var p = path.Pos[i];
                    oceanEdge = Mathf.Max(oceanEdge, Vector2.Dot(new Vector2(p.x, p.z) - centre, oceanDir));
                }
            }

            var b = path.Bounds;
            terrainRect = Rect.MinMaxRect(b.min.x - OuterMargin, b.min.z - OuterMargin, b.max.x + OuterMargin, b.max.z + OuterMargin);
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        float Perlin(float x, float z, float freq, Vector2 o)
        {
            return Mathf.PerlinNoise(x * freq + o.x, z * freq + o.y);
        }

        // ---------------------------------------------------------------- height function

        float Natural(float x, float z, float d)
        {
            float n1 = Perlin(x, z, 0.0042f, offs1);
            float n2 = Perlin(x, z, 0.013f, offs2);
            float hills = ((n1 - 0.45f) * 2f + (n2 - 0.5f) * 0.6f) * theme.HillAmp;
            hills *= SmoothStep(35f, 150f, d);

            float ridge = 1f - Mathf.Abs(Perlin(x, z, 0.0065f, offs3) * 2f - 1f);
            float mountains = SmoothStep(230f, 560f, d) * theme.MountainHeight * (0.35f + 0.85f * ridge * ridge);

            float lakes = 0f;
            if (theme.HasWater && theme.LakeAmount > 0f)
            {
                float ln = Perlin(x, z, 0.0055f, offs4);
                float thr = 0.66f - theme.LakeAmount * 0.3f;
                lakes = -SmoothStep(thr, thr + 0.1f, ln) * 9f * SmoothStep(6f, 40f, d);
            }

            float ocean = 0f;
            if (theme.HasWater && theme.Island)
            {
                float o = SmoothStep(theme.OceanDistance, theme.OceanDistance + 170f, d + (Perlin(x, z, 0.01f, offs2) - 0.5f) * 60f);
                ocean = -o * 32f;
                mountains *= 1f - o;
                hills = Mathf.Lerp(hills, Mathf.Abs(hills) * 0.6f, 0.5f);
            }
            else if (theme.HasWater && theme.OceanAngle >= 0f)
            {
                float proj = Vector2.Dot(new Vector2(x, z) - centre, oceanDir) - oceanEdge - theme.OceanDistance;
                proj += (Perlin(x, z, 0.008f, offs3) - 0.5f) * 80f;
                float o = SmoothStep(-60f, 120f, proj);
                ocean = -o * 30f;
                mountains *= 1f - SmoothStep(-260f, -40f, proj);
                hills *= 1f - o;
            }
            return hills + mountains + lakes + ocean;
        }

        // terrain height at a world position (deterministic; also used for pillars and props).
        public float Height(float x, float z)
        {
            float dist, ty;
            field.Query(x, z, out dist, out ty);
            float d = Mathf.Max(0f, dist - TrackPath.HalfWidth);
            float nat = Natural(x, z, d);

            float elevated = SmoothStep(1.5f, 5f, ty);
            float nearTarget = Mathf.Lerp(-0.3f, Mathf.Min(nat, -0.3f), elevated);
            float h = Mathf.Lerp(nearTarget, nat, SmoothStep(2f, 28f, d));

            if (ty > 2.5f)
            {
                float cap = ty - 2.6f + Mathf.Max(0f, d - 3f) * 0.6f;
                h = Mathf.Min(h, cap);
            }
            return h;
        }

        Color GroundColor(Vector3 c, Vector3 n, float d, float trackY, System.Random rng)
        {
            Color col;
            float slope = n.y;
            float gn = Perlin(c.x, c.z, 0.02f, offs2);
            Color grass = Color.Lerp(theme.GrassA, theme.GrassB, SmoothStep(0.3f, 0.7f, gn));
            if (theme.HasWater && c.y < theme.WaterLevel + 0.9f)
            {
                float depth = Mathf.Clamp01((theme.WaterLevel - c.y) / 6f);
                col = Color.Lerp(theme.Sand, Color.Lerp(theme.Sand, theme.WaterDeep, 0.6f), depth);
            }
            else if (slope < 0.74f || c.y > 55f)
            {
                float k = SmoothStep(0.74f, 0.6f, slope);
                if (c.y > 55f) k = Mathf.Max(k, SmoothStep(55f, 75f, c.y));
                col = Color.Lerp(grass, theme.Rock, k);
            }
            else if (d < 3.5f && c.y < 1f && trackY < 1.5f)
            {
                col = theme.Shoulder;
            }
            else
            {
                col = grass;
                // a little dirt in hollows
                float dn = Perlin(c.x, c.z, 0.05f, offs4);
                if (dn > 0.72f) col = Color.Lerp(col, theme.Dirt, 0.35f);
            }
            float j = 1f + ((float)rng.NextDouble() - 0.5f) * 0.07f;
            return new Color(col.r * j, col.g * j, col.b * j, 1f);
        }

        // ---------------------------------------------------------------- terrain

        public void BuildTerrain(Transform parent, List<Object> created)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed + 1);
            int innerRes = detail < 0.6f ? 16 : (detail < 0.9f ? 24 : (detail > 1.1f ? 40 : 32));
            const int outerRes = 12;
            var inner = path.Bounds;
            Rect innerRect = Rect.MinMaxRect(inner.min.x - InnerMargin, inner.min.z - InnerMargin, inner.max.x + InnerMargin, inner.max.z + InnerMargin);

            int cx0 = Mathf.FloorToInt(terrainRect.xMin / ChunkSize), cx1 = Mathf.CeilToInt(terrainRect.xMax / ChunkSize);
            int cz0 = Mathf.FloorToInt(terrainRect.yMin / ChunkSize), cz1 = Mathf.CeilToInt(terrainRect.yMax / ChunkSize);

            for (int cx = cx0; cx < cx1; cx++)
                for (int cz = cz0; cz < cz1; cz++)
                {
                    float x0 = cx * ChunkSize, z0 = cz * ChunkSize;
                    var chunkRect = new Rect(x0, z0, ChunkSize, ChunkSize);
                    bool isInner = chunkRect.Overlaps(innerRect);
                    int res = isInner ? innerRes : outerRes;
                    float step = ChunkSize / res;

                    var hs = new float[res + 1, res + 1];
                    for (int i = 0; i <= res; i++)
                        for (int k = 0; k <= res; k++)
                            hs[i, k] = Height(x0 + i * step, z0 + k * step);

                    var mb = new MeshBuilder(1);
                    for (int i = 0; i < res; i++)
                        for (int k = 0; k < res; k++)
                        {
                            Vector3 a = new Vector3(x0 + i * step, hs[i, k], z0 + k * step);
                            Vector3 b = new Vector3(x0 + (i + 1) * step, hs[i + 1, k], z0 + k * step);
                            Vector3 c = new Vector3(x0 + (i + 1) * step, hs[i + 1, k + 1], z0 + (k + 1) * step);
                            Vector3 d = new Vector3(x0 + i * step, hs[i, k + 1], z0 + (k + 1) * step);
                            // alternate the diagonal for a less regular faceted look
                            if (((i + k) & 1) == 0)
                            {
                                GroundTri(mb, a, d, c, rng);
                                GroundTri(mb, a, c, b, rng);
                            }
                            else
                            {
                                GroundTri(mb, a, d, b, rng);
                                GroundTri(mb, b, d, c, rng);
                            }
                        }
                    AddSkirts(mb, hs, res, x0, z0, step);

                    var mesh = mb.ToMesh("Terrain_" + cx + "_" + cz);
                    created.Add(mesh);
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Materials.Ground;
                    mr.shadowCastingMode = isInner ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (isInner && field.Distance(x0 + ChunkSize * 0.5f, z0 + ChunkSize * 0.5f) < 320f)
                        go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
        }

        void GroundTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, System.Random rng)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.y < 0)
            {
                Vector3 t = b; b = c; c = t;
                n = -n;
            }
            n.Normalize();
            Vector3 centroid = (a + b + c) / 3f;
            float dist, ty;
            field.Query(centroid.x, centroid.z, out dist, out ty);
            float d = Mathf.Max(0f, dist - TrackPath.HalfWidth);
            Color col = GroundColor(centroid, n, d, ty, rng);
            int i0 = mb.AddVertex(a, n, new Vector2(a.x, a.z) * 0.05f, col);
            int i1 = mb.AddVertex(b, n, new Vector2(b.x, b.z) * 0.05f, col);
            int i2 = mb.AddVertex(c, n, new Vector2(c.x, c.z) * 0.05f, col);
            mb.AddTriangle(i0, i1, i2);
        }

        void AddSkirts(MeshBuilder mb, float[,] hs, int res, float x0, float z0, float step)
        {
            // Vertical strips around the chunk border hide cracks between different resolutions.
            const float depth = 14f;
            Color col = theme.Rock * 0.8f;
            col.a = 1f;
            for (int i = 0; i < res; i++)
            {
                Skirt(mb, new Vector3(x0 + i * step, hs[i, 0], z0), new Vector3(x0 + (i + 1) * step, hs[i + 1, 0], z0), Vector3.back, depth, col);
                Skirt(mb, new Vector3(x0 + i * step, hs[i, res], z0 + res * step), new Vector3(x0 + (i + 1) * step, hs[i + 1, res], z0 + res * step), Vector3.forward, depth, col);
                Skirt(mb, new Vector3(x0, hs[0, i], z0 + i * step), new Vector3(x0, hs[0, i + 1], z0 + (i + 1) * step), Vector3.left, depth, col);
                Skirt(mb, new Vector3(x0 + res * step, hs[res, i], z0 + i * step), new Vector3(x0 + res * step, hs[res, i + 1], z0 + (i + 1) * step), Vector3.right, depth, col);
            }
        }

        static void Skirt(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 outward, float depth, Color col)
        {
            int i0 = mb.AddVertex(a, outward, Vector2.zero, col);
            int i1 = mb.AddVertex(b, outward, Vector2.zero, col);
            int i2 = mb.AddVertex(b - Vector3.up * depth, outward, Vector2.zero, col);
            int i3 = mb.AddVertex(a - Vector3.up * depth, outward, Vector2.zero, col);
            mb.AddQuadIdx(i0, i1, i2, i3, outward);
        }

        // ---------------------------------------------------------------- water

        public void BuildWater(Transform parent, List<Object> created, Vector3 sunDir)
        {
            if (!theme.HasWater) return;
            var mb = new MeshBuilder(1);
            const int res = 90;
            Rect r = Rect.MinMaxRect(terrainRect.xMin - 2500f, terrainRect.yMin - 2500f, terrainRect.xMax + 2500f, terrainRect.yMax + 2500f);
            // denser grid near the track for nicer waves, the far ring is coarse
            float[] xs = Axis(r.xMin, r.xMax, terrainRect.xMin, terrainRect.xMax, res);
            float[] zs = Axis(r.yMin, r.yMax, terrainRect.yMin, terrainRect.yMax, res);
            int rowLen = zs.Length;
            for (int i = 0; i < xs.Length; i++)
                for (int k = 0; k < zs.Length; k++)
                    mb.AddVertex(new Vector3(xs[i], theme.WaterLevel, zs[k]), Vector3.up, Vector2.zero, Color.white);
            for (int i = 0; i < xs.Length - 1; i++)
                for (int k = 0; k < zs.Length - 1; k++)
                {
                    int a = i * rowLen + k;
                    mb.AddQuadIdx(a, a + 1, a + rowLen + 1, a + rowLen, Vector3.up);
                }
            var mesh = mb.ToMesh("Water");
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.up * 4f);
            created.Add(mesh);
            var go = new GameObject("Water");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = Materials.NewWater(theme, sunDir);
            created.Add(mat);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static float[] Axis(float outerMin, float outerMax, float innerMin, float innerMax, int innerRes)
        {
            var l = new List<float>();
            l.Add(outerMin);
            for (int i = 0; i <= innerRes; i++) l.Add(Mathf.Lerp(innerMin, innerMax, i / (float)innerRes));
            l.Add(outerMax);
            return l.ToArray();
        }

        // ---------------------------------------------------------------- scenery

        public void BuildScenery(Transform parent, List<Object> created)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed + 2);
            var chunks = new Dictionary<long, MeshBuilder>();
            float hw = TrackPath.HalfWidth;

            System.Func<float, float, MeshBuilder> chunkFor = (x, z) =>
            {
                long key = ((long)Mathf.FloorToInt(x / 220f) << 32) ^ (uint)Mathf.FloorToInt(z / 220f);
                MeshBuilder mb;
                if (!chunks.TryGetValue(key, out mb))
                {
                    mb = new MeshBuilder(1);
                    chunks[key] = mb;
                }
                return mb;
            };

            var b = path.Bounds;
            float margin = 460f;
            float spacing = 12.5f / Mathf.Sqrt(Mathf.Max(0.1f, theme.TreeDensity * detail));
            for (float x = b.min.x - margin; x < b.max.x + margin; x += spacing)
                for (float z = b.min.z - margin; z < b.max.z + margin; z += spacing)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * spacing * 0.9f;
                    float pz = z + ((float)rng.NextDouble() - 0.5f) * spacing * 0.9f;
                    double roll = rng.NextDouble();
                    float d = field.Distance(px, pz) - hw;
                    if (d < 14f || d > 420f) continue;
                    float forest = Perlin(px, pz, 0.011f, offs1);
                    float p = forest > 0.5f ? 0.8f : (forest > 0.4f ? 0.3f : 0.07f);
                    p *= 1f - SmoothStep(220f, 420f, d);
                    p *= Mathf.Lerp(0.35f, 1f, SmoothStep(14f, 60f, d)); // keep the area next to the road fairly open
                    if (roll > p) continue;
                    float h = Height(px, pz);
                    if (theme.HasWater && h < theme.WaterLevel + 0.6f) continue;
                    float hx = Height(px + 2f, pz), hz = Height(px, pz + 2f);
                    float slope = Mathf.Max(Mathf.Abs(hx - h), Mathf.Abs(hz - h)) / 2f;
                    if (slope > 0.7f) continue;
                    var kind = theme.Trees[rng.Next(theme.Trees.Length)];
                    // beach props instead of trees on sand in coastal themes
                    if (theme.HasWater && h < theme.WaterLevel + 1.6f)
                    {
                        if ((theme.Island || theme.OceanAngle >= 0f) && rng.NextDouble() < 0.25)
                            Props.Umbrella(chunkFor(px, pz), new Vector3(px, h, pz), rng);
                        else if (kind != TreeKind.Palm && rng.NextDouble() < 0.7) continue;
                    }
                    float scale = 0.85f + (float)rng.NextDouble() * 0.6f;
                    Props.Tree(chunkFor(px, pz), kind, new Vector3(px, h, pz), scale, rng);
                }

            // rocks
            float rockSpacing = 26f / Mathf.Sqrt(Mathf.Max(0.05f, theme.RockDensity * detail));
            for (float x = b.min.x - margin; x < b.max.x + margin; x += rockSpacing)
                for (float z = b.min.z - margin; z < b.max.z + margin; z += rockSpacing)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * rockSpacing;
                    float pz = z + ((float)rng.NextDouble() - 0.5f) * rockSpacing;
                    float d = field.Distance(px, pz) - hw;
                    float size = 0.8f + (float)rng.NextDouble() * 2.2f;
                    if (d < 8f + size * 1.5f || d > 520f) continue;
                    if (rng.NextDouble() > 0.55) continue;
                    float h = Height(px, pz);
                    if (theme.HasWater && h < theme.WaterLevel - 2f) continue;
                    Props.Rock(chunkFor(px, pz), new Vector3(px, h - size * 0.2f, pz), size, theme.Rock, rng);
                }

            // mesas (canyon)
            if (theme.Mesas)
            {
                int placed = 0;
                for (int tries = 0; tries < 400 && placed < 36; tries++)
                {
                    float px = Mathf.Lerp(b.min.x - 380f, b.max.x + 380f, (float)rng.NextDouble());
                    float pz = Mathf.Lerp(b.min.z - 380f, b.max.z + 380f, (float)rng.NextDouble());
                    float size = 22f + (float)rng.NextDouble() * 30f;
                    float d = field.Distance(px, pz) - hw;
                    if (d < size + 25f) continue;
                    float h = Height(px, pz);
                    Props.Mesa(chunkFor(px, pz), new Vector3(px, h, pz), size, theme, rng);
                    placed++;
                }
            }

            // flags along the track
            Color[] flagCols = { theme.AccentColor, Color.white, new Color(0.15f, 0.55f, 1f), new Color(1f, 0.3f, 0.3f) };
            int side = 1, fi = 0;
            for (float s = 30f; s < path.TotalLength - 20f; s += 55f)
            {
                int i = path.IndexAtDistance(s);
                if (!path.HasRoad(i) || path.Pos[i].y > 0.6f) continue;
                side = -side;
                Vector3 r = path.Right[i];
                r.y = 0;
                r.Normalize();
                Vector3 pos = path.Pos[i] + r * side * (hw + 3.2f);
                if (field.Distance(pos.x, pos.z) < hw + 2.5f) continue;
                float h = Height(pos.x, pos.z);
                Vector3 fwd = path.Fwd[i];
                fwd.y = 0;
                Props.Flag(chunkFor(pos.x, pos.z), new Vector3(pos.x, h, pos.z), Quaternion.LookRotation(-fwd.normalized, Vector3.up), flagCols[(fi++) % flagCols.Length]);
            }

            BuildGrandstand(chunkFor, rng);

            foreach (var kv in chunks)
            {
                if (kv.Value.VertexCount == 0) continue;
                var mesh = kv.Value.ToMesh("Scenery");
                created.Add(mesh);
                var go = new GameObject("Scenery");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Materials.VertexLit;
            }
        }

        void BuildGrandstand(System.Func<float, float, MeshBuilder> chunkFor, System.Random rng)
        {
            int i = path.StartLineIndex;
            if (path.Pos[i].y > 0.6f) return;
            float hw = TrackPath.HalfWidth;
            const float length = 34f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 r = path.Right[i];
                r.y = 0;
                r.Normalize();
                Vector3 f = path.Fwd[i];
                f.y = 0;
                f.Normalize();
                Vector3 origin = path.Pos[i] + r * side * (hw + 7f) + f * 6f;
                bool clear = true;
                for (float a = -length * 0.5f - 2f; a <= length * 0.5f + 2f && clear; a += 4f)
                    for (float o = 0f; o <= 14f; o += 3.5f)
                    {
                        Vector3 p = origin + f * a + r * side * o;
                        if (field.Distance(p.x, p.z) < hw + 6f) { clear = false; break; }
                    }
                if (!clear) continue;
                float h = Height(origin.x, origin.z);
                // local +X points away from the track, +Z along it
                Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
                if (side < 0) rot = Quaternion.LookRotation(-f, Vector3.up);
                var m = Matrix4x4.TRS(new Vector3(origin.x, h + 0.3f, origin.z), rot, Vector3.one);
                Props.Grandstand(chunkFor(origin.x, origin.z), m, length, theme.AccentColor, rng);
                return;
            }
        }

        public void BuildClouds(Transform parent, List<Object> created)
        {
            if (theme.Clouds <= 0) return;
            var rng = new System.Random(seed + 3);
            var mb = new MeshBuilder(1);
            Vector3 c = path.Bounds.center;
            for (int i = 0; i < theme.Clouds; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 250f + (float)rng.NextDouble() * 1300f;
                Vector3 p = new Vector3(c.x + Mathf.Cos(a) * dist, 150f + (float)rng.NextDouble() * 140f, c.z + Mathf.Sin(a) * dist);
                Props.Cloud(mb, p, 18f + (float)rng.NextDouble() * 22f, rng);
            }
            var mesh = mb.ToMesh("Clouds");
            created.Add(mesh);
            var go = new GameObject("Clouds");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Cloud;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var drift = go.AddComponent<CloudDrift>();
            drift.Pivot = c;
        }

        // ---------------------------------------------------------------- lighting

        public static Vector3 ApplyLighting(Theme theme, Light sun, List<Object> created)
        {
            sun.type = LightType.Directional;
            sun.color = theme.SunColor;
            sun.intensity = theme.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sun.transform.rotation = Quaternion.Euler(theme.SunEuler);
            Vector3 sunDir = -sun.transform.forward;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = theme.FogColor;
            RenderSettings.fogDensity = theme.FogDensity;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.6f;

            var sky = Materials.NewSky(theme, sunDir);
            if (sky != null)
            {
                created.Add(sky);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();
            return sunDir;
        }
    }

    // slowly rotates the cloud layer around the track.
    public class CloudDrift : MonoBehaviour
    {
        public Vector3 Pivot;
        void Update()
        {
            transform.RotateAround(Pivot, Vector3.up, Time.deltaTime * 0.35f);
        }
    }
}
