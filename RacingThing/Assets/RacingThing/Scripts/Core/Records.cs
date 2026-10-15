using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RacingThing
{
    public enum Medal { None = 0, Bronze = 1, Silver = 2, Gold = 3, Author = 4 }

    // Personal bests, checkpoint splits and ghost files. All stored on this computer.
    public static class Records
    {
        static string Key(string trackId, string what) { return "rt_rec_" + trackId + "_" + what; }

        public static float BestTime(string trackId)
        {
            return PlayerPrefs.GetFloat(Key(trackId, "best"), -1f);
        }

        public static bool HasBest(string trackId) { return BestTime(trackId) > 0f; }

        public static float[] BestSplits(string trackId)
        {
            string s = PlayerPrefs.GetString(Key(trackId, "splits"), "");
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(';');
            var res = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                float v;
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return null;
                res[i] = v;
            }
            return res;
        }

        // saves the run if it's a new best. returns true if it was
        public static bool SubmitRun(string trackId, float time, float[] splits, Ghost ghost)
        {
            float best = BestTime(trackId);
            if (best > 0f && time >= best) return false;
            PlayerPrefs.SetFloat(Key(trackId, "best"), time);
            var parts = new string[splits.Length];
            for (int i = 0; i < splits.Length; i++) parts[i] = splits[i].ToString("R", CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(Key(trackId, "splits"), string.Join(";", parts));
            PlayerPrefs.Save();
            if (ghost != null) SaveGhost(trackId, ghost);
            return true;
        }

        public static void ResetAll(string[] trackIds)
        {
            foreach (var id in trackIds)
            {
                PlayerPrefs.DeleteKey(Key(id, "best"));
                PlayerPrefs.DeleteKey(Key(id, "splits"));
                try { File.Delete(GhostPath(id)); } catch (Exception) { }
            }
            PlayerPrefs.Save();
        }

        static string GhostPath(string trackId)
        {
            return Path.Combine(Path.Combine(Application.persistentDataPath, "ghosts"), trackId + ".ghost");
        }

        public static void SaveGhost(string trackId, Ghost ghost)
        {
            try
            {
                string path = GhostPath(trackId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, ghost.ToBytes());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't save ghost: " + e.Message);
            }
        }

        public static Ghost LoadGhost(string trackId)
        {
            try
            {
                string path = GhostPath(trackId);
                if (!File.Exists(path)) return null;
                return Ghost.FromBytes(File.ReadAllBytes(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't load ghost: " + e.Message);
                return null;
            }
        }

        public static Medal MedalFor(TrackInfo info, float time)
        {
            if (time <= 0f) return Medal.None;
            if (time <= info.AuthorTime) return Medal.Author;
            if (time <= info.GoldTime) return Medal.Gold;
            if (time <= info.SilverTime) return Medal.Silver;
            if (time <= info.BronzeTime) return Medal.Bronze;
            return Medal.None;
        }

        public static string MedalName(Medal m)
        {
            switch (m)
            {
                case Medal.Author: return "Author";
                case Medal.Gold: return "Gold";
                case Medal.Silver: return "Silver";
                case Medal.Bronze: return "Bronze";
                default: return "";
            }
        }

        public static Color MedalColor(Medal m)
        {
            switch (m)
            {
                case Medal.Author: return new Color(0.35f, 0.95f, 0.75f);
                case Medal.Gold: return new Color(1f, 0.82f, 0.2f);
                case Medal.Silver: return new Color(0.82f, 0.86f, 0.92f);
                case Medal.Bronze: return new Color(0.86f, 0.55f, 0.3f);
                default: return new Color(1f, 1f, 1f, 0.25f);
            }
        }

        // 83.4567 -> 1:23.456
        public static string FormatTime(float t)
        {
            if (t < 0f) return "-:--.---";
            int ms = Mathf.FloorToInt(t * 1000f + 0.0001f);
            int m = ms / 60000;
            int s = (ms / 1000) % 60;
            int f = ms % 1000;
            return m + ":" + s.ToString("00") + "." + f.ToString("000");
        }

        public static string FormatDelta(float d)
        {
            string sign = d < 0 ? "-" : "+";
            d = Mathf.Abs(d);
            int ms = Mathf.FloorToInt(d * 1000f + 0.0001f);
            int s = ms / 1000;
            int f = ms % 1000;
            return sign + s + "." + f.ToString("000");
        }
    }
}
