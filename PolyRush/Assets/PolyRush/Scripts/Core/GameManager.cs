using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PolyRush
{
    /// <summary>
    /// Entry point and state machine: menus <-> loading <-> racing.
    /// Created automatically when any scene starts, so the project has no scene wiring to break.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        Camera cam;
        CameraRig rig;
        Light sun;
        ReflectionProbe skyProbe;
        MenuUI menu;
        RaceHud hud;
        TrackWorld world;
        RaceController race;
        bool loading;
        int selectedTrack;

        public int SelectedTrack { get { return selectedTrack; } }
        public TrackWorld World { get { return world; } }
        public bool IsLoading { get { return loading; } }
        public bool IsOverlayOpen { get { return menu != null && menu.OverlayOpen; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("PolyRush");
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Time.fixedDeltaTime = 0.01f;   // 100 Hz physics for stable high-speed driving
            Time.maximumDeltaTime = 0.1f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            GameSettings.Apply(true);

            // remove any camera / light / event system a scene might contain
            foreach (var c in FindObjectsOfType<Camera>()) if (c.gameObject != gameObject) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsOfType<Light>()) l.gameObject.SetActive(false);

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowMSAA = true;
            cam.allowHDR = false;
            camGo.AddComponent<AudioListener>();
            rig = camGo.AddComponent<CameraRig>();

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;

            // Sky-only reflection probe so glossy paint and glass reflect the current sky.
            var probeGo = new GameObject("SkyReflections");
            probeGo.transform.SetParent(transform, false);
            skyProbe = probeGo.AddComponent<ReflectionProbe>();
            skyProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            skyProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            skyProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            skyProbe.cullingMask = 0;
            skyProbe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            skyProbe.resolution = 128;
            skyProbe.size = new Vector3(100000f, 100000f, 100000f);
            skyProbe.importance = 0;

            gameObject.AddComponent<AudioManager>();

            // UI root
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand keeps the whole 1920x1080 layout visible on 16:10, 4:3 and ultrawide screens.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            hud = canvasGo.AddComponent<RaceHud>();
            hud.Build(canvasGo.transform, this);
            menu = canvasGo.AddComponent<MenuUI>();
            menu.Build(canvasGo.transform, this);
        }

        void Start()
        {
            var tracks = TrackLibrary.All;
            selectedTrack = Mathf.Clamp(PlayerPrefs.GetInt("pr_last_track", 0), 0, Mathf.Max(0, tracks.Count - 1));
            if (tracks.Count == 0)
            {
                Debug.LogError("PolyRush: no tracks - nothing to show");
                return;
            }
            StartCoroutine(LoadWorld(selectedTrack, () => menu.ShowMain()));
        }

        void Update()
        {
            if (GameSettings.ShowFps) hud.UpdateFps(Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------------ navigation API (used by UI)

        public void ShowMainMenu()
        {
            EndRace();
            menu.ShowMain();
        }

        public void ShowTrackSelect()
        {
            EndRace();
            menu.ShowTrackSelect(selectedTrack);
        }

        public void SelectTrack(int index)
        {
            if (loading) return;
            selectedTrack = index;
            PlayerPrefs.SetInt("pr_last_track", index);
            if (world != null && world.Info.Index == index && world.BuiltQuality == GameSettings.Quality)
            {
                menu.ShowTrackSelect(index);
                return;
            }
            StartCoroutine(LoadWorld(index, () => menu.ShowTrackSelect(index)));
        }

        public void StartRace(int index)
        {
            if (loading) return;
            selectedTrack = index;
            PlayerPrefs.SetInt("pr_last_track", index);
            if (world != null && world.Info.Index == index && world.BuiltQuality == GameSettings.Quality)
            {
                BeginRace();
                return;
            }
            StartCoroutine(LoadWorld(index, BeginRace));
        }

        public void NextTrack()
        {
            int next = (selectedTrack + 1) % TrackLibrary.All.Count;
            EndRace();
            StartRace(next);
        }

        public void RestartRace()
        {
            if (race != null) race.Restart();
        }

        public void ResumeRace()
        {
            if (race != null) race.SetPaused(false);
        }

        public void ExitToTrackSelect()
        {
            ShowTrackSelect();
        }

        public void OpenSettingsFromPause()
        {
            menu.ShowSettings(true);
        }

        public void OnSettingsChanged()
        {
            GameSettings.Apply(false);
            if (race != null && race.Car != null)
            {
                // car colour is applied on the next race; camera mode applies immediately
                rig.SnapBehindTarget();
            }
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ internals

        void BeginRace()
        {
            EndRace();
            menu.HideAll();
            var go = new GameObject("Race");
            race = go.AddComponent<RaceController>();
            hud.Show(true);
            race.Init(world, rig, hud, this);
        }

        void EndRace()
        {
            if (race != null)
            {
                Destroy(race.gameObject);
                race = null;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            hud.Show(false);
            rig.Target = null;
            rig.CurrentMode = CameraRig.Mode.Menu;
            if (world != null) rig.MenuPath = world.Path;
        }

        IEnumerator LoadWorld(int index, System.Action after)
        {
            loading = true;
            EndRace();
            var info = TrackLibrary.All[index];
            menu.ShowLoading(info);
            yield return null;
            yield return null;
            if (world != null)
            {
                Destroy(world.gameObject);
                world = null;
            }
            yield return null;
            TrackWorld built = null;
            yield return StartCoroutine(TrackWorld.Build(info, sun, w => built = w));
            world = built;
            skyProbe.RenderProbe();
            rig.MenuPath = world.Path;
            rig.CurrentMode = CameraRig.Mode.Menu;
            System.GC.Collect();
            loading = false;
            menu.HideLoading();
            if (after != null) after();
        }
    }
}
