using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PolyRush
{
    /// <summary>Start page, track selection, settings, controls and the loading screen.</summary>
    public class MenuUI : MonoBehaviour
    {
        enum Screen { None, Main, Tracks, Settings, Controls }

        GameManager game;
        Transform canvas;
        GameObject screenRoot, overlayRoot, loadingRoot;
        Screen current = Screen.None;
        Screen overlay = Screen.None;
        int selected;
        Text loadingTitle, loadingSub;
        RectTransform spinner;
        readonly List<Action> rowRefreshers = new List<Action>();
        bool confirmReset;

        int overlayClosedFrame = -1;

        /// <summary>True while settings/controls are open (or were closed this frame, so Esc is not handled twice).</summary>
        public bool OverlayOpen { get { return overlay != Screen.None || overlayClosedFrame == Time.frameCount; } }

        public void Build(Transform canvas, GameManager game)
        {
            this.canvas = canvas;
            this.game = game;
            screenRoot = UIKit.Node("Menu", canvas);
            UIKit.Fill(UIKit.Rect(screenRoot));
            overlayRoot = UIKit.Node("Overlay", canvas);
            UIKit.Fill(UIKit.Rect(overlayRoot));
            BuildLoading();
        }

        void Clear(GameObject root)
        {
            foreach (Transform c in root.transform) Destroy(c.gameObject);
        }

        public void HideAll()
        {
            Clear(screenRoot);
            Clear(overlayRoot);
            current = Screen.None;
            overlay = Screen.None;
        }

        // ------------------------------------------------------------------ main menu

        public void ShowMain()
        {
            HideAll();
            current = Screen.Main;
            var t = screenRoot.transform;

            var side = UIKit.Plain(t, "Side", new Color(0.02f, 0.04f, 0.1f, 0.42f));
            UIKit.Place(side.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(760f, 0f));

            var title = UIKit.Label(t, "POLY<color=#FFB829>RUSH</color>", 132, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -150f), new Vector2(900f, 150f));
            title.text = GameInfo.Title.StartsWith("POLY") ? "POLY<color=#FFB829>" + GameInfo.Title.Substring(4) + "</color>" : GameInfo.Title;
            UIKit.AddShadow(title, 6f, 0.35f);

            var bar = UIKit.Plain(t, "Bar", UIKit.Accent);
            UIKit.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -308f), new Vector2(70f, 8f));
            var sub = UIKit.Label(t, GameInfo.Subtitle, 34, UIKit.TextColor, TextAnchor.MiddleLeft);
            UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -292f), new Vector2(600f, 40f));
            UIKit.AddShadow(sub);

            var items = new List<KeyValuePair<string, UnityEngine.Events.UnityAction>>();
            items.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("PLAY", () => game.ShowTrackSelect()));
            items.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("SETTINGS", () => ShowSettings(false)));
            items.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("CONTROLS", ShowControls));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                items.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("QUIT", () => game.Quit()));
            for (int i = 0; i < items.Count; i++)
            {
                var b = UIKit.Button(t, items[i].Key, 36, i == 0 ? UIKit.ButtonStyle.Primary : UIKit.ButtonStyle.Normal, items[i].Value);
                UIKit.Place(UIKit.Rect(b.gameObject), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -400f - i * 98f), new Vector2(470f, 82f));
                UIKit.ButtonLabel(b).alignment = TextAnchor.MiddleCenter;
            }

            int got = 0, total = 0;
            foreach (var tr in TrackLibrary.All)
            {
                total += 4;
                got += (int)Records.MedalFor(tr, Records.BestTime(tr.Id));
            }
            var medals = UIKit.Label(t, "MEDALS  " + got + " / " + total, 26, UIKit.Accent, TextAnchor.LowerLeft);
            UIKit.Place(medals.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(112f, 70f), new Vector2(600f, 36f));
            var ver = UIKit.Label(t, "v" + GameInfo.Version + "   |   " + TrackLibrary.All.Count + " SUMMER TRACKS   |   TIME TRIAL", 20, UIKit.TextDim, TextAnchor.LowerLeft, false);
            UIKit.Place(ver.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(112f, 36f), new Vector2(800f, 30f));

            if (game.World != null)
            {
                var now = UIKit.Label(t, "NOW SHOWING  -  " + game.World.Info.Name.ToUpperInvariant(), 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerRight);
                UIKit.Place(now.rectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-40f, 36f), new Vector2(800f, 30f));
                UIKit.AddShadow(now, 2f, 0.6f);
            }
        }

        // ------------------------------------------------------------------ track select

        public void ShowTrackSelect(int sel)
        {
            HideAll();
            current = Screen.Tracks;
            selected = sel;
            var t = screenRoot.transform;
            var tracks = TrackLibrary.All;

            var shade = UIKit.Plain(t, "Shade", new Color(0.02f, 0.04f, 0.1f, 0.35f));
            UIKit.Fill(shade.rectTransform);

            var back = UIKit.Button(t, "<  BACK", 26, UIKit.ButtonStyle.Normal, () => ShowMain());
            UIKit.Place(UIKit.Rect(back.gameObject), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(200f, 62f));
            var title = UIKit.Label(t, "SUMMER TRACKS", 58, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, -46f), new Vector2(800f, 70f));
            UIKit.AddShadow(title, 4f, 0.4f);

            // cards
            const float cw = 270f, ch = 236f, gap = 22f;
            for (int i = 0; i < tracks.Count; i++)
            {
                int col = i % 4, row = i / 4;
                var card = BuildCard(t, tracks[i], i == selected);
                UIKit.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(60f + col * (cw + gap), -160f - row * (ch + gap)), new Vector2(cw, ch));
            }

            BuildDetails(t, tracks[Mathf.Clamp(selected, 0, tracks.Count - 1)]);
        }

        RectTransform BuildCard(Transform parent, TrackInfo info, bool isSelected)
        {
            var theme = Theme.Get(info.ThemeId);
            var bg = UIKit.Panel(parent, "Card" + info.Index, Color.white);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = isSelected ? new Color(0.16f, 0.2f, 0.32f, 0.95f) : new Color(0.07f, 0.09f, 0.16f, 0.82f);
            colors.highlightedColor = new Color(0.2f, 0.25f, 0.38f, 0.95f);
            colors.pressedColor = new Color(0.25f, 0.3f, 0.45f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            int idx = info.Index;
            btn.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
            btn.onClick.AddListener(() =>
            {
                if (idx == selected) game.StartRace(idx);
                else game.SelectTrack(idx);
            });
            bg.gameObject.AddComponent<ButtonFx>().HoverScale = 1.05f;
            if (isSelected) UIKit.AddOutline(bg, UIKit.Accent, 3f);

            var band = UIKit.Panel(bg.transform, "Band", theme.AccentColor, UIKit.RoundedSmall);
            band.raycastTarget = false;
            UIKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-24f, 8f));

            var num = UIKit.Label(bg.transform, (info.Index + 1).ToString("00"), 60, Color.white, TextAnchor.UpperLeft);
            UIKit.Place(num.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -26f), new Vector2(200f, 70f));
            var name = UIKit.Label(bg.transform, info.Name.ToUpperInvariant(), 23, UIKit.TextColor, TextAnchor.UpperLeft);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -104f), new Vector2(240f, 30f));
            var themeL = UIKit.Label(bg.transform, theme.DisplayName.ToUpperInvariant(), 17, UIKit.TextDim, TextAnchor.UpperLeft, false);
            UIKit.Place(themeL.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -136f), new Vector2(240f, 24f));

            var chip = UIKit.Panel(bg.transform, "Diff", info.DifficultyColor, UIKit.RoundedSmall);
            chip.raycastTarget = false;
            UIKit.Place(chip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(104f, 32f));
            var dl = UIKit.Label(chip.transform, info.DifficultyName, 17, new Color(0.08f, 0.08f, 0.1f));
            UIKit.Fill(dl.rectTransform);

            float best = Records.BestTime(info.Id);
            var bt = UIKit.Label(bg.transform, best > 0 ? Records.FormatTime(best) : "--:--.---", 22, best > 0 ? UIKit.TextColor : UIKit.TextDim, TextAnchor.LowerRight);
            UIKit.Place(bt.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 20f), new Vector2(140f, 30f));

            Medal m = Records.MedalFor(info, best);
            var disc = UIKit.Panel(bg.transform, "Medal", Records.MedalColor(m), UIKit.Circle);
            disc.raycastTarget = false;
            UIKit.Place(disc.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -32f), new Vector2(40f, 40f));
            if (m != Medal.None) UIKit.AddShadow(disc, 2f, 0.4f);
            return bg.rectTransform;
        }

        void BuildDetails(Transform parent, TrackInfo info)
        {
            var theme = Theme.Get(info.ThemeId);
            var panel = UIKit.Panel(parent, "Details", UIKit.PanelColor);
            UIKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -160f), new Vector2(620f, 800f));
            var p = panel.transform;

            var band = UIKit.Panel(p, "Band", theme.AccentColor, UIKit.RoundedSmall);
            UIKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-28f, 10f));

            var num = UIKit.Label(p, "TRACK " + (info.Index + 1).ToString("00"), 26, theme.AccentColor, TextAnchor.UpperLeft);
            UIKit.Place(num.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -44f), new Vector2(500f, 34f));
            var name = UIKit.Label(p, info.Name.ToUpperInvariant(), 50, Color.white, TextAnchor.UpperLeft);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -80f), new Vector2(560f, 64f));
            UIKit.AddShadow(name, 3f, 0.35f);

            var chip = UIKit.Panel(p, "Diff", info.DifficultyColor, UIKit.RoundedSmall);
            UIKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -160f), new Vector2(130f, 36f));
            var dl = UIKit.Label(chip.transform, info.DifficultyName, 20, new Color(0.08f, 0.08f, 0.1f));
            UIKit.Fill(dl.rectTransform);
            var stats = UIKit.Label(p, theme.DisplayName.ToUpperInvariant() + "   |   " + (info.Length / 1000f).ToString("0.00") + " KM   |   " + info.CheckpointCount + " CHECKPOINTS", 20, UIKit.TextDim, TextAnchor.MiddleLeft, false);
            UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(184f, -160f), new Vector2(420f, 36f));

            float best = Records.BestTime(info.Id);
            var pbL = UIKit.Label(p, "PERSONAL BEST", 22, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Place(pbL.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -228f), new Vector2(500f, 30f));
            var pb = UIKit.Label(p, best > 0 ? Records.FormatTime(best) : "NOT SET", 64, best > 0 ? Color.white : UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Place(pb.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -258f), new Vector2(560f, 80f));

            var mt = UIKit.Label(p, "MEDALS", 22, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Place(mt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -360f), new Vector2(500f, 30f));
            Medal[] medals = { Medal.Author, Medal.Gold, Medal.Silver, Medal.Bronze };
            float[] times = { info.AuthorTime, info.GoldTime, info.SilverTime, info.BronzeTime };
            for (int i = 0; i < 4; i++)
            {
                bool got = best > 0 && best <= times[i];
                float y = -400f - i * 58f;
                var disc = UIKit.Panel(p, "M", got ? Records.MedalColor(medals[i]) : new Color(1f, 1f, 1f, 0.15f), UIKit.Circle);
                UIKit.Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, y), new Vector2(40f, 40f));
                var ml = UIKit.Label(p, Records.MedalName(medals[i]), 26, got ? Records.MedalColor(medals[i]) : UIKit.TextColor, TextAnchor.MiddleLeft);
                UIKit.Place(ml.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, y), new Vector2(240f, 40f));
                var tl = UIKit.Label(p, Records.FormatTime(times[i]), 26, UIKit.TextColor, TextAnchor.MiddleRight, false);
                UIKit.Place(tl.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, y), new Vector2(240f, 40f));
            }

            var race = UIKit.Button(p, "RACE!", 44, UIKit.ButtonStyle.Primary, () => game.StartRace(info.Index));
            UIKit.Place(UIKit.Rect(race.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(548f, 96f));
            var hint = UIKit.Label(p, "PRESS ENTER TO RACE", 18, UIKit.TextDim);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(500f, 30f));
        }

        // ------------------------------------------------------------------ settings

        public void ShowSettings(bool fromPause)
        {
            Clear(overlayRoot);
            overlay = Screen.Settings;
            confirmReset = false;
            rowRefreshers.Clear();
            var t = overlayRoot.transform;

            var dim = UIKit.Plain(t, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.65f));
            dim.raycastTarget = true;
            UIKit.Fill(dim.rectTransform);
            var card = UIKit.Panel(t, "Card", UIKit.PanelColor);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 960f));
            var c = card.transform;
            var title = UIKit.Label(c, "SETTINGS", 54, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 70f));

            float y = -118f;
            const float step = 56f;
            Row(c, ref y, step, "GRAPHICS QUALITY", () => GameSettings.QualityNames[GameSettings.Quality],
                d => GameSettings.Quality = Mathf.Clamp(GameSettings.Quality + d, 0, 3));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Row(c, ref y, step, "RESOLUTION", GameSettings.ResolutionLabel, d =>
                {
                    var list = GameSettings.AvailableResolutions();
                    int i = GameSettings.ResolutionIndex;
                    if (i < 0) i = 0;
                    GameSettings.ResolutionIndex = (i + d + list.Count) % list.Count;
                    GameSettings.Apply(true);
                });
                Row(c, ref y, step, "FULLSCREEN", () => GameSettings.Fullscreen ? "ON" : "OFF", d =>
                {
                    GameSettings.Fullscreen = !GameSettings.Fullscreen;
                    GameSettings.Apply(true);
                });
            }
            Row(c, ref y, step, "VSYNC", () => GameSettings.VSync ? "ON" : "OFF", d => GameSettings.VSync = !GameSettings.VSync);
            Row(c, ref y, step, "MASTER VOLUME", () => Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%", d => GameSettings.MasterVolume = Step(GameSettings.MasterVolume, d));
            Row(c, ref y, step, "EFFECTS VOLUME", () => Mathf.RoundToInt(GameSettings.EffectsVolume * 100) + "%", d => GameSettings.EffectsVolume = Step(GameSettings.EffectsVolume, d));
            Row(c, ref y, step, "ENGINE VOLUME", () => Mathf.RoundToInt(GameSettings.EngineVolume * 100) + "%", d => GameSettings.EngineVolume = Step(GameSettings.EngineVolume, d));
            Row(c, ref y, step, "CAMERA", () => GameSettings.CameraNames[GameSettings.CameraMode],
                d => GameSettings.CameraMode = (GameSettings.CameraMode + d + GameSettings.CameraNames.Length) % GameSettings.CameraNames.Length);
            Row(c, ref y, step, "CAR COLOUR", () => GameSettings.CarColorNames[GameSettings.CarColor],
                d => GameSettings.CarColor = (GameSettings.CarColor + d + GameSettings.CarColors.Length) % GameSettings.CarColors.Length, true);
            Row(c, ref y, step, "SPEED UNITS", () => GameSettings.UseMph ? "MPH" : "KM/H", d => GameSettings.UseMph = !GameSettings.UseMph);
            Row(c, ref y, step, "SHOW GHOST", () => GameSettings.ShowGhost ? "ON" : "OFF", d => GameSettings.ShowGhost = !GameSettings.ShowGhost);
            Row(c, ref y, step, "SHOW FPS", () => GameSettings.ShowFps ? "ON" : "OFF", d => GameSettings.ShowFps = !GameSettings.ShowFps);

            var note = UIKit.Label(c, "Quality changes to terrain and trees apply when the next track loads.  Car colour applies on the next race.", 17, UIKit.TextDim, TextAnchor.MiddleCenter, false);
            UIKit.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(900f, 30f));

            Button reset = null;
            reset = UIKit.Button(c, "RESET RECORDS", 24, UIKit.ButtonStyle.Normal, () =>
            {
                if (!confirmReset)
                {
                    confirmReset = true;
                    UIKit.ButtonLabel(reset).text = "CLICK AGAIN TO CONFIRM";
                    UIKit.ButtonLabel(reset).color = UIKit.Bad;
                    return;
                }
                Records.ResetAll(TrackLibrary.Ids());
                UIKit.ButtonLabel(reset).text = "RECORDS CLEARED";
                UIKit.ButtonLabel(reset).color = UIKit.TextDim;
            });
            UIKit.Place(UIKit.Rect(reset.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 40f), new Vector2(400f, 70f));
            var back = UIKit.Button(c, "DONE", 30, UIKit.ButtonStyle.Primary, CloseOverlay);
            UIKit.Place(UIKit.Rect(back.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 40f), new Vector2(400f, 70f));
        }

        static float Step(float v, int d)
        {
            return Mathf.Clamp01(Mathf.Round((v + d * 0.1f) * 10f) / 10f);
        }

        void Row(Transform parent, ref float y, float step, string label, Func<string> value, Action<int> change, bool swatch = false)
        {
            var row = UIKit.Panel(parent, "Row", rowRefreshers.Count % 2 == 0 ? UIKit.PanelLight : new Color(1f, 1f, 1f, 0.03f));
            row.raycastTarget = false;
            UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, step - 8f));
            var l = UIKit.Label(row.transform, label, 24, UIKit.TextColor, TextAnchor.MiddleLeft);
            UIKit.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(400f, 40f));

            var v = UIKit.Label(row.transform, value(), 24, UIKit.Accent);
            UIKit.Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(300f, 40f));
            Image sw = null;
            if (swatch)
            {
                sw = UIKit.Panel(row.transform, "Swatch", GameSettings.CarColors[GameSettings.CarColor], UIKit.Circle);
                UIKit.Place(sw.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-400f, 0f), new Vector2(30f, 30f));
            }
            Action refresh = () =>
            {
                v.text = value();
                if (sw != null) sw.color = GameSettings.CarColors[GameSettings.CarColor];
            };
            rowRefreshers.Add(refresh);

            var left = UIKit.Button(row.transform, "<", 28, UIKit.ButtonStyle.Normal, () => { change(-1); AfterChange(); });
            UIKit.Place(UIKit.Rect(left.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, 0f), new Vector2(52f, 40f));
            var right = UIKit.Button(row.transform, ">", 28, UIKit.ButtonStyle.Normal, () => { change(1); AfterChange(); });
            UIKit.Place(UIKit.Rect(right.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(52f, 40f));
            if (swatch) UIKit.Rect(sw.gameObject).anchoredPosition = new Vector2(-330f, 0f);
            y -= step;
        }

        void AfterChange()
        {
            game.OnSettingsChanged();
            foreach (var r in rowRefreshers) r();
        }

        // ------------------------------------------------------------------ controls

        void ShowControls()
        {
            Clear(overlayRoot);
            overlay = Screen.Controls;
            var t = overlayRoot.transform;
            var dim = UIKit.Plain(t, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.65f));
            dim.raycastTarget = true;
            UIKit.Fill(dim.rectTransform);
            var card = UIKit.Panel(t, "Card", UIKit.PanelColor);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 820f));
            var c = card.transform;
            var title = UIKit.Label(c, "CONTROLS", 54, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 70f));
            string[,] rows =
            {
                { "W  /  UP", "ACCELERATE" },
                { "S  /  DOWN", "BRAKE  /  REVERSE" },
                { "A D  /  LEFT RIGHT", "STEER" },
                { "SPACE  /  SHIFT", "DRIFT" },
                { "ENTER", "RESPAWN AT LAST CHECKPOINT" },
                { "R  /  BACKSPACE", "RESTART RUN" },
                { "C", "CHANGE CAMERA" },
                { "ESC  /  P", "PAUSE" },
                { "GAMEPAD", "LEFT STICK + A / B / X / Y" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -120f - i * 64f;
                var keyBg = UIKit.Panel(c, "Key", new Color(1f, 1f, 1f, 0.1f), UIKit.RoundedSmall);
                UIKit.Place(keyBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-20f, y), new Vector2(330f, 50f));
                var k = UIKit.Label(keyBg.transform, rows[i, 0], 24, UIKit.Accent);
                UIKit.Fill(k.rectTransform);
                var a = UIKit.Label(c, rows[i, 1], 24, UIKit.TextColor, TextAnchor.MiddleLeft);
                UIKit.Place(a.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(400f, 50f));
            }
            var back = UIKit.Button(c, "BACK", 30, UIKit.ButtonStyle.Primary, CloseOverlay);
            UIKit.Place(UIKit.Rect(back.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 70f));
        }

        void CloseOverlay()
        {
            Clear(overlayRoot);
            overlay = Screen.None;
            overlayClosedFrame = Time.frameCount;
            if (current == Screen.Main) ShowMain();
        }

        // ------------------------------------------------------------------ loading

        void BuildLoading()
        {
            loadingRoot = UIKit.Node("Loading", canvas);
            UIKit.Fill(UIKit.Rect(loadingRoot));
            var bg = UIKit.Plain(loadingRoot.transform, "Bg", new Color(0.05f, 0.08f, 0.15f, 1f));
            bg.raycastTarget = true;
            UIKit.Fill(bg.rectTransform);
            var glow = UIKit.Plain(loadingRoot.transform, "Glow", new Color(1f, 0.6f, 0.2f, 0.12f));
            glow.sprite = UIKit.Gradient;
            // gradient sprite is opaque at the top; flip it so the glow rises from the bottom edge
            UIKit.Place(glow.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(0f, 500f));
            glow.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            loadingTitle = UIKit.Label(loadingRoot.transform, "", 72, Color.white);
            UIKit.Place(loadingTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 100f));
            UIKit.AddShadow(loadingTitle, 4f, 0.4f);
            loadingSub = UIKit.Label(loadingRoot.transform, "", 28, UIKit.TextDim);
            UIKit.Place(loadingSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1400f, 40f));

            var sp = UIKit.Panel(loadingRoot.transform, "Spinner", UIKit.Accent, UIKit.RoundedSmall);
            spinner = sp.rectTransform;
            UIKit.Place(spinner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(46f, 46f));

            var tip = UIKit.Label(loadingRoot.transform, "TIP:  PRESS ENTER TO RESPAWN AT YOUR LAST CHECKPOINT - YOU KEEP YOUR SPEED.", 20, UIKit.TextDim, TextAnchor.MiddleCenter, false);
            UIKit.Place(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400f, 30f));
            loadingRoot.SetActive(false);
        }

        public void ShowLoading(TrackInfo info)
        {
            loadingRoot.transform.SetAsLastSibling();
            loadingRoot.SetActive(true);
            loadingTitle.text = info.Name.ToUpperInvariant();
            loadingSub.text = "TRACK " + (info.Index + 1).ToString("00") + "   |   " + Theme.Get(info.ThemeId).DisplayName.ToUpperInvariant() + "   |   " + info.DifficultyName;
        }

        public void HideLoading()
        {
            loadingRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (loadingRoot.activeSelf && spinner != null)
            {
                spinner.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 360f);
                float s = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f;
                spinner.localScale = new Vector3(s, s, 1f);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (overlay != Screen.None) CloseOverlay();
                else if (current == Screen.Tracks) ShowMain();
                return;
            }
            if (current == Screen.Tracks && overlay == Screen.None && !game.IsLoading &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                game.StartRace(selected);
            if (current == Screen.Main && overlay == Screen.None && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                game.ShowTrackSelect();
        }
    }
}
