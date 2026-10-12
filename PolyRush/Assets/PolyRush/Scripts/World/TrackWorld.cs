using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    // One fully built track and its surroundings. Destroying it frees all generated meshes.
    public class TrackWorld : MonoBehaviour
    {
        public TrackInfo Info { get; private set; }
        public Theme Theme { get; private set; }
        public TrackPath Path { get; private set; }
        public Landscape Land { get; private set; }
        public int Quality { get; private set; }

        readonly List<Object> owned = new List<Object>();

        // spread over a few frames so the loading screen keeps animating
        public static IEnumerator Build(TrackInfo info, Light sun, System.Action<TrackWorld> done)
        {
            var go = new GameObject("Track " + info.Name);
            var w = go.AddComponent<TrackWorld>();
            w.Info = info;
            w.Quality = Settings.Quality;
            w.Theme = Theme.Get(info.ThemeId);
            w.Path = TrackPath.Build(info);
            yield return null;

            Vector3 sunDir = Landscape.ApplyLighting(w.Theme, sun, w.owned);
            w.Land = new Landscape(w.Path, w.Theme, Settings.DetailScale, 1234 + info.Index * 97);
            yield return null;
            w.Land.BuildTerrain(go.transform, w.owned);
            yield return null;
            w.Land.BuildWater(go.transform, w.owned, sunDir);
            TrackBuilder.Build(w.Path, w.Theme, w.Land.Height, go.transform, w.owned);
            yield return null;
            w.Land.BuildScenery(go.transform, w.owned);
            w.Land.BuildClouds(go.transform, w.owned);
            yield return null;
            done(w);
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
            owned.Clear();
        }
    }
}
