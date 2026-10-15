using System.Collections.Generic;
using UnityEngine;

namespace RacingThing
{
    public static class TrackList
    {
        static List<TrackInfo> tracks;

        public static List<TrackInfo> All
        {
            get
            {
                if (tracks == null) Load();
                return tracks;
            }
        }

        static void Load()
        {
            tracks = new List<TrackInfo>();
            var files = new List<TextAsset>(Resources.LoadAll<TextAsset>("Tracks"));
            files.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var file in files)
            {
                var t = TrackInfo.Parse(file.name, file.text);
                if (t.Pieces.Count == 0) continue;
                t.Index = tracks.Count;

                var path = TrackPath.Build(t);
                t.Length = path.TotalLength;
                t.Checkpoints = path.Checkpoints.Count;
                float par = path.ParTime();
                t.AuthorTime = Tenths(par * 1.03f);
                t.GoldTime = Tenths(par * 1.12f);
                t.SilverTime = Tenths(par * 1.28f);
                t.BronzeTime = Tenths(par * 1.55f);
                tracks.Add(t);
            }
            if (tracks.Count == 0) Debug.LogError("No tracks in Resources/Tracks");
        }

        static float Tenths(float t) { return Mathf.Ceil(t * 10f) / 10f; }

        public static string[] Ids()
        {
            var ids = new string[All.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = All[i].Id;
            return ids;
        }
    }
}
