using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PolyRush
{
    public enum PieceType { Start, Finish, Checkpoint, Forward, Boost, Turn, Slope, Ramp, Gap, Loop }

    public struct Piece
    {
        public PieceType Type;
        public float Size;      // tiles (length for straights, radius class for turns)
        public int Dir;         // turns/loops: +1 right, -1 left. slopes: +1 up, -1 down
        public bool Steep;
        public bool Bank;
        public float Levels;    // height change for turns and gaps, in levels
        public int Line;
    }

    public class TrackInfo
    {
        public string Id;
        public int Index;
        public string Name = "Untitled";
        public string ThemeId = "meadow";
        public int Difficulty = 1;
        public List<Piece> Pieces = new List<Piece>();

        public float Length;
        public int Checkpoints;
        public float AuthorTime, GoldTime, SilverTime, BronzeTime;

        public string DifficultyName
        {
            get
            {
                switch (Difficulty)
                {
                    case 1: return "Easy";
                    case 2: return "Medium";
                    case 3: return "Hard";
                    default: return "Expert";
                }
            }
        }

        public Color DifficultyColor
        {
            get
            {
                switch (Difficulty)
                {
                    case 1: return new Color(0.4f, 0.85f, 0.45f);
                    case 2: return new Color(1f, 0.8f, 0.25f);
                    case 3: return new Color(1f, 0.5f, 0.25f);
                    default: return new Color(0.95f, 0.3f, 0.4f);
                }
            }
        }

        // Track files are plain text, one piece per line. See README for the format.
        public static TrackInfo Parse(string id, string text)
        {
            var t = new TrackInfo { Id = id };
            var lines = text.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                    string val = line.Substring(colon + 1).Trim();
                    if (key == "name") t.Name = val;
                    else if (key == "theme") t.ThemeId = val.ToLowerInvariant();
                    else if (key == "difficulty") int.TryParse(val, out t.Difficulty);
                    continue;
                }

                var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var p = new Piece { Size = 1, Line = n + 1 };
                float number = -1;
                foreach (var w in words)
                {
                    float v;
                    if (float.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { number = v; continue; }
                    if (w == "steep") p.Steep = true;
                    else if (w == "bank") p.Bank = true;
                    else if (w == "left") p.Dir = -1;
                    else if (w.StartsWith("up=")) p.Levels = Num(w);
                    else if (w.StartsWith("down=")) p.Levels = -Num(w);
                    else if (w.StartsWith("drop=")) p.Levels = -Num(w);
                }
                if (number > 0) p.Size = number;

                switch (words[0].ToLowerInvariant())
                {
                    case "start": p.Type = PieceType.Start; break;
                    case "finish": p.Type = PieceType.Finish; break;
                    case "cp":
                    case "checkpoint": p.Type = PieceType.Checkpoint; break;
                    case "f":
                    case "forward": p.Type = PieceType.Forward; break;
                    case "boost": p.Type = PieceType.Boost; break;
                    case "left": p.Type = PieceType.Turn; p.Dir = -1; break;
                    case "right": p.Type = PieceType.Turn; p.Dir = 1; break;
                    case "up": p.Type = PieceType.Slope; p.Dir = 1; break;
                    case "down": p.Type = PieceType.Slope; p.Dir = -1; break;
                    case "ramp": p.Type = PieceType.Ramp; break;
                    case "gap": p.Type = PieceType.Gap; break;
                    case "loop": p.Type = PieceType.Loop; if (p.Dir == 0) p.Dir = 1; break;
                    default:
                        Debug.LogWarning("Track " + id + " line " + (n + 1) + ": don't know '" + words[0] + "'");
                        continue;
                }
                t.Pieces.Add(p);
            }
            return t;
        }

        static float Num(string w)
        {
            float v;
            float.TryParse(w.Substring(w.IndexOf('=') + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            return v;
        }
    }
}
