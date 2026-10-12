using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    public struct BoostRange
    {
        public int Start;
        public int End;
    }

    /// <summary>
    /// The sampled centre line of a track (one sample per metre) with a full orientation frame
    /// per sample. Everything else (road mesh, scenery masking, checkpoints, boost pads,
    /// respawning, camera) is derived from this, which guarantees the pieces always line up.
    /// Keep the sampling in sync with Tools/track_tool.py.
    /// </summary>
    public class TrackPath
    {
        public const float Step = 1f;
        public const float HalfWidth = 6f;
        public const float WallThickness = 0.6f;
        public const float WallHeight = 1.0f;
        public const float SlabDepth = 0.8f;

        public readonly List<Vector3> Pos = new List<Vector3>();
        public readonly List<float> Yaw = new List<float>();
        public readonly List<bool> Gap = new List<bool>();
        readonly List<float> bankTarget = new List<float>();

        public Vector3[] Fwd;
        public Vector3[] Right;
        public Vector3[] Up;
        public float[] Dist;
        public float[] Bank;

        public readonly List<int> Checkpoints = new List<int>();
        public readonly List<BoostRange> Boosts = new List<BoostRange>();
        public int SpawnIndex;
        public int StartLineIndex;
        public int FinishIndex;
        public float TotalLength;
        public Bounds Bounds;
        public float MinY, MaxY;

        public int Count { get { return Pos.Count; } }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        static Vector3 Dir(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        void Push(Vector3 p, float yaw, bool gap, float bank)
        {
            Pos.Add(p);
            Yaw.Add(yaw);
            Gap.Add(gap);
            bankTarget.Add(bank);
        }

        public static TrackPath Build(TrackInfo info)
        {
            var path = new TrackPath();
            Vector3 p = Vector3.zero;
            float yaw = 0f;
            path.Push(p, yaw, false, 0f);
            var boostMarks = new List<KeyValuePair<int, float>>();

            foreach (var c in info.Commands)
            {
                switch (c.Op)
                {
                    case "S":
                    case "UP":
                    case "DOWN":
                    {
                        float len = c.Arg(0, 10f);
                        float rise = c.Arg(1, 0f);
                        if (c.Op == "DOWN") rise = -Mathf.Abs(rise);
                        int n = Mathf.Max(1, Mathf.RoundToInt(len / Step));
                        float y0 = p.y;
                        Vector3 d = Dir(yaw) * (len / n);
                        for (int i = 1; i <= n; i++)
                        {
                            float t = i / (float)n;
                            p += d;
                            p.y = y0 + rise * Smooth(t);
                            path.Push(p, yaw, false, 0f);
                        }
                        break;
                    }
                    case "L":
                    case "R":
                    {
                        float ang = Mathf.Abs(c.Arg(0, 90f)) * (c.Op == "R" ? 1f : -1f);
                        float rad = Mathf.Max(8f, c.Arg(1, 40f));
                        float rise = c.Arg(2, 0f);
                        float arc = rad * Mathf.Abs(ang) * Mathf.Deg2Rad;
                        int n = Mathf.Max(2, Mathf.RoundToInt(arc / Step));
                        float y0 = p.y, yaw0 = yaw;
                        float bank = -Mathf.Sign(ang) * Mathf.Abs(c.Bank);
                        for (int i = 1; i <= n; i++)
                        {
                            float t = i / (float)n;
                            float ym = yaw0 + ang * (i - 0.5f) / n;
                            p += Dir(ym) * (arc / n);
                            yaw = yaw0 + ang * t;
                            p.y = y0 + rise * Smooth(t);
                            path.Push(p, yaw, false, bank);
                        }
                        break;
                    }
                    case "KICK":
                    case "LAND":
                    case "OUTRUN":
                    case "GAP":
                    {
                        float len = c.Arg(0, 10f);
                        float rise = c.Arg(1, 0f);
                        if (c.Op == "LAND" || c.Op == "OUTRUN") rise = -Mathf.Abs(rise);
                        int n = Mathf.Max(1, Mathf.RoundToInt(len / Step));
                        float y0 = p.y;
                        Vector3 d = Dir(yaw) * (len / n);
                        bool gap = c.Op == "GAP";
                        for (int i = 1; i <= n; i++)
                        {
                            float t = i / (float)n;
                            p += d;
                            float e;
                            if (c.Op == "KICK" || c.Op == "LAND") e = t * t;
                            else if (c.Op == "OUTRUN") e = 1f - (1f - t) * (1f - t);
                            else e = t;
                            p.y = y0 + rise * e;
                            path.Push(p, yaw, gap, 0f);
                        }
                        break;
                    }
                    case "CP":
                        path.Checkpoints.Add(path.Pos.Count - 1);
                        break;
                    case "BOOST":
                        boostMarks.Add(new KeyValuePair<int, float>(path.Pos.Count - 1, c.Arg(0, 15f)));
                        break;
                    default:
                        Debug.LogWarning("PolyRush: unknown track command '" + c.Op + "' (track " + info.Id + ", line " + c.Line + ")");
                        break;
                }
            }

            path.Finish(boostMarks);
            return path;
        }

        void Finish(List<KeyValuePair<int, float>> boostMarks)
        {
            int n = Pos.Count;
            Fwd = new Vector3[n];
            Right = new Vector3[n];
            Up = new Vector3[n];
            Dist = new float[n];
            Bank = new float[n];

            // Arc length
            for (int i = 1; i < n; i++) Dist[i] = Dist[i - 1] + Vector3.Distance(Pos[i - 1], Pos[i]);
            TotalLength = Dist[n - 1];

            // Smooth bank (two box blur passes ~ +-12 m) so banking always eases in and out.
            var tmp = new float[n];
            for (int i = 0; i < n; i++) Bank[i] = bankTarget[i];
            for (int pass = 0; pass < 2; pass++)
            {
                const int r = 12;
                for (int i = 0; i < n; i++)
                {
                    float s = 0f;
                    int c = 0;
                    for (int k = i - r; k <= i + r; k++)
                    {
                        if (k < 0 || k >= n) continue;
                        s += Bank[k];
                        c++;
                    }
                    tmp[i] = s / c;
                }
                System.Array.Copy(tmp, Bank, n);
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 a = Pos[Mathf.Max(0, i - 1)];
                Vector3 b = Pos[Mathf.Min(n - 1, i + 1)];
                Vector3 f = (b - a);
                if (f.sqrMagnitude < 1e-6f) f = Dir(Yaw[i]);
                f.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
                Vector3 u = Vector3.Cross(f, r).normalized;
                Quaternion q = Quaternion.AngleAxis(Bank[i], f);
                Fwd[i] = f;
                Right[i] = q * r;
                Up[i] = q * u;
            }

            foreach (var kv in boostMarks)
            {
                int s = Mathf.Clamp(kv.Key, 0, n - 1);
                int e = s;
                while (e < n - 1 && Dist[e] - Dist[s] < kv.Value) e++;
                var br = new BoostRange();
                br.Start = s;
                br.End = e;
                Boosts.Add(br);
            }

            SpawnIndex = IndexAtDistance(6f);
            StartLineIndex = IndexAtDistance(11f);
            FinishIndex = IndexAtDistance(Mathf.Max(TotalLength - 70f, TotalLength * 0.75f));

            MinY = float.MaxValue;
            MaxY = float.MinValue;
            Bounds = new Bounds(Pos[0], Vector3.zero);
            for (int i = 0; i < n; i++)
            {
                Bounds.Encapsulate(Pos[i]);
                MinY = Mathf.Min(MinY, Pos[i].y);
                MaxY = Mathf.Max(MaxY, Pos[i].y);
            }
        }

        public int IndexAtDistance(float d)
        {
            int lo = 0, hi = Count - 1;
            if (d <= 0) return 0;
            if (d >= Dist[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Dist[mid] < d) lo = mid; else hi = mid;
            }
            return (d - Dist[lo] < Dist[hi] - d) ? lo : hi;
        }

        public Quaternion RotationAt(int i)
        {
            return Quaternion.LookRotation(Fwd[i], Up[i]);
        }

        /// <summary>Point on the road surface at a lateral offset from the centre line.</summary>
        public Vector3 SurfacePoint(int i, float lateral, float height)
        {
            return Pos[i] + Right[i] * lateral + Up[i] * height;
        }

        /// <summary>True if the segment between sample i and i+1 has road (is not part of a gap).</summary>
        public bool HasRoad(int i)
        {
            return i >= 0 && i + 1 < Count && !Gap[i + 1];
        }

        public int Nearest(Vector3 p, int hint, int window)
        {
            int best = -1;
            float bestD = float.MaxValue;
            int a = 0, b = Count - 1;
            if (hint >= 0)
            {
                a = Mathf.Max(0, hint - window);
                b = Mathf.Min(Count - 1, hint + window);
            }
            for (int i = a; i <= b; i++)
            {
                float d = (Pos[i] - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Rough "good driver" time used for medal targets: curvature-limited speed profile
        /// with acceleration / braking passes. Mirrors par_time() in Tools/track_tool.py.
        /// </summary>
        public float EstimateParTime()
        {
            int n = Count;
            const float maxSpeed = CarController.DefaultMaxSpeed;
            const float accel = 22f, brake = 28f, latMax = 30f;
            var curv = new float[n];
            for (int i = 1; i < n - 1; i++)
            {
                float dyaw = Mathf.Abs(Yaw[i + 1] - Yaw[i - 1]) * Mathf.Deg2Rad;
                curv[i] = dyaw / (2f * Step);
            }
            var boost = new bool[n];
            foreach (var b in Boosts) for (int k = b.Start; k <= b.End; k++) boost[k] = true;
            var vmax = new float[n];
            for (int i = 0; i < n; i++)
            {
                float c = 0f;
                for (int k = Mathf.Max(0, i - 3); k <= Mathf.Min(n - 1, i + 3); k++) c = Mathf.Max(c, curv[k]);
                vmax[i] = c < 1e-4f ? maxSpeed : Mathf.Min(maxSpeed + 15f, Mathf.Sqrt(latMax / c));
            }
            var v = new float[n];
            for (int i = 1; i < n; i++)
            {
                float x = v[i - 1] / maxSpeed;
                float acc = accel * Mathf.Max(0.05f, 1f - x * x);
                if (boost[i]) acc += 30f;
                v[i] = Mathf.Min(vmax[i], Mathf.Sqrt(v[i - 1] * v[i - 1] + 2f * acc * Step));
            }
            for (int i = n - 2; i >= 0; i--) v[i] = Mathf.Min(v[i], Mathf.Sqrt(v[i + 1] * v[i + 1] + 2f * brake * Step));
            float t = 0f;
            for (int i = Mathf.Max(1, SpawnIndex); i <= FinishIndex; i++) t += Step / Mathf.Max(1f, 0.5f * (v[i] + v[i - 1]));
            return t;
        }
    }
}
