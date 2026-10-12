using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PolyRush.EditorTools
{
    /// <summary>
    /// One-time project setup that runs automatically when the project is opened:
    /// creates the main scene, registers it in Build Settings and applies player settings.
    /// Everything in the game is generated from code, so this is all the wiring needed.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/PolyRush/Scenes/Main.unity";
        const string DoneKey = "PolyRush.SetupDone.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool firstTime = !File.Exists(ScenePath) || !EditorPrefs.GetBool(DoneKey + "." + Application.dataPath, false);
            EnsureAll();
            if (firstTime)
            {
                EditorPrefs.SetBool(DoneKey + "." + Application.dataPath, true);
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("PolyRush: project ready. Press Play to start the game, or use the PolyRush menu to build for Windows.");
            }
        }

        [MenuItem("PolyRush/Setup Project", priority = 0)]
        public static void SetupFromMenu()
        {
            EnsureAll();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("PolyRush: setup complete.");
        }

        public static void EnsureAll()
        {
            EnsureScene();
            EnsureBuildSettings();
            EnsurePlayerSettings();
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var current = SceneManager.GetActiveScene();
            string currentPath = current.path;
            // Unity cannot add an untitled scene next to another untitled scene, so replace it instead.
            bool replace = string.IsNullOrEmpty(currentPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replace ? NewSceneMode.Single : NewSceneMode.Additive);
            var marker = new GameObject("PolyRush (game boots automatically)");
            SceneManager.MoveGameObjectToScene(marker, scene);
            SceneManager.SetActiveScene(scene);
            ApplySceneRenderSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!replace) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The game sets fog at runtime, but Unity strips fog shader variants from builds unless a scene
        /// in the build uses that fog mode - so the main scene is saved with exponential fog enabled.
        /// </summary>
        public static void ApplySceneRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0012f;
            RenderSettings.fogColor = new Color(0.77f, 0.89f, 0.96f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        /// <summary>Makes sure the saved main scene has the render settings the build needs.</summary>
        public static void PrepareSceneForBuild()
        {
            EnsureAll();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Exponential)
            {
                ApplySceneRenderSettings();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath && s.enabled) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void EnsurePlayerSettings()
        {
            PlayerSettings.companyName = GameInfo.Studio;
            PlayerSettings.productName = "PolyRush";
            PlayerSettings.bundleVersion = GameInfo.Version;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;
        }
    }
}
