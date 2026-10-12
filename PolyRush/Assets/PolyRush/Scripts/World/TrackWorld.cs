using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    /// <summary>A fully built track + environment. Destroying it frees every generated asset.</summary>
    public class TrackWorld : MonoBehaviour
    {
        public TrackInfo Info { get; private set; }
        public Theme Theme { get; private set; }
        public TrackPath Path { get; private set; }
        public EnvironmentBuilder Env { get; private set; }
        public int BuiltQuality { get; private set; }

        readonly List<Object> created = new List<Object>();

        /// <summary>Builds the world over a few frames so a loading screen can animate.</summary>
        public static IEnumerator Build(TrackInfo info, Light sun, System.Action<TrackWorld> done)
        {
            var go = new GameObject("World_" + info.Id);
            var world = go.AddComponent<TrackWorld>();
            world.Info = info;
            world.BuiltQuality = GameSettings.Quality;
            world.Theme = Theme.Get(info.ThemeId);
            world.Path = TrackPath.Build(info);
            yield return null;

            Vector3 sunDir = EnvironmentBuilder.ApplyLighting(world.Theme, sun, world.created);
            world.Env = new EnvironmentBuilder(world.Path, world.Theme, GameSettings.DetailScale, 1234 + info.Index * 97);
            yield return null;

            world.Env.BuildTerrain(go.transform, world.created);
            yield return null;
            world.Env.BuildWater(go.transform, world.created, sunDir);
            TrackMeshBuilder.Build(world.Path, world.Theme, world.Env.Height, go.transform, world.created);
            yield return null;
            world.Env.BuildScenery(go.transform, world.created);
            world.Env.BuildClouds(go.transform, world.created);
            yield return null;
            done(world);
        }

        void OnDestroy()
        {
            foreach (var o in created)
                if (o != null) Destroy(o);
            created.Clear();
        }
    }
}
