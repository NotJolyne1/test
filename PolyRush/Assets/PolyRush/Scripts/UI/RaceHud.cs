using UnityEngine;
using UnityEngine.UI;

namespace PolyRush
{
    /// <summary>In-race HUD: timer, checkpoints and splits, speedometer, countdown, pause and results.</summary>
    public class RaceHud : MonoBehaviour
    {
        GameManager game;
        RaceController race;
        TrackInfo info;

        GameObject root;
        Text timer, cpText, splitText, trackName, pbText, targetText, speedText, unitText, countdownText, flashText, fpsText;
        Image splitBg, speedBar, boostGlow;
        GameObject pausePanel, finishPanel;
        Transform finishContent;

        float countdownAnim = 10f;
        float splitTimer, flashTimer;
        float fpsAccum;
        int fpsFrames;

        public void Build(Transform canvas, GameManager game)
        {
            this.game = game;
            root = UIKit.Node("HUD", canvas);
            UIKit.Fill(UIKit.Rect(root));

            // timer
            var pill = UIKit.Panel(root.transform, "TimerBg", new Color(0.05f, 0.07f, 0.13f, 0.72f));
            UIKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 104f));
            timer = UIKit.Label(pill.transform, "0:00.000", 66, UIKit.TextColor);
            UIKit.Fill(timer.rectTransform);
            UIKit.AddShadow(timer, 2f, 0.4f);

            cpText = UIKit.Label(root.transform, "", 26, UIKit.TextDim);
            UIKit.Place(cpText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(500f, 40f));
            UIKit.AddShadow(cpText);

            splitBg = UIKit.Panel(root.transform, "SplitBg", new Color(0.05f, 0.07f, 0.13f, 0.7f));
            UIKit.Place(splitBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(330f, 60f));
            splitText = UIKit.Label(splitBg.transform, "", 36, UIKit.Good);
            UIKit.Fill(splitText.rectTransform);
            splitBg.gameObject.SetActive(false);

