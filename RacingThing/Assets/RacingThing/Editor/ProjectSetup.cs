using System.IO;
using RelayNetworking.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingThing.EditorTools
{
    // Runs when the project opens: makes sure the main scene exists, is in the build list,
    // and that the player settings are what the game expects.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/RacingThing/Scenes/Main.unity";
        public const string CarPrefabPath = "Assets/RacingThing/Prefabs/NetCar.prefab";

        static ProjectSetup()
        {
            EditorApplication.delayCall += OnOpen;
        }

        static void OnOpen()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string key = "RacingThing.Opened." + Application.dataPath;
            bool first = !EditorPrefs.GetBool(key, false);
            EnsureAll();
            if (first)
            {
                EditorPrefs.SetBool(key, true);
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.path == ScenePath) EnsureNetwork(scene);
        }

        [MenuItem("Racing Thing/Fix Project Setup", priority = 0)]
        public static void FixFromMenu()
        {
            EnsureAll();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EnsureNetwork(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
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

        // Multiplayer needs RelayNetworking's NetworkManager in the scene with the NetCar prefab as
        // its player prefab. Both ship with the project; this only puts them back if they go missing.
        public static void EnsureNetwork(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetCar>() == null)
            {
                var temp = new GameObject("NetCar");
                temp.AddComponent<NetworkObject>();
                temp.AddComponent<NetworkTransform>();
                temp.AddComponent<NetCar>();
                Directory.CreateDirectory(Path.GetDirectoryName(CarPrefabPath));
                prefab = PrefabUtility.SaveAsPrefabAsset(temp, CarPrefabPath);
                Object.DestroyImmediate(temp);
                Debug.Log("Rebuilt " + CarPrefabPath);
            }

            NetworkManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<NetworkManager>(true);
                if (manager != null) break;
            }
            bool dirty = false;
            if (manager == null)
            {
                var go = new GameObject("Network");
                SceneManager.MoveGameObjectToScene(go, scene);
                manager = go.AddComponent<NetworkManager>();
                dirty = true;
            }
            if (manager.GetComponent<NetworkPrefabRegistry>() == null)
            {
                manager.gameObject.AddComponent<NetworkPrefabRegistry>();
                dirty = true;
            }

            var so = new SerializedObject(manager);
            var player = so.FindProperty("_playerPrefab");
            if (player != null && player.objectReferenceValue == null)
            {
                player.objectReferenceValue = prefab.GetComponent<NetworkObject>();
                dirty = true;
            }
            var kick = so.FindProperty("AllowDropConnection");
            if (kick != null && !kick.boolValue)
            {
                kick.boolValue = true;
                dirty = true;
            }
            if (!dirty) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Set up the Network object in " + scene.path);
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
            EnsureNetwork(scene);
            if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Exponential) return;
            UseFog();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
