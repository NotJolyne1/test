using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PolyRush.EditorTools
{
    // Runs when the project opens: makes sure the main scene exists, is in the build list,
    // and that the player settings are what the game expects.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/PolyRush/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += OnOpen;
        }

        static void OnOpen()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string key = "PolyRush.Opened." + Application.dataPath;
            bool first = !EditorPrefs.GetBool(key, false);
            EnsureAll();
            if (!first) return;
            EditorPrefs.SetBool(key, true);
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("PolyRush/Fix Project Setup", priority = 0)]
        public static void FixFromMenu()
        {
            EnsureAll();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void EnsureAll()
        {
            EnsureScene();
            var scenes = EditorBuildSettings.scenes;
            bool listed = false;
            foreach (var s in scenes) listed |= s.path == ScenePath && s.enabled;
            if (!listed) EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = GameInfo.Studio;
            PlayerSettings.productName = GameInfo.Title;
            PlayerSettings.bundleVersion = GameInfo.Version;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            // Unity won't put a new untitled scene next to another untitled one, so replace it
            bool replace = string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replace ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            UseFog();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!replace) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }

        // The game turns fog on at runtime, but Unity strips fog shader variants from a build
        // unless a scene in it uses fog. So the main scene is saved with fog switched on.
        static void UseFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0012f;
        }

        public static void PrepareForBuild()
        {
            EnsureAll();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Exponential) return;
            UseFog();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