            // track info (top left)
            var infoBg = UIKit.Panel(root.transform, "InfoBg", new Color(0.05f, 0.07f, 0.13f, 0.6f));
            UIKit.Place(infoBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -26f), new Vector2(440f, 132f));
            trackName = UIKit.Label(infoBg.transform, "", 30, UIKit.TextColor, TextAnchor.UpperLeft);
            UIKit.Place(trackName.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(0f, 0f), Vector2.zero);
            trackName.rectTransform.offsetMin = new Vector2(22f, 0f);
            trackName.rectTransform.offsetMax = new Vector2(-16f, -14f);
            pbText = UIKit.Label(infoBg.transform, "", 24, UIKit.TextDim, TextAnchor.UpperLeft, false);
            UIKit.Fill(pbText.rectTransform);
            pbText.rectTransform.offsetMin = new Vector2(22f, 0f);
            pbText.rectTransform.offsetMax = new Vector2(-16f, -56f);
            targetText = UIKit.Label(infoBg.transform, "", 22, UIKit.TextDim, TextAnchor.UpperLeft, false);
            UIKit.Fill(targetText.rectTransform);
            targetText.rectTransform.offsetMin = new Vector2(22f, 0f);
            targetText.rectTransform.offsetMax = new Vector2(-16f, -90f);

            // speedometer (bottom right)
            var spBg = UIKit.Panel(root.transform, "SpeedBg", new Color(0.05f, 0.07f, 0.13f, 0.6f));
            UIKit.Place(spBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(320f, 150f));
            boostGlow = UIKit.Panel(spBg.transform, "BoostGlow", new Color(1f, 0.55f, 0.1f, 0f));
            UIKit.Fill(boostGlow.rectTransform);
            speedText = UIKit.Label(spBg.transform, "0", 92, UIKit.TextColor, TextAnchor.MiddleRight);
            UIKit.Place(speedText.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0.5f), new Vector2(0f, 0f), Vector2.zero);
            speedText.rectTransform.offsetMin = new Vector2(10f, 30f);
            speedText.rectTransform.offsetMax = new Vector2(-120f, -8f);
            UIKit.AddShadow(speedText, 3f, 0.35f);
            unitText = UIKit.Label(spBg.transform, "KM/H", 26, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Fill(unitText.rectTransform);
            unitText.rectTransform.offsetMin = new Vector2(208f, 30f);
            unitText.rectTransform.offsetMax = new Vector2(-10f, -20f);
            var barBg = UIKit.Plain(spBg.transform, "BarBg", new Color(1f, 1f, 1f, 0.12f));
            UIKit.Place(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 22f), new Vector2(-48f, 8f));
            barBg.rectTransform.offsetMin = new Vector2(24f, 22f);
            barBg.rectTransform.offsetMax = new Vector2(-24f, 30f);
            speedBar = UIKit.Plain(barBg.transform, "Bar", UIKit.Accent);
            UIKit.Place(speedBar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f));

            // hints
            var hints = UIKit.Label(root.transform, "R  RESTART      ENTER  CHECKPOINT      C  CAMERA      SPACE  DRIFT      ESC  PAUSE", 19, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft, false);
            UIKit.Place(hints.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 28f), new Vector2(1000f, 30f));
            UIKit.AddShadow(hints, 2f, 0.5f);

            // countdown + flash
            countdownText = UIKit.Label(root.transform, "", 170, Color.white);
            UIKit.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(600f, 240f));
            UIKit.AddOutline(countdownText, new Color(0f, 0f, 0f, 0.35f), 4f);
            UIKit.AddShadow(countdownText, 6f, 0.3f);

            flashText = UIKit.Label(root.transform, "", 40, UIKit.Bad);
            UIKit.Place(flashText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 60f));
            UIKit.AddOutline(flashText, new Color(0f, 0f, 0f, 0.5f), 2f);

            fpsText = UIKit.Label(canvas, "", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperRight, false);
            UIKit.Place(fpsText.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12f, -8f), new Vector2(200f, 26f));

            BuildPause();
            BuildFinishPanel();
            root.SetActive(false);
        }

        void BuildPause()
        {
            pausePanel = UIKit.Node("Pause", root.transform);
            UIKit.Fill(UIKit.Rect(pausePanel));
            var dim = UIKit.Plain(pausePanel.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.6f));
            dim.raycastTarget = true;
            UIKit.Fill(dim.rectTransform);
            var card = UIKit.Panel(pausePanel.transform, "Card", UIKit.PanelColor);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 600f));
            var title = UIKit.Label(card.transform, "PAUSED", 60, UIKit.TextColor);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(500f, 80f));
            string[] labels = { "RESUME", "RESTART", "SETTINGS", "TRACKS", "MAIN MENU" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = UIKit.Button(card.transform, labels[i], 30, i == 0 ? UIKit.ButtonStyle.Primary : UIKit.ButtonStyle.Normal, () => OnPauseButton(idx));
                UIKit.Place(UIKit.Rect(b.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f - i * 86f), new Vector2(420f, 70f));
            }
            pausePanel.SetActive(false);
        }

        void OnPauseButton(int i)
        {
            switch (i)
            {
                case 0: game.ResumeRace(); break;
                case 1: game.RestartRace(); break;
                case 2: game.OpenSettingsFromPause(); break;
                case 3: game.ExitToTrackSelect(); break;
                case 4: game.ShowMainMenu(); break;
            }
        }

        void BuildFinishPanel()
        {
            finishPanel = UIKit.Node("Finish", root.transform);
            UIKit.Fill(UIKit.Rect(finishPanel));
            var dim = UIKit.Plain(finishPanel.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.35f));
            dim.raycastTarget = true;
            UIKit.Fill(dim.rectTransform);
            var card = UIKit.Panel(finishPanel.transform, "Card", UIKit.PanelColor);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(760f, 620f));
            finishContent = card.transform;
            finishPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        public void Show(bool show)
        {
            root.SetActive(show);
            if (!show)
            {
                race = null;
                pausePanel.SetActive(false);
                finishPanel.SetActive(false);
            }
        }

        public void Bind(RaceController race, TrackInfo info)
        {
            this.race = race;
            this.info = info;
            trackName.text = (info.Index + 1).ToString("00") + "  " + info.Name.ToUpperInvariant();
            RefreshInfo();
        }

        void RefreshInfo()
        {
            float best = Records.BestTime(info.Id);
            pbText.text = "PERSONAL BEST  " + (best > 0 ? Records.FormatTime(best) : "-:--.---");
            Medal m = Records.MedalFor(info, best);
            if (m == Medal.Author)
            {
                targetText.text = "<color=#59F2BF>ALL MEDALS EARNED</color>";
                return;
            }
            Medal next = (Medal)((int)m + 1);
            float t = next == Medal.Bronze ? info.BronzeTime : next == Medal.Silver ? info.SilverTime : next == Medal.Gold ? info.GoldTime : info.AuthorTime;
            targetText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Records.MedalColor(next)) + ">" + Records.MedalName(next) + "</color>  " + Records.FormatTime(t);
        }

        public void OnRestart()
        {
            finishPanel.SetActive(false);
            pausePanel.SetActive(false);
            splitBg.gameObject.SetActive(false);
            flashText.text = "";
            countdownText.text = "";
            countdownAnim = 10f;
            RefreshInfo();
        }

        public void ShowCountdown(string text, bool go)
        {
            countdownText.text = text;
            countdownText.color = go ? UIKit.Good : Color.white;
            countdownAnim = 0f;
        }

        public void OnCheckpoint(int taken, int total, float time, float? delta)
        {
            splitTimer = 2.6f;
            splitBg.gameObject.SetActive(true);
            if (delta.HasValue)
            {
                splitText.text = Records.FormatDelta(delta.Value);
                splitText.color = delta.Value <= 0f ? new Color(0.4f, 0.75f, 1f) : UIKit.Bad;
            }
            else
            {
                splitText.text = Records.FormatTime(time);
                splitText.color = UIKit.TextColor;
            }
        }

        public void Flash(string text, Color color)
        {
            flashText.text = text;
            flashText.color = color;
            flashTimer = 2.5f;
        }

        public void ShowPause(bool paused)
        {
            pausePanel.SetActive(paused);
        }

        public void ShowFinish(float time, float prevBest, bool record, Medal medal, bool newMedal)
        {
            foreach (Transform c in finishContent) Destroy(c.gameObject);
            finishPanel.SetActive(true);
            splitBg.gameObject.SetActive(false);

            var title = UIKit.Label(finishContent, record ? "NEW RECORD!" : "FINISH!", 64, record ? UIKit.Accent : UIKit.TextColor);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(700f, 80f));
            UIKit.AddShadow(title, 3f, 0.4f);

            var t = UIKit.Label(finishContent, Records.FormatTime(time), 92, Color.white);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(700f, 110f));

            string sub;
            Color subCol = UIKit.TextDim;
            if (prevBest <= 0f) sub = "FIRST FINISH ON THIS TRACK";
            else
            {
                float d = time - prevBest;
                sub = (d < 0 ? "IMPROVED BY " : "PERSONAL BEST ") + (d < 0 ? Records.FormatDelta(d).Substring(1) : Records.FormatTime(prevBest) + "   (" + Records.FormatDelta(d) + ")");
                subCol = d < 0 ? new Color(0.4f, 0.75f, 1f) : UIKit.TextDim;
            }
            var s = UIKit.Label(finishContent, sub, 26, subCol);
            UIKit.Place(s.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(700f, 40f));

            // medal
            var medalRow = UIKit.Node("Medal", finishContent);
            UIKit.Place(UIKit.Rect(medalRow), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(600f, 80f));
            if (medal != Medal.None)
            {
                var disc = UIKit.Panel(medalRow.transform, "Disc", Records.MedalColor(medal), UIKit.Circle);
                UIKit.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(64f, 64f));
                UIKit.AddShadow(disc, 2f, 0.4f);
                var ml = UIKit.Label(medalRow.transform, Records.MedalName(medal) + " MEDAL" + (newMedal ? "  - NEW!" : ""), 34, Records.MedalColor(medal), TextAnchor.MiddleLeft);
                UIKit.Place(ml.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-100f, 0f), new Vector2(420f, 64f));
            }
            else
            {
                var ml = UIKit.Label(medalRow.transform, "BRONZE AT " + Records.FormatTime(info.BronzeTime), 30, UIKit.TextDim);
                UIKit.Fill(ml.rectTransform);
            }

            string[] labels = { "RETRY  (R)", "NEXT TRACK", "TRACKS", "MENU" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = UIKit.Button(finishContent, labels[i], 28, i == 0 ? UIKit.ButtonStyle.Primary : UIKit.ButtonStyle.Normal, () => OnFinishButton(idx));
                float x = (i % 2 == 0) ? -170f : 170f;
                float y = -400f - (i / 2) * 86f;
                UIKit.Place(UIKit.Rect(b.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(320f, 70f));
            }
            RefreshInfo();
        }

        void OnFinishButton(int i)
        {
            switch (i)
            {
                case 0: game.RestartRace(); break;
                case 1: game.NextTrack(); break;
                case 2: game.ExitToTrackSelect(); break;
                case 3: game.ShowMainMenu(); break;
            }
        }

        public void UpdateFps(float dt)
        {
            fpsAccum += dt;
            fpsFrames++;
            if (fpsAccum >= 0.5f)
            {
                fpsText.text = Mathf.RoundToInt(fpsFrames / fpsAccum) + " FPS";
                fpsAccum = 0f;
                fpsFrames = 0;
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!GameSettings.ShowFps && fpsText.text.Length > 0) fpsText.text = "";
            if (race == null || !root.activeSelf) return;
            float dt = Time.unscaledDeltaTime;

            float shown = race.RaceTime;
            if (race.CurrentState == RaceController.State.Racing && !race.Paused)
                shown += Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            timer.text = Records.FormatTime(shown);
            timer.color = race.CurrentState == RaceController.State.Finished ? UIKit.Accent : UIKit.TextColor;

            cpText.text = race.CheckpointTotal > 0 ? "CHECKPOINT  " + race.CheckpointsTaken + " / " + race.CheckpointTotal : "";

            var car = race.Car;
            float speed = car != null ? car.Speed : 0f;
            bool mph = GameSettings.UseMph;
            speedText.text = Mathf.RoundToInt(speed * (mph ? 2.23694f : 3.6f)).ToString();
            unitText.text = mph ? "MPH" : "KM/H";
            float frac = Mathf.Clamp01(speed / 85f);
            var parentRect = speedBar.rectTransform.parent as RectTransform;
            speedBar.rectTransform.sizeDelta = new Vector2(parentRect.rect.width * frac, 0f);
            float boost = car != null ? car.BoostVisual : 0f;
            boostGlow.color = new Color(1f, 0.55f, 0.1f, boost * 0.35f);
            speedBar.color = Color.Lerp(UIKit.Accent, new Color(1f, 0.4f, 0.1f), boost);

            // countdown pop animation
            if (countdownAnim < 1.2f)
            {
                countdownAnim += dt;
                float k = Mathf.Clamp01(countdownAnim / 0.15f);
                float scale = Mathf.Lerp(1.6f, 1f, k * k * (3 - 2 * k));
                countdownText.rectTransform.localScale = Vector3.one * scale;
                float a = 1f - Mathf.Clamp01((countdownAnim - 0.45f) / 0.25f);
                var c = countdownText.color;
                c.a = a;
                countdownText.color = c;
            }
            else if (countdownText.text.Length > 0) countdownText.text = "";

            if (splitTimer > 0f)
            {
                splitTimer -= dt;
                if (splitTimer <= 0f) splitBg.gameObject.SetActive(false);
            }
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                var c = flashText.color;
                c.a = Mathf.Clamp01(flashTimer / 0.4f);
                flashText.color = c;
                if (flashTimer <= 0f) flashText.text = "";
            }
        }
    }
}
