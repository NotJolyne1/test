using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingThing
{
    // Starts by itself when any scene loads, so there's nothing to wire up in the editor.
    // Owns the camera, sun, UI and whichever track is loaded.
    public class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        // what the multiplayer side of the game is showing right now
        public enum OnlineView { None, Lobby, Race }

        ChaseCam cam;
        Light sun;
        ReflectionProbe sky;
        Menus menus;
        Hud hud;
        TrackWorld world;
        Race race;
        Online online;
        Transform tagLayer;
        int selected;

        public bool Loading { get; private set; }
        public bool MenuOpen { get { return menus != null && menus.OverlayOpen; } }
        public TrackWorld World { get { return world; } }
        public OnlineView View { get; private set; }
        public Transform TagLayer { get { return tagLayer; } }

        // used by the online cars and the live race order
        public CarController LocalCar { get { return race != null ? race.Car : null; } }
        public int LocalCheckpoints { get { return race != null ? race.CheckpointsHit : 0; } }
        public double LocalProgress() { return race != null ? race.Progress : 0.0; }
        public bool InOnlineRace { get { return race != null && race.Online; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("RacingThing").AddComponent<Game>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Time.fixedDeltaTime = 0.01f;   // 100 Hz physics, the car is fast
            Time.maximumDeltaTime = 0.1f;
            Physics.defaultSolverIterations = 8;
            Settings.Apply(true);

            foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsOfType<Light>()) l.gameObject.SetActive(false);

            // kept at the top level: RelayNetworking's NetworkSpawner.DestroyAll() pulls the
            // main camera out of whatever it's parented to when you leave a room
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            DontDestroyOnLoad(camGo);
            var c0 = camGo.AddComponent<Camera>();
            c0.nearClipPlane = 0.25f;
            c0.farClipPlane = 4000f;
            c0.allowHDR = false;
            camGo.AddComponent<AudioListener>();
            cam = camGo.AddComponent<ChaseCam>();

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;

            // sky-only probe so shiny paint reflects the right sky
            sky = new GameObject("Sky Probe").AddComponent<ReflectionProbe>();
            sky.transform.SetParent(transform, false);
            sky.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            sky.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            sky.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            sky.cullingMask = 0;
            sky.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            sky.resolution = 128;
            sky.size = Vector3.one * 100000f;

            gameObject.AddComponent<Sfx>();

            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var ui = new GameObject("UI", typeof(RectTransform));
            ui.transform.SetParent(transform, false);
            ui.layer = 5;
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui.AddComponent<GraphicRaycaster>();

            // name tags over other people's cars go underneath everything else
            tagLayer = Ui.Node("Name tags", ui.transform).transform;
            Ui.Fill(Ui.Rect(tagLayer.gameObject));

            online = gameObject.AddComponent<Online>();
            online.Init(this);
            online.Dropped += OnDropped;

            hud = ui.AddComponent<Hud>();
            hud.Build(ui.transform, this);
            menus = ui.AddComponent<Menus>();
            menus.Build(ui.transform, this);
        }

        void Start()
        {
            if (TrackList.All.Count == 0) return;
            selected = Mathf.Clamp(PlayerPrefs.GetInt("rt_last_track", 0), 0, TrackList.All.Count - 1);
            StartCoroutine(Load(selected, menus.ShowMain));
        }

        public void ShowMain()
        {
            EndRace();
            View = OnlineView.None;
            menus.ShowMain();
        }

        // ------------------------------------------------------------ online

        public void ShowOnline()
        {
            EndRace();
            View = OnlineView.None;
            menus.ShowOnline(null);
        }

        public void ShowLobby()
        {
            EndRace();
            View = OnlineView.Lobby;
            menus.ShowLobby();
        }

        // the room is gone: we left, got kicked or lost the connection
        void OnDropped(string reason)
        {
            EndRace();
            View = OnlineView.None;
            menus.ShowOnline(reason);
        }

        public void LoadOnlineTrack(int index, System.Action done)
        {
            Remember(index);
            if (Ready(index))
            {
                EndRace();
                done();
            }
            else StartCoroutine(Load(index, done));
        }

        public void BeginOnlineRace(int slot)
        {
            if (world == null) return;
            EndRace();
            menus.HideAll();
            View = OnlineView.Race;
            race = new GameObject("Race").AddComponent<Race>();
            hud.Show(true);
            race.InitOnline(world, cam, hud, this, online.Race, slot);
        }

        public void ShowTracks()
        {
            EndRace();
            menus.ShowTracks(selected);
        }

        public void Select(int index)
        {
            if (Loading || online.State != Online.Status.Offline) return;
            Remember(index);
            if (Ready(index)) menus.ShowTracks(index);
            else StartCoroutine(Load(index, () => menus.ShowTracks(index)));
        }

        public void StartRace(int index)
        {
            if (Loading || online.State != Online.Status.Offline) return;
            Remember(index);
            if (Ready(index)) BeginRace();
            else StartCoroutine(Load(index, BeginRace));
        }

        public void NextTrack()
        {
            EndRace();
            StartRace((selected + 1) % TrackList.All.Count);
        }

        public void Retry() { if (race != null) race.Restart(); }
        public void Resume() { if (race != null) race.Pause(false); }
        public void OpenSettings() { menus.ShowSettings(); }

        public void SettingsChanged()
        {
            Settings.Apply(false);
            if (race != null) cam.Snap();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Remember(int index)
        {
            selected = index;
            PlayerPrefs.SetInt("rt_last_track", index);
        }

        bool Ready(int index)
        {
            return world != null && world.Info.Index == index && world.Quality == Settings.Quality;
        }

        void BeginRace()
        {
            EndRace();
            menus.HideAll();
            race = new GameObject("Race").AddComponent<Race>();
            hud.Show(true);
            race.Init(world, cam, hud, this);
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
            cam.Target = null;
            cam.Menu = true;
            if (world != null) cam.MenuPath = world.Path;
        }

        IEnumerator Load(int index, System.Action then)
        {
            Loading = true;
            EndRace();
            var info = TrackList.All[index];
            menus.ShowLoading(info);
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
            sky.RenderProbe();
            cam.MenuPath = world.Path;
            cam.Menu = true;
            System.GC.Collect();
            Loading = false;
            menus.HideLoading();
            if (then != null) then();
        }
    }
}
