using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PolyRush
{
    public class Menus : MonoBehaviour
    {
        enum Page { None, Main, Tracks }

        Game game;
        Transform canvas;
        GameObject pageRoot, overlayRoot, loading;
        Page page;
        bool overlay;
        int overlayClosedFrame = -1;
        int selected;
        Text loadingName, loadingInfo;
        RectTransform spinner;
        readonly List<Action> refresh = new List<Action>();

        // true while settings/controls are up (or were closed this frame, so Esc isn't handled twice)
        public bool OverlayOpen { get { return overlay || overlayClosedFrame == Time.frameCount; } }

        public void Build(Transform canvas, Game game)
        {
            this.canvas = canvas;
            this.game = game;
            pageRoot = Ui.Node("Pages", canvas);
            Ui.Fill(Ui.Rect(pageRoot));
            overlayRoot = Ui.Node("Overlay", canvas);
            Ui.Fill(Ui.Rect(overlayRoot));
            BuildLoading();
        }

        static void Clear(GameObject go)
        {
            foreach (Transform c in go.transform) Destroy(c.gameObject);
        }

        public void HideAll()
        {
            Clear(pageRoot);
            Clear(overlayRoot);
            page = Page.None;
            overlay = false;
        }

        static RectTransform TopLeft(Component c, float x, float y, float w, float h)
        {
            return Ui.Place(c.GetComponent<RectTransform>(), Vector2.up, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(w, h));
        }

        // ------------------------------------------------------------ main menu

        public void ShowMain()
        {
            HideAll();
            page = Page.Main;
            var t = pageRoot.transform;

            var side = Ui.Plain(t, "Shade", new Color(0.02f, 0.04f, 0.1f, 0.4f));
            Ui.Place(side.rectTransform, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(720f, 0f));

            string name = GameInfo.Title;
            var logo = Ui.Label(t, name.Length > 4 ? name.Substring(0, 4) + "<color=#FFB829>" + name.Substring(4) + "</color>" : name, 124, Color.white, TextAnchor.MiddleLeft);
            TopLeft(logo, 100f, -140f, 900f, 150f);
            Ui.AddShadow(logo, 6f, 0.35f);
            var sub = Ui.Label(t, GameInfo.Subtitle, 34, Ui.Accent, TextAnchor.MiddleLeft);
            TopLeft(sub, 108f, -284f, 600f, 44f);
            Ui.AddShadow(sub);

            var items = new List<KeyValuePair<string, UnityEngine.Events.UnityAction>>
            {
                new KeyValuePair<string, UnityEngine.Events.UnityAction>("Play", game.ShowTracks),
                new KeyValuePair<string, UnityEngine.Events.UnityAction>("Settings", () => ShowSettings()),
                new KeyValuePair<string, UnityEngine.Events.UnityAction>("Controls", ShowControls),
            };
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                items.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("Quit", game.Quit));
            for (int i = 0; i < items.Count; i++)
            {
                var b = Ui.Button(t, items[i].Key, 36, i == 0 ? Ui.ButtonStyle.Primary : Ui.ButtonStyle.Normal, items[i].Value);
                TopLeft(b, 108f, -380f - i * 96f, 440f, 80f);
            }

            int got = 0;
            foreach (var tr in TrackList.All) got += (int)Records.MedalFor(tr, Records.BestTime(tr.Id));
            var medals = Ui.Label(t, "Medals  " + got + " / " + TrackList.All.Count * 4, 26, Ui.Accent, TextAnchor.LowerLeft);
            Ui.Place(medals.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(110f, 66f), new Vector2(500f, 34f));
            var ver = Ui.Label(t, "v" + GameInfo.Version, 20, Ui.TextDim, TextAnchor.LowerLeft, false);
            Ui.Place(ver.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(110f, 34f), new Vector2(300f, 28f));
        }

        // ------------------------------------------------------------ track select

        public void ShowTracks(int sel)
        {
            HideAll();
            page = Page.Tracks;
            selected = sel;
            var t = pageRoot.transform;
            var tracks = TrackList.All;

            var shade = Ui.Plain(t, "Shade", new Color(0.02f, 0.04f, 0.1f, 0.3f));
            Ui.Fill(shade.rectTransform);
            var back = Ui.Button(t, "Back", 26, Ui.ButtonStyle.Normal, ShowMain);
            TopLeft(back, 60f, -48f, 160f, 60f);
            var head = Ui.Label(t, "Tracks", 56, Color.white, TextAnchor.MiddleLeft);
            TopLeft(head, 250f, -44f, 600f, 70f);
            Ui.AddShadow(head, 4f, 0.4f);

            const float w = 270f, h = 230f, gap = 22f;
            for (int i = 0; i < tracks.Count; i++)
            {
                var card = Card(t, tracks[i], i == selected);
                Ui.Place(card, Vector2.up, Vector2.up, Vector2.up, new Vector2(60f + (i % 4) * (w + gap), -150f - (i / 4) * (h + gap)), new Vector2(w, h));
            }
            Details(t, tracks[Mathf.Clamp(selected, 0, tracks.Count - 1)]);
        }

        RectTransform Card(Transform parent, TrackInfo info, bool picked)
        {
            var theme = Theme.Get(info.ThemeId);
            var bg = Ui.Panel(parent, "Card", Color.white);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = picked ? new Color(0.16f, 0.2f, 0.32f, 0.95f) : new Color(0.07f, 0.09f, 0.16f, 0.82f);
            cb.highlightedColor = new Color(0.2f, 0.25f, 0.38f, 0.95f);
            cb.pressedColor = new Color(0.25f, 0.3f, 0.45f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            int idx = info.Index;
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sound.Click);
                if (idx == selected) game.StartRace(idx);
                else game.Select(idx);
            });
            bg.gameObject.AddComponent<ButtonFx>().HoverScale = 1.05f;
            if (picked) Ui.AddOutline(bg, Ui.Accent, 3f);

            var band = Ui.Panel(bg.transform, "Band", theme.AccentColor, Ui.RoundedSmall);
            band.raycastTarget = false;
            Ui.Place(band.rectTransform, Vector2.up, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-24f, 8f));

            TopLeft(Ui.Label(bg.transform, (info.Index + 1).ToString(), 60, Color.white, TextAnchor.UpperLeft), 18f, -24f, 120f, 70f);
            TopLeft(Ui.Label(bg.transform, info.Name, 24, Ui.TextColor, TextAnchor.UpperLeft), 20f, -102f, 240f, 30f);
            TopLeft(Ui.Label(bg.transform, theme.DisplayName, 18, Ui.TextDim, TextAnchor.UpperLeft, false), 20f, -134f, 240f, 24f);

            var chip = Ui.Panel(bg.transform, "Difficulty", info.DifficultyColor, Ui.RoundedSmall);
            chip.raycastTarget = false;
            Ui.Place(chip.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(18f, 18f), new Vector2(100f, 30f));
            Ui.Fill(Ui.Label(chip.transform, info.DifficultyName, 18, new Color(0.08f, 0.08f, 0.1f)).rectTransform);

            float pb = Records.BestTime(info.Id);
            var time = Ui.Label(bg.transform, pb > 0 ? Records.FormatTime(pb) : "-", 22, pb > 0 ? Ui.TextColor : Ui.TextDim, TextAnchor.LowerRight);
            Ui.Place(time.rectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-18f, 20f), new Vector2(140f, 30f));

            Medal m = Records.MedalFor(info, pb);
            var disc = Ui.Panel(bg.transform, "Medal", Records.MedalColor(m), Ui.Circle);
            disc.raycastTarget = false;
            Ui.Place(disc.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-18f, -30f), new Vector2(38f, 38f));
            return bg.rectTransform;
        }

        void Details(Transform parent, TrackInfo info)
        {
            var theme = Theme.Get(info.ThemeId);
            var panel = Ui.Panel(parent, "Details", Ui.PanelColor);
            Ui.Place(panel.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -150f), new Vector2(600f, 780f));
            var p = panel.transform;

            var band = Ui.Panel(p, "Band", theme.AccentColor, Ui.RoundedSmall);
            Ui.Place(band.rectTransform, Vector2.up, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-28f, 10f));
            TopLeft(Ui.Label(p, "Track " + (info.Index + 1), 24, theme.AccentColor, TextAnchor.UpperLeft), 36f, -42f, 400f, 32f);
            var name = Ui.Label(p, info.Name, 50, Color.white, TextAnchor.UpperLeft);
            TopLeft(name, 34f, -76f, 540f, 64f);
            Ui.AddShadow(name, 3f, 0.35f);

            var chip = Ui.Panel(p, "Difficulty", info.DifficultyColor, Ui.RoundedSmall);
            TopLeft(chip, 36f, -156f, 120f, 34f);
            Ui.Fill(Ui.Label(chip.transform, info.DifficultyName, 20, new Color(0.08f, 0.08f, 0.1f)).rectTransform);
            var facts = Ui.Label(p, theme.DisplayName + "   " + Mathf.RoundToInt(info.Length) + " m   " + info.Checkpoints + " checkpoints", 20, Ui.TextDim, TextAnchor.MiddleLeft, false);
            TopLeft(facts, 172f, -156f, 400f, 34f);

            float pb = Records.BestTime(info.Id);
            TopLeft(Ui.Label(p, "Your best", 22, Ui.TextDim, TextAnchor.UpperLeft, false), 36f, -220f, 400f, 30f);
            TopLeft(Ui.Label(p, pb > 0 ? Records.FormatTime(pb) : "No time yet", 60, pb > 0 ? Color.white : Ui.TextDim, TextAnchor.UpperLeft), 32f, -250f, 540f, 76f);

            TopLeft(Ui.Label(p, "Medals", 22, Ui.TextDim, TextAnchor.UpperLeft, false), 36f, -346f, 400f, 30f);
            Medal[] medals = { Medal.Author, Medal.Gold, Medal.Silver, Medal.Bronze };
            float[] times = { info.AuthorTime, info.GoldTime, info.SilverTime, info.BronzeTime };
            for (int i = 0; i < 4; i++)
            {
                bool got = pb > 0 && pb <= times[i];
                float y = -384f - i * 54f;
                TopLeft(Ui.Panel(p, "Dot", got ? Records.MedalColor(medals[i]) : new Color(1f, 1f, 1f, 0.15f), Ui.Circle), 38f, y, 36f, 36f);
                TopLeft(Ui.Label(p, Records.MedalName(medals[i]), 26, got ? Records.MedalColor(medals[i]) : Ui.TextColor, TextAnchor.MiddleLeft), 92f, y, 240f, 36f);
                var tl = Ui.Label(p, Records.FormatTime(times[i]), 26, Ui.TextColor, TextAnchor.MiddleRight, false);
                Ui.Place(tl.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, y), new Vector2(240f, 36f));
            }

            var go = Ui.Button(p, "Race", 44, Ui.ButtonStyle.Primary, () => game.StartRace(info.Index));
            Ui.Place(Ui.Rect(go.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(530f, 92f));
            var hint = Ui.Label(p, "or press Enter", 18, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(400f, 28f));
        }

        // ------------------------------------------------------------ settings

        public void ShowSettings()
        {
            Clear(overlayRoot);
            overlay = true;
            refresh.Clear();
            var card = OverlayCard("Settings", new Vector2(980f, 950f));

            float y = -112f;
            Row(card, ref y, "Graphics", () => Settings.QualityNames[Settings.Quality], d => Settings.Quality = Mathf.Clamp(Settings.Quality + d, 0, 3));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Row(card, ref y, "Resolution", Settings.ResolutionLabel, d =>
                {
                    var list = Settings.AvailableResolutions();
                    Settings.ResolutionIndex = (Mathf.Max(0, Settings.ResolutionIndex) + d + list.Count) % list.Count;
                    Settings.Apply(true);
                });
                Row(card, ref y, "Fullscreen", () => Settings.Fullscreen ? "On" : "Off", d => { Settings.Fullscreen = !Settings.Fullscreen; Settings.Apply(true); });
            }
            Row(card, ref y, "VSync", () => Settings.VSync ? "On" : "Off", d => Settings.VSync = !Settings.VSync);
            Row(card, ref y, "Volume", () => Percent(Settings.MasterVolume), d => Settings.MasterVolume = Step(Settings.MasterVolume, d));
            Row(card, ref y, "Effects", () => Percent(Settings.EffectsVolume), d => Settings.EffectsVolume = Step(Settings.EffectsVolume, d));
            Row(card, ref y, "Engine", () => Percent(Settings.EngineVolume), d => Settings.EngineVolume = Step(Settings.EngineVolume, d));
            Row(card, ref y, "Camera", () => Settings.CameraNames[Settings.CameraMode], d => Settings.CameraMode = Wrap(Settings.CameraMode + d, Settings.CameraNames.Length));
            Row(card, ref y, "Car colour", () => Settings.CarColorNames[Settings.CarColor], d => Settings.CarColor = Wrap(Settings.CarColor + d, Settings.CarColors.Length), true);
            Row(card, ref y, "Speed", () => Settings.UseMph ? "mph" : "km/h", d => Settings.UseMph = !Settings.UseMph);
            Row(card, ref y, "Ghost", () => Settings.ShowGhost ? "On" : "Off", d => Settings.ShowGhost = !Settings.ShowGhost);
            Row(card, ref y, "FPS counter", () => Settings.ShowFps ? "On" : "Off", d => Settings.ShowFps = !Settings.ShowFps);

            var note = Ui.Label(card, "Graphics changes to trees and terrain show up on the next track you load.", 18, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 124f), new Vector2(900f, 28f));

            bool armed = false;
            Button reset = null;
            reset = Ui.Button(card, "Reset times", 24, Ui.ButtonStyle.Normal, () =>
            {
                var label = Ui.ButtonLabel(reset);
                if (!armed)
                {
                    armed = true;
                    label.text = "Click again to confirm";
                    label.color = Ui.Bad;
                    return;
                }
                Records.ResetAll(TrackList.Ids());
                label.text = "Times cleared";
                label.color = Ui.TextDim;
            });
            Ui.Place(Ui.Rect(reset.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 38f), new Vector2(380f, 66f));
            var done = Ui.Button(card, "Done", 30, Ui.ButtonStyle.Primary, CloseOverlay);
            Ui.Place(Ui.Rect(done.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 38f), new Vector2(380f, 66f));
        }

        static string Percent(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
        static float Step(float v, int d) { return Mathf.Clamp01(Mathf.Round((v + d * 0.1f) * 10f) / 10f); }
        static int Wrap(int v, int n) { return (v % n + n) % n; }

        Transform OverlayCard(string heading, Vector2 size)
        {
            var dim = Ui.Plain(overlayRoot.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.65f));
            dim.raycastTarget = true;
            Ui.Fill(dim.rectTransform);
            var card = Ui.Panel(overlayRoot.transform, "Card", Ui.PanelColor);
            Ui.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var t = Ui.Label(card.transform, heading, 52, Color.white);
            Ui.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(600f, 68f));
            return card.transform;
        }

        void Row(Transform parent, ref float y, string label, Func<string> value, Action<int> change, bool swatch = false)
        {
            var row = Ui.Panel(parent, "Row", refresh.Count % 2 == 0 ? Ui.PanelLight : new Color(1f, 1f, 1f, 0.03f));
            row.raycastTarget = false;
            Ui.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(880f, 50f));
            var l = Ui.Label(row.transform, label, 24, Ui.TextColor, TextAnchor.MiddleLeft, false);
            Ui.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(400f, 40f));
            var v = Ui.Label(row.transform, value(), 24, Ui.Accent);
            Ui.Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(290f, 40f));

            Image dot = null;
            if (swatch)
            {
                dot = Ui.Panel(row.transform, "Swatch", Settings.CarColors[Settings.CarColor], Ui.Circle);
                Ui.Place(dot.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(28f, 28f));
            }
            refresh.Add(() =>
            {
                v.text = value();
                if (dot != null) dot.color = Settings.CarColors[Settings.CarColor];
            });

            var minus = Ui.Button(row.transform, "<", 28, Ui.ButtonStyle.Normal, () => { change(-1); Changed(); });
            Ui.Place(Ui.Rect(minus.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-370f, 0f), new Vector2(50f, 38f));
            var plus = Ui.Button(row.transform, ">", 28, Ui.ButtonStyle.Normal, () => { change(1); Changed(); });
            Ui.Place(Ui.Rect(plus.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(50f, 38f));
            y -= 56f;
        }

        void Changed()
        {
            game.SettingsChanged();
            foreach (var r in refresh) r();
        }

        void ShowControls()
        {
            Clear(overlayRoot);
            overlay = true;
            var card = OverlayCard("Controls", new Vector2(880f, 800f));
            string[,] rows =
            {
                { "W / Up", "Accelerate" },
                { "S / Down", "Brake, reverse" },
                { "A D / Left Right", "Steer" },
                { "Space / Shift", "Drift" },
                { "Enter", "Back to last checkpoint" },
                { "R", "Restart" },
                { "C", "Change camera" },
                { "Esc", "Pause" },
                { "Gamepad", "Stick, B drift, X checkpoint, Y restart" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -112f - i * 62f;
                var key = Ui.Panel(card, "Key", new Color(1f, 1f, 1f, 0.1f), Ui.RoundedSmall);
                Ui.Place(key.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-20f, y), new Vector2(320f, 48f));
                Ui.Fill(Ui.Label(key.transform, rows[i, 0], 24, Ui.Accent).rectTransform);
                var a = Ui.Label(card, rows[i, 1], 24, Ui.TextColor, TextAnchor.MiddleLeft, false);
                Ui.Place(a.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(420f, 48f));
            }
            var back = Ui.Button(card, "Back", 30, Ui.ButtonStyle.Primary, CloseOverlay);
            Ui.Place(Ui.Rect(back.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(340f, 66f));
        }

        void CloseOverlay()
        {
            Clear(overlayRoot);
            overlay = false;
            overlayClosedFrame = Time.frameCount;
            if (page == Page.Main) ShowMain();
        }

        // ------------------------------------------------------------ loading screen

        void BuildLoading()
        {
            loading = Ui.Node("Loading", canvas);
            Ui.Fill(Ui.Rect(loading));
            var bg = Ui.Plain(loading.transform, "Bg", new Color(0.05f, 0.08f, 0.15f, 1f));
            bg.raycastTarget = true;
            Ui.Fill(bg.rectTransform);
            loadingName = Ui.Label(loading.transform, "", 70, Color.white);
            Ui.Place(loadingName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 100f));
            loadingInfo = Ui.Label(loading.transform, "", 28, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(loadingInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1400f, 40f));
            var sp = Ui.Panel(loading.transform, "Spinner", Ui.Accent, Ui.RoundedSmall);
            spinner = sp.rectTransform;
            Ui.Place(spinner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(42f, 42f));
            loading.SetActive(false);
        }

        public void ShowLoading(TrackInfo info)
        {
            loading.transform.SetAsLastSibling();
            loading.SetActive(true);
            loadingName.text = info.Name;
            loadingInfo.text = "Track " + (info.Index + 1) + "  ·  " + Theme.Get(info.ThemeId).DisplayName + "  ·  " + info.DifficultyName;
        }

        public void HideLoading() { loading.SetActive(false); }

        void Update()
        {
            if (loading.activeSelf)
            {
                spinner.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 300f);
                return;
            }
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (overlay) CloseOverlay();
                else if (page == Page.Tracks) ShowMain();
            }
            else if (enter && !overlay)
            {
                if (page == Page.Tracks && !game.Loading) game.StartRace(selected);
                else if (page == Page.Main) game.ShowTracks();
            }
        }
    }
}
