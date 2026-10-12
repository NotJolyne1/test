using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PolyRush
{
    public class TrackCommand
    {
        public string Op;
        public float[] Args;
        public float Bank;
        public int Line;

        public float Arg(int i, float def)
        {
            return i < Args.Length ? Args[i] : def;
        }
    }

    /// <summary>
    /// Track metadata + the parsed build commands. Tracks are plain text files in
    /// Resources/Tracks so new ones can be added without touching code
    /// (Tools/track_tool.py validates and previews them).
    /// </summary>
    public class TrackInfo
    {
        public string Id;
        public int Index;
        public string Name = "Unnamed";
        public string ThemeId = "meadow";
        public int Difficulty = 1;
        public readonly List<TrackCommand> Commands = new List<TrackCommand>();

        // Filled in by TrackLibrary after sampling the path once.
        public float Length;
        public int CheckpointCount;
        public float AuthorTime, GoldTime, SilverTime, BronzeTime;

        public string DifficultyName
        {
            get
            {
                switch (Difficulty)
                {
                    case 1: return "EASY";
                    case 2: return "MEDIUM";
                    case 3: return "HARD";
                    default: return "EXPERT";
                }
            }
        }

        public Color DifficultyColor
        {
            get
            {
                switch (Difficulty)
                {
                    case 1: return new Color(0.35f, 0.85f, 0.4f);
                    case 2: return new Color(0.98f, 0.78f, 0.2f);
                    case 3: return new Color(1f, 0.45f, 0.2f);
                    default: return new Color(0.95f, 0.25f, 0.35f);
                }
            }
        }

        public static TrackInfo Parse(string id, string text)
        {
            var info = new TrackInfo();
            info.Id = id;
            var lines = text.Split('\n');
            for (int ln = 0; ln < lines.Length; ln++)
            {
                string line = lines[ln];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                int colon = line.IndexOf(':');
                if (colon > 0 && line.Substring(0, colon).Trim().IndexOf(' ') < 0)
                {
                    string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                    string val = line.Substring(colon + 1).Trim();
                    switch (key)
                    {
                        case "name": info.Name = val; break;
                        case "theme": info.ThemeId = val.ToLowerInvariant(); break;
                        case "difficulty":
                            int d;
                            if (int.TryParse(val, out d)) info.Difficulty = Mathf.Clamp(d, 1, 4);
                            break;
                    }
                    continue;
                }

                var toks = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var cmd = new TrackCommand();
                cmd.Op = toks[0].ToUpperInvariant();
                cmd.Line = ln + 1;
                var args = new List<float>();
                for (int i = 1; i < toks.Length; i++)
                {
                    string t = toks[i];
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = t.Substring(0, eq).ToLowerInvariant();
                        float v;
                        if (float.TryParse(t.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        {
                            if (k == "b" || k == "bank") cmd.Bank = v;
                        }
                        continue;
                    }
                    float f;
                    if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) args.Add(f);
                    else Debug.LogWarning("PolyRush: track " + id + " line " + (ln + 1) + ": bad number '" + t + "'");
                }
                cmd.Args = args.ToArray();
                info.Commands.Add(cmd);
            }
            return info;
        }
    }
}
