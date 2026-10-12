using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    public struct Range
    {
        public int Start, End;
    }

    // The centre line of a track, one sample per metre, with a full frame (forward/right/up)
    // at every sample. Road mesh, scenery, checkpoints, boost pads and respawns all come from
    // this, which is why pieces always line up.
    // Keep in sync with Tools/track_tool.py.
    public class TrackPath
    {
        public const float Tile = 10f;
        public const float Level = 2.5f;
        public const float HalfWidth = 4.5f;
        public const float WallThickness = 0.5f;
        public const float WallHeight = 0.7f;
        public const float SlabDepth = 1.0f;
        public const float LoopRadius = 12f;
        const float RampRise = 0.3f;

        public readonly List<Vector3> Pos = new List<Vector3>();
        public readonly List<float> Yaw = new List<float>();
        public readonly List<bool> Gap = new List<bool>();
        public readonly List<int> LoopId = new List<int>();
        readonly List<Vector3> upHint = new List<Vector3>();
        readonly List<float> bankTarget = new List<float>();

        public Vector3[] Fwd, Right, Up;
        public float[] Dist, Curvature;

        public readonly List<int> Checkpoints = new List<int>();
        public readonly List<Range> Boosts = new List<Range>();
        public int SpawnIndex, StartLineIndex, FinishIndex;
        public float TotalLength;
        public Bounds Bounds;
        public float MinY, MaxY;

        public int Count { get { return Pos.Count; } }

        Vector3 p;
        float yaw;
        int loops;

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        static Vector3 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        void Push(bool gap = false, float bank = 0f, int loop = 0, Vector3 hint = default(Vector3))
        {
            Pos.Add(p);
            Yaw.Add(yaw);
            Gap.Add(gap);
            LoopId.Add(loop);
            bankTarget.Add(bank);
            upHint.Add(hint);
        }

        // ease: 0 smooth, 1 accelerating (t^2), 2 linear
        void Straight(float length, float rise = 0f, int ease = 0, bool gap = false)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(length));
            float y0 = p.y;
            Vector3 step = Dir(yaw) * (length / n);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float e = ease == 0 ? Smooth(t) : ease == 1 ? t * t : t;
                p += step;
                p.y = y0 + rise * e;
                Push(gap);
            }
        }

        void Turn(float size, int dir, float rise, bool bank)
        {
            float radius = (size - 0.5f) * Tile;
            float angle = 90f * dir;
            float arc = radius * Mathf.PI * 0.5f;
            int n = Mathf.Max(2, Mathf.RoundToInt(arc));
            float y0 = p.y, yaw0 = yaw;
            float bankDeg = bank ? Mathf.Clamp(140f / radius + 8f, 10f, 22f) : 0f;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                p += Dir(yaw0 + angle * (i - 0.5f) / n) * (arc / n);
                yaw = yaw0 + angle * t;
                p.y = y0 + rise * Smooth(t);
                Push(false, -dir * bankDeg);
            }
        }

        // A full vertical loop. It moves forward two tiles and shifts one tile sideways so the
        // way out runs next to the way in.
        void Loop(int side)
        {
            loops++;
            Vector3 f0 = Dir(yaw), r0 = Vector3.Cross(Vector3.up, f0);
            Vector3 start = p;
            float advance = 2f * Tile;
            int n = Mathf.RoundToInt(2f * Mathf.PI * LoopRadius * 1.15f);
            for (int i = 1; i <= n; i++)
            {
                float th = 2f * Mathf.PI * i / n;
                float along = LoopRadius * Mathf.Sin(th) + advance * th / (2f * Mathf.PI);
                float height = LoopRadius * (1f - Mathf.Cos(th));
                float lateral = side * Tile * Smooth(th / (2f * Mathf.PI));
                p = start + f0 * along + r0 * lateral + Vector3.up * height;
                Vector3 up = Vector3.up * Mathf.Cos(th) - f0 * Mathf.Sin(th);
                Push(false, 0f, loops, up);
            }
            p.y = start.y;
        }

        public static TrackPath Build(TrackInfo info)
        {
            var path = new TrackPath();
            path.Push();
            int start = 0, finish = -1;
            foreach (var pc in info.Pieces)
            {
                int here = path.Pos.Count - 1;
                switch (pc.Type)
                {
                    case PieceType.Start:
                        start = here;
                        path.Straight(Tile);
                        break;
                    case PieceType.Finish:
                        finish = here + (int)(Tile / 2);
                        path.Straight(Tile * 3);   // finish tile + run-off
                        break;
                    case PieceType.Checkpoint:
                        path.Checkpoints.Add(here + (int)(Tile / 2));
                        path.Straight(Tile);
                        break;
                    case PieceType.Forward:
                        path.Straight(pc.Size * Tile);
                        break;
                    case PieceType.Boost:
                        path.Straight(pc.Size * Tile);
                        path.Boosts.Add(new Range { Start = here, End = path.Pos.Count - 1 });
                        break;
                    case PieceType.Slope:
                        path.Straight(pc.Size * Tile, pc.Dir * pc.Size * Level * (pc.Steep ? 2f : 1f));
                        break;
                    case PieceType.Turn:
                        path.Turn(pc.Size, pc.Dir, pc.Levels * Level, pc.Bank);
                        break;
                    case PieceType.Ramp:
                        path.Straight(Tile, RampRise, 1);
                        break;
                    case PieceType.Gap:
                        path.Straight(pc.Size * Tile, pc.Levels * Level, 2, true);
                        break;
                    case PieceType.Loop:
                        path.Loop(pc.Dir);
                        break;
                }
            }
            path.Finish(start, finish);
            return path;
        }

        void Finish(int start, int finish)
        {
            int n = Pos.Count;
            Fwd = new Vector3[n];
            Right = new Vector3[n];
            Up = new Vector3[n];
            Dist = new float[n];
            Curvature = new float[n];

            for (int i = 1; i < n; i++) Dist[i] = Dist[i - 1] + Vector3.Distance(Pos[i - 1], Pos[i]);
            TotalLength = Dist[n - 1];

            // blur the bank angle so it rolls in and out instead of snapping
            var bank = bankTarget.ToArray();
            var tmp = new float[n];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f;
                    int c = 0;
                    for (int k = Mathf.Max(0, i - 8); k <= Mathf.Min(n - 1, i + 8); k++) { sum += bank[k]; c++; }
                    tmp[i] = sum / c;
                }
                System.Array.Copy(tmp, bank, n);
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 f = Pos[Mathf.Min(n - 1, i + 1)] - Pos[Mathf.Max(0, i - 1)];
                if (f.sqrMagnitude < 1e-6f) f = Dir(Yaw[i]);
                f.Normalize();
                Vector3 r, u;
                if (upHint[i] != Vector3.zero)
                {
                    r = Vector3.Cross(upHint[i], f).normalized;
                    u = Vector3.Cross(f, r);
                }
                else
                {
                    r = Vector3.Cross(Vector3.up, f).normalized;
                    u = Vector3.Cross(f, r);
                    var q = Quaternion.AngleAxis(bank[i], f);
                    r = q * r;
                    u = q * u;
                }
                Fwd[i] = f;
                Right[i] = r;
                Up[i] = u;
            }

            // how fast the road bends towards its own "up" (loops, dips) - the car uses this to stick
            for (int i = 1; i < n - 1; i++)
            {
                float ds = Dist[i + 1] - Dist[i - 1];
                if (ds > 1e-4f) Curvature[i] = Vector3.Dot((Fwd[i + 1] - Fwd[i - 1]) / ds, Up[i]);
            }

            SpawnIndex = Mathf.Min(n - 1, start + 3);
            StartLineIndex = Mathf.Min(n - 1, start + 8);
            FinishIndex = finish > 0 ? finish : n - 1;

            Bounds = new Bounds(Pos[0], Vector3.zero);
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            foreach (var q in Pos)
            {
                Bounds.Encapsulate(q);
                MinY = Mathf.Min(MinY, q.y);
                MaxY = Mathf.Max(MaxY, q.y);
            }
        }

        public bool HasRoad(int i) { return i >= 0 && i + 1 < Count && !Gap[i + 1]; }

        public bool InLoop(int i) { return i >= 0 && i < Count && LoopId[i] > 0; }

        public Quaternion RotationAt(int i) { return Quaternion.LookRotation(Fwd[i], Up[i]); }

        public Vector3 SurfacePoint(int i, float lateral, float height)
        {
            return Pos[i] + Right[i] * lateral + Up[i] * height;
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
            return d - Dist[lo] < Dist[hi] - d ? lo : hi;
        }

        public int Nearest(Vector3 q, int hint, int window)
        {
            int a = 0, b = Count - 1;
            if (hint >= 0)
            {
                a = Mathf.Max(0, hint - window);
                b = Mathf.Min(Count - 1, hint + window);
            }
            int best = a;
            float bestD = float.MaxValue;
            for (int i = a; i <= b; i++)
            {
                float d = (Pos[i] - q).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // A rough "clean lap" time used to set medal targets.
        public float ParTime()
        {
            int n = Count;
            const float top = CarController.TopSpeed, accel = 24f, brake = 34f, grip = 34f;
            var limit = new float[n];
            for (int i = 0; i < n; i++)
            {
                float turn = 0f;
                for (int k = Mathf.Max(1, i - 3); k <= Mathf.Min(n - 2, i + 3); k++)
                    turn = Mathf.Max(turn, Mathf.Abs(Mathf.DeltaAngle(Yaw[k - 1], Yaw[k + 1])) * Mathf.Deg2Rad / 2f);
                limit[i] = turn < 1e-4f ? top : Mathf.Min(top, Mathf.Sqrt(grip / turn));
            }
            foreach (var b in Boosts)
                for (int k = b.Start; k <= b.End; k++) limit[k] = top + 20f;

            var v = new float[n];
            for (int i = 1; i < n; i++)
            {
                float x = v[i - 1] / top;
                float a = accel * Mathf.Max(0.05f, 1f - x * x);
                v[i] = Mathf.Min(limit[i], Mathf.Sqrt(v[i - 1] * v[i - 1] + 2f * a));
            }
            for (int i = n - 2; i >= 0; i--) v[i] = Mathf.Min(v[i], Mathf.Sqrt(v[i + 1] * v[i + 1] + 2f * brake));

            float time = 0f;
            for (int i = SpawnIndex + 1; i <= FinishIndex; i++)
                time += (Dist[i] - Dist[i - 1]) / Mathf.Max(2f, 0.5f * (v[i] + v[i - 1]));
            return time;
        }
    }
}
