using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    // Fast horizontal distance-to-track queries (spatial hash of centre-line segments).
    // Used to flatten terrain under the road and to keep scenery off the track.
    public class DistanceField
    {
        const float Cell = 40f;
        public const float MaxRange = 115f;
        const int Stride = 3;

        readonly Vector3[] pts;
        readonly Vector3[] coarse;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        public DistanceField(TrackPath path)
        {
            var list = new List<Vector3>();
            for (int i = 0; i < path.Count; i += Stride) list.Add(path.Pos[i]);
            if ((path.Count - 1) % Stride != 0) list.Add(path.Pos[path.Count - 1]);
            pts = list.ToArray();

            var c = new List<Vector3>();
            for (int i = 0; i < path.Count; i += 25) c.Add(path.Pos[i]);
            c.Add(path.Pos[path.Count - 1]);
            coarse = c.ToArray();

            for (int k = 0; k < pts.Length - 1; k++)
            {
                Vector3 a = pts[k], b = pts[k + 1];
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - MaxRange) / Cell);
                int x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + MaxRange) / Cell);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - MaxRange) / Cell);
                int z1 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + MaxRange) / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        long key = Key(x, z);
                        List<int> l;
                        if (!grid.TryGetValue(key, out l))
                        {
                            l = new List<int>();
                            grid[key] = l;
                        }
                        l.Add(k);
                    }
            }
        }

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        // horizontal distance to the centre line and the track height at the nearest point.
        public void Query(float x, float z, out float dist, out float trackY)
        {
            List<int> l;
            long key = Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell));
            float best = float.MaxValue;
            trackY = 0f;
            if (grid.TryGetValue(key, out l))
            {
                for (int n = 0; n < l.Count; n++)
                {
                    int k = l[n];
                    Vector3 a = pts[k], b = pts[k + 1];
                    float abx = b.x - a.x, abz = b.z - a.z;
                    float apx = x - a.x, apz = z - a.z;
                    float len2 = abx * abx + abz * abz;
                    float t = len2 > 1e-6f ? Mathf.Clamp01((apx * abx + apz * abz) / len2) : 0f;
                    float dx = apx - abx * t, dz = apz - abz * t;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < best)
                    {
                        best = d2;
                        trackY = a.y + (b.y - a.y) * t;
                    }
                }
            }
            if (best < MaxRange * MaxRange)
            {
                dist = Mathf.Sqrt(best);
                return;
            }
            // Far away: coarse brute force (precision is irrelevant out there).
            for (int i = 0; i < coarse.Length; i++)
            {
                float dx = x - coarse[i].x, dz = z - coarse[i].z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best)
                {
                    best = d2;
                    trackY = coarse[i].y;
                }
            }
            dist = Mathf.Max(MaxRange, Mathf.Sqrt(best));
        }

        public float Distance(float x, float z)
        {
            float d, y;
            Query(x, z, out d, out y);
            return d;
        }
    }
}
