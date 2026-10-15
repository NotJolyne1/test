using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingThing
{
    // Builds meshes from code. The Flat* helpers give each face its own vertices (hard low-poly
    // edges) and flip faces to point away from an "inside" point, so I never have to think about
    // winding order. Vertex alpha below 1 = glow in the RacingThing/Lit shader.
    public class MeshBuilder
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color> Colors = new List<Color>();
        readonly List<int>[] tris;

        public Matrix4x4 Matrix = Matrix4x4.identity;
        static bool? linear;

        public MeshBuilder(int submeshes = 1)
        {
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        public int VertexCount { get { return Verts.Count; } }

        public static Color Fix(Color c)
        {
            c.a = Mathf.Clamp01(c.a);
            if (!linear.HasValue) linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            if (linear.Value)
            {
                float a = c.a;
                c = c.linear;
                c.a = a;
            }
            return c;
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            Verts.Add(p);
            Normals.Add(n);
            Uvs.Add(uv);
            Colors.Add(Fix(c));
            return Verts.Count - 1;
        }

        public void AddTriangle(int a, int b, int c, int sub = 0)
        {
            var t = tris[sub];
            t.Add(a); t.Add(b); t.Add(c);
        }

        // adds a quad from 4 existing vertex indices, oriented so its face normal agrees with 'expected'.
        public void AddQuadIdx(int a, int b, int c, int d, Vector3 expected, int sub = 0)
        {
            Vector3 n = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
            if (Vector3.Dot(n, expected) < 0f)
            {
                AddTriangle(a, c, b, sub);
                AddTriangle(a, d, c, sub);
            }
            else
            {
                AddTriangle(a, b, c, sub);
                AddTriangle(a, c, d, sub);
            }
        }

        Vector3 T(Vector3 p) { return Matrix.MultiplyPoint3x4(p); }

        // ---------------------------------------------------------------- flat primitives

        // flat triangle in local space (transformed by Matrix). Oriented away from 'inside' (local space).
        public void FlatTri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 inside, int sub = 0)
        {
            Vector3 wa = T(a), wb = T(b), wc = T(c), wi = T(inside);
            Vector3 n = Vector3.Cross(wb - wa, wc - wa);
            Vector3 centre = (wa + wb + wc) / 3f;
            if (Vector3.Dot(n, centre - wi) < 0f)
            {
                Vector3 tmp = wb; wb = wc; wc = tmp;
                n = -n;
            }
            n.Normalize();
            int i0 = AddVertex(wa, n, new Vector2(wa.x, wa.z) * 0.1f, col);
            int i1 = AddVertex(wb, n, new Vector2(wb.x, wb.z) * 0.1f, col);
            int i2 = AddVertex(wc, n, new Vector2(wc.x, wc.z) * 0.1f, col);
            AddTriangle(i0, i1, i2, sub);
        }

        // flat triangle with an explicit facing direction (local space), for open surfaces.
        public void FlatTriFacing(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 facing, int sub = 0)
        {
            Vector3 centre = (a + b + c) / 3f;
            FlatTri(a, b, c, col, centre - facing, sub);
        }

        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 inside, int sub = 0)
        {
            FlatTri(a, b, c, col, inside, sub);
            FlatTri(a, c, d, col, inside, sub);
        }

        // flat quad with explicit UVs (banners, signs). 'facing' is the desired normal (local space).
        public void QuadUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
                           Color col, Vector3 facing, int sub = 0)
        {
            Vector3 wa = T(a), wb = T(b), wc = T(c), wd = T(d);
            Vector3 wf = Matrix.MultiplyVector(facing).normalized;
            int i0 = AddVertex(wa, wf, ua, col);
            int i1 = AddVertex(wb, wf, ub, col);
            int i2 = AddVertex(wc, wf, uc, col);
            int i3 = AddVertex(wd, wf, ud, col);
            AddQuadIdx(i0, i1, i2, i3, wf, sub);
        }

        // double sided flat quad (leaves, flags).
        public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color front, Color back, int sub = 0)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector3 centre = (a + b + c + d) * 0.25f;
            FlatQuad(a, b, c, d, front, centre - n * 0.01f, sub);
            FlatQuad(a, b, c, d, back, centre + n * 0.01f, sub);
        }

        public void DoubleTri(Vector3 a, Vector3 b, Vector3 c, Color front, Color back, int sub = 0)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector3 centre = (a + b + c) / 3f;
            FlatTri(a, b, c, front, centre - n * 0.01f, sub);
            FlatTri(a, b, c, back, centre + n * 0.01f, sub);
        }

        // Convex hexahedron from 8 corners: 0-3 bottom ring, 4-7 top ring (same order).

        // faceColors (optional, 6 entries): bottom, top, then the 4 sides (0-1, 1-2, 2-3, 3-0).
        public void Hexa(Vector3[] c, Color col, Color[] faceColors = null, int sub = 0)
        {
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < 8; i++) centre += c[i];
            centre /= 8f;
            Color cb = faceColors != null ? faceColors[0] : col;
            Color ct = faceColors != null ? faceColors[1] : col;
            FlatQuad(c[0], c[1], c[2], c[3], cb, centre, sub);
            FlatQuad(c[4], c[5], c[6], c[7], ct, centre, sub);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Color cs = faceColors != null ? faceColors[2 + i] : col;
                FlatQuad(c[i], c[j], c[j + 4], c[i + 4], cs, centre, sub);
            }
        }

        public void Box(Vector3 centre, Vector3 size, Color col, int sub = 0)
        {
            Vector3 h = size * 0.5f;
            Hexa(new[]
            {
                centre + new Vector3(-h.x, -h.y, -h.z), centre + new Vector3(h.x, -h.y, -h.z),
                centre + new Vector3(h.x, -h.y, h.z), centre + new Vector3(-h.x, -h.y, h.z),
                centre + new Vector3(-h.x, h.y, -h.z), centre + new Vector3(h.x, h.y, -h.z),
                centre + new Vector3(h.x, h.y, h.z), centre + new Vector3(-h.x, h.y, h.z),
            }, col, null, sub);
        }

        // box tapered on top (top half-extents scaled by 'topScale').
        public void TaperBox(Vector3 centre, Vector3 size, float topScaleX, float topScaleZ, Color col, int sub = 0)
        {
            Vector3 h = size * 0.5f;
            float tx = h.x * topScaleX, tz = h.z * topScaleZ;
            Hexa(new[]
            {
                centre + new Vector3(-h.x, -h.y, -h.z), centre + new Vector3(h.x, -h.y, -h.z),
                centre + new Vector3(h.x, -h.y, h.z), centre + new Vector3(-h.x, -h.y, h.z),
                centre + new Vector3(-tx, h.y, -tz), centre + new Vector3(tx, h.y, -tz),
                centre + new Vector3(tx, h.y, tz), centre + new Vector3(-tx, h.y, tz),
            }, col, null, sub);
        }

        // cylinder / frustum along local +Y from y0 to y1.
        public void Cylinder(Vector3 baseCentre, float r0, float r1, float height, int sides, Color col,
                             bool capBottom = true, bool capTop = true, int sub = 0, float angleOffset = 0f)
        {
            Vector3 top = baseCentre + Vector3.up * height;
            Vector3 mid = baseCentre + Vector3.up * (height * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides;
                float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCentre + d0 * r0, b1 = baseCentre + d1 * r0;
                Vector3 t0 = top + d0 * r1, t1 = top + d1 * r1;
                if (r1 > 0.0001f) FlatQuad(b0, b1, t1, t0, col, mid, sub);
                else FlatTri(b0, b1, top, col, mid, sub);
                if (capBottom && r0 > 0.0001f) FlatTri(baseCentre, b1, b0, col, mid + Vector3.up * height, sub);
                if (capTop && r1 > 0.0001f) FlatTri(top, t0, t1, col, mid - Vector3.up * height, sub);
            }
        }

        // cylinder along an arbitrary local axis (used for wheels).
        public void CylinderAxis(Vector3 centre, Vector3 axis, float radius, float width, int sides, Color side, Color cap, int sub = 0)
        {
            axis.Normalize();
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            Vector3 a = centre - axis * (width * 0.5f);
            Vector3 b = centre + axis * (width * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius;
                Vector3 d1 = (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                FlatQuad(a + d0, a + d1, b + d1, b + d0, side, centre, sub);
                FlatTri(a, a + d0, a + d1, cap, centre + axis, sub);
                FlatTri(b, b + d1, b + d0, cap, centre - axis, sub);
            }
        }

        static readonly Vector3[] IcoVerts;
        static readonly int[] IcoTris =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        static MeshBuilder()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            IcoVerts = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < IcoVerts.Length; i++) IcoVerts[i] = IcoVerts[i].normalized;
        }

        // Flat-shaded, optionally subdivided and jittered icosphere (foliage, rocks, clouds).

        // colorFn lets callers shade per face (gets the face normal in local space).
        public void Blob(Vector3 centre, Vector3 scale, int subdiv, float jitter, System.Random rng, Color col,
                         System.Func<Vector3, Color> colorFn = null, int sub = 0)
        {
            var verts = new List<Vector3>(IcoVerts);
            var faces = new List<int>(IcoTris);
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                var nf = new List<int>();
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = nf;
            }
            if (jitter > 0f && rng != null)
            {
                for (int i = 0; i < verts.Count; i++)
                    verts[i] = verts[i] * (1f + ((float)rng.NextDouble() * 2f - 1f) * jitter);
            }
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = Vector3.Scale(verts[faces[f]], scale);
                Vector3 b = Vector3.Scale(verts[faces[f + 1]], scale);
                Vector3 c = Vector3.Scale(verts[faces[f + 2]], scale);
                Color fc = col;
                if (colorFn != null)
                {
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                    if (Vector3.Dot(n, a + b + c) < 0) n = -n;
                    fc = colorFn(n);
                }
                FlatTri(centre + a, centre + b, centre + c, fc, centre, sub);
            }
        }

        static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }

        // ---------------------------------------------------------------- output

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            if (Verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Clear()
        {
            Verts.Clear();
            Normals.Clear();
            Uvs.Clear();
            Colors.Clear();
            foreach (var t in tris) t.Clear();
        }
    }
}
