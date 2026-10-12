using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    /// <summary>Loads every track file from Resources/Tracks, sorted by file name.</summary>
    public static class TrackLibrary
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
            var assets = Resources.LoadAll<TextAsset>("Tracks");
            var sorted = new List<TextAsset>(assets);
            sorted.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var asset in sorted)
            {
                var info = TrackInfo.Parse(asset.name, asset.text);
                if (info.Commands.Count == 0) continue;
                info.Index = tracks.Count;
                var path = TrackPath.Build(info);
                info.Length = path.TotalLength;
                info.CheckpointCount = path.Checkpoints.Count;
                float par = path.EstimateParTime();
                info.AuthorTime = Round(par * 1.02f);
                info.GoldTime = Round(par * 1.10f);
                info.SilverTime = Round(par * 1.25f);
                info.BronzeTime = Round(par * 1.50f);
                tracks.Add(info);
            }
            if (tracks.Count == 0) Debug.LogError("PolyRush: no tracks found in Resources/Tracks");
        }

        static float Round(float t) { return Mathf.Ceil(t * 10f) / 10f; }

        public static string[] Ids()
        {
            var ids = new string[All.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = All[i].Id;
            return ids;
        }
    }
}
