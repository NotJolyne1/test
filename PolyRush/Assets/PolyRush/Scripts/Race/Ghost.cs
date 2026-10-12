using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PolyRush
{
    // A recorded run: the car's position and rotation every 20 ms. Same data could be sent over
    // the network later for multiplayer.
    public class Ghost
    {
        const int Magic = 0x50524731; // "PRG1"
        public const float Interval = 0.02f;

        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Quaternion> Rotations = new List<Quaternion>();
        public float FinishTime;
        public int CarColor;

        public float Duration { get { return Mathf.Max(0, Positions.Count - 1) * Interval; } }

        public void Add(Vector3 p, Quaternion r)
        {
            Positions.Add(p);
            Rotations.Add(r);
        }

        public bool Sample(float time, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (Positions.Count == 0) return false;
            float f = Mathf.Max(0f, time) / Interval;
            int i = Mathf.FloorToInt(f);
            if (i >= Positions.Count - 1)
            {
                pos = Positions[Positions.Count - 1];
                rot = Rotations[Rotations.Count - 1];
                return false;
            }
            float t = f - i;
            pos = Vector3.LerpUnclamped(Positions[i], Positions[i + 1], t);
            rot = Quaternion.Slerp(Rotations[i], Rotations[i + 1], t);
            return true;
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);
                w.Write(FinishTime);
                w.Write(CarColor);
                w.Write(Positions.Count);
                for (int i = 0; i < Positions.Count; i++)
                {
                    var p = Positions[i];
                    var r = Rotations[i];
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(r.x); w.Write(r.y); w.Write(r.z); w.Write(r.w);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static Ghost FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadInt32() != Magic) throw new Exception("bad ghost file");
                var g = new Ghost();
                g.FinishTime = r.ReadSingle();
                g.CarColor = r.ReadInt32();
                int n = r.ReadInt32();
                if (n < 0 || n > 200000) throw new Exception("bad ghost length");
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    g.Add(p, q);
                }
                return g;
            }
        }
    }
}
