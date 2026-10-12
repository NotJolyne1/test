using UnityEngine;
using UnityEngine.UI;

namespace PolyRush
{
    public class Hud : MonoBehaviour
    {
        Game game;
        Race race;
        TrackInfo info;

        GameObject root, pausePanel, finishPanel;
        Transform finishCard;
        Text timer, cps, split, title, best, next, speed, unit, hint, message, fps;
        Image splitBg, speedBar, boostGlow;
        float splitTimer, messageTimer, fpsTime;
        int fpsFrames;

        static readonly Color Panel = new Color(0.05f, 0.07f, 0.13f, 0.62f);

        public void Build(Transform canvas, Game game)
        {
            this.game = game;
            root = Ui.Node("Hud", canvas);
            Ui.Fill(Ui.Rect(root));
            var r = root.transform;

            // clock, bottom middle
            var clock = Ui.Panel(r, "Clock", Panel);
            Ui.Place(clock.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(400f, 96f));
            timer = Ui.Label(clock.transform, "0:00.000", 64, Ui.TextColor);
            Ui.Fill(timer.rectTransform);

            splitBg = Ui.Panel(r, "Split", Panel);
            Ui.Place(splitBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(260f, 54f));
            split = Ui.Label(splitBg.transform, "", 34, Ui.Good);
            Ui.Fill(split.rectTransform);
            splitBg.gameObject.SetActive(false);

            cps = Ui.Label(r, "", 30, Ui.TextColor);
            Ui.Place(cps.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(400f, 44f));
            Ui.AddShadow(cps, 2f, 0.5f);

            // track name and times, top left
            title = Ui.Label(r, "", 28, Ui.TextColor, TextAnchor.UpperLeft);
            Ui.Place(title.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(32f, -24f), new Vector2(600f, 40f));
            Ui.AddShadow(title, 2f, 0.5f);
            best = Ui.Label(r, "", 22, Ui.TextDim, TextAnchor.UpperLeft, false);
            Ui.Place(best.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(32f, -62f), new Vector2(600f, 30f));
            Ui.AddShadow(best, 2f, 0.5f);
            next = Ui.Label(r, "", 22, Ui.TextDim, TextAnchor.UpperLeft, false);
            Ui.Place(next.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(32f, -90f), new Vector2(600f, 30f));
            Ui.AddShadow(next, 2f, 0.5f);

            // speed, bottom right
            var sp = Ui.Panel(r, "Speed", Panel);
            Ui.Place(sp.rectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-32f, 34f), new Vector2(280f, 120f));
            boostGlow = Ui.Panel(sp.transform, "Boost", new Color(1f, 0.55f, 0.1f, 0f));
            Ui.Fill(boostGlow.rectTransform);
            speed = Ui.Label(sp.transform, "0", 76, Ui.TextColor, TextAnchor.MiddleRight);
            Ui.Fill(speed.rectTransform);
            speed.rectTransform.offsetMin = new Vector2(10f, 24f);
            speed.rectTransform.offsetMax = new Vector2(-100f, -4f);
            unit = Ui.Label(sp.transform, "km/h", 24, Ui.TextDim, TextAnchor.MiddleLeft, false);
            Ui.Fill(unit.rectTransform);
            unit.rectTransform.offsetMin = new Vector2(186f, 24f);
            unit.rectTransform.offsetMax = new Vector2(-8f, -14f);
            var bar = Ui.Plain(sp.transform, "Bar", new Color(1f, 1f, 1f, 0.12f));
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = Vector2.right;
            bar.rectTransform.offsetMin = new Vector2(22f, 18f);
            bar.rectTransform.offsetMax = new Vector2(-22f, 24f);
            speedBar = Ui.Plain(bar.transform, "Fill", Ui.Accent);
            Ui.Place(speedBar.rectTransform, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            hint = Ui.Label(r, "", 26, Color.white);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(900f, 40f));
            Ui.AddOutline(hint, new Color(0f, 0f, 0f, 0.45f), 2f);

            message = Ui.Label(r, "", 38, Ui.Bad);
            Ui.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 60f));
            Ui.AddOutline(message, new Color(0f, 0f, 0f, 0.5f), 2f);

            var keys = Ui.Label(r, "R restart    Enter checkpoint    C camera    Esc pause", 18, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerLeft, false);
            Ui.Place(keys.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32f, 30f), new Vector2(800f, 26f));
            Ui.AddShadow(keys, 2f, 0.5f);

            fps = Ui.Label(canvas, "", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperRight, false);
            Ui.Place(fps.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12f, -8f), new Vector2(200f, 26f));

            BuildPause();
            finishPanel = Ui.Node("Finish", r);
            Ui.Fill(Ui.Rect(finishPanel));
            var dim = Ui.Plain(finishPanel.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.35f));
            dim.raycastTarget = true;
            Ui.Fill(dim.rectTransform);
            var card = Ui.Panel(finishPanel.transform, "Card", Ui.PanelColor);
            Ui.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(720f, 560f));
            finishCard = card.transform;
            finishPanel.SetActive(false);
            root.SetActive(false);
        }

        void BuildPause()
        {
            pausePanel = Ui.Node("Pause", root.transform);
            Ui.Fill(Ui.Rect(pausePanel));
            var dim = Ui.Plain(pausePanel.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.6f));
            dim.raycastTarget = true;
            Ui.Fill(dim.rectTransform);
            var card = Ui.Panel(pausePanel.transform, "Card", Ui.PanelColor);
            Ui.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 580f));
            var t = Ui.Label(card.transform, "Paused", 56, Ui.TextColor);
            Ui.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 76f));
            string[] labels = { "Resume", "Restart", "Settings", "Tracks", "Main menu" };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = Ui.Button(card.transform, labels[i], 30, i == 0 ? Ui.ButtonStyle.Primary : Ui.ButtonStyle.Normal, () => PauseButton(k));
                Ui.Place(Ui.Rect(b.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f - i * 84f), new Vector2(400f, 68f));
            }
            pausePanel.SetActive(false);
        }

        void PauseButton(int i)
        {
            if (i == 0) game.Resume();
            else if (i == 1) game.Retry();
            else if (i == 2) game.OpenSettings();
            else if (i == 3) game.ShowTracks();
            else game.ShowMain();
        }

        public void Show(bool on)
        {
            root.SetActive(on);
            if (on) return;
            race = null;
            pausePanel.SetActive(false);
            finishPanel.SetActive(false);
        }

        public void Bind(Race race, TrackInfo info)
        {
            this.race = race;
            this.info = info;
            title.text = (info.Index + 1) + ".  " + info.Name;
            RefreshTimes();
        }

        void RefreshTimes()
        {
            float pb = Records.BestTime(info.Id);
            best.text = "Best  " + (pb > 0 ? Records.FormatTime(pb) : "-");
            Medal m = Records.MedalFor(info, pb);
            if (m == Medal.Author)
            {
                next.text = "All medals done";
                return;
            }
            Medal goal = (Medal)((int)m + 1);
            float t = goal == Medal.Bronze ? info.BronzeTime : goal == Medal.Silver ? info.SilverTime : goal == Medal.Gold ? info.GoldTime : info.AuthorTime;
            next.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Records.MedalColor(goal)) + ">" + Records.MedalName(goal) + "</color>  " + Records.FormatTime(t);
        }

        public void Clear()
        {
            finishPanel.SetActive(false);
            pausePanel.SetActive(false);
            splitBg.gameObject.SetActive(false);
            message.text = "";
            RefreshTimes();
        }

        public void Checkpoint(float time, float? diff)
        {
            splitTimer = 2.5f;
            splitBg.gameObject.SetActive(true);
            if (diff.HasValue)
            {
                split.text = Records.FormatDelta(diff.Value);
                split.color = diff.Value <= 0f ? new Color(0.45f, 0.75f, 1f) : Ui.Bad;
            }
            else
            {
                split.text = Records.FormatTime(time);
                split.color = Ui.TextColor;
            }
        }

        public void Message(string text, Color color)
        {
            message.text = text;
            message.color = color;
            messageTimer = 2.5f;
        }

        public void ShowPause(bool on) { pausePanel.SetActive(on); }

        public void Finished(float time, float before, bool record, Medal medal, bool newMedal)
        {
            foreach (Transform c in finishCard) Destroy(c.gameObject);
            finishPanel.SetActive(true);
            splitBg.gameObject.SetActive(false);
            var c0 = finishCard;

            var head = Ui.Label(c0, record && before > 0 ? "New best!" : "Finished", 54, record && before > 0 ? Ui.Accent : Ui.TextColor);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(660f, 70f));

            var big = Ui.Label(c0, Records.FormatTime(time), 88, Color.white);
            Ui.Place(big.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(660f, 104f));

            string sub;
            Color subColor = Ui.TextDim;
            if (before <= 0f) sub = "First time on this track";
            else if (time < before)
            {
                sub = Records.FormatDelta(time - before) + " faster than before";
                subColor = new Color(0.45f, 0.75f, 1f);
            }
            else sub = "Best is " + Records.FormatTime(before) + "  (" + Records.FormatDelta(time - before) + ")";
            var s = Ui.Label(c0, sub, 26, subColor, TextAnchor.MiddleCenter, false);
            Ui.Place(s.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(660f, 36f));

            var row = Ui.Node("Medal", c0);
            Ui.Place(Ui.Rect(row), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(560f, 70f));
            if (medal != Medal.None)
            {
                var disc = Ui.Panel(row.transform, "Disc", Records.MedalColor(medal), Ui.Circle);
                Ui.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140f, 0f), new Vector2(56f, 56f));
                var ml = Ui.Label(row.transform, Records.MedalName(medal) + " medal" + (newMedal ? "  (new)" : ""), 32, Records.MedalColor(medal), TextAnchor.MiddleLeft);
                Ui.Place(ml.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-96f, 0f), new Vector2(400f, 56f));
            }
            else
            {
                var ml = Ui.Label(row.transform, "Bronze is " + Records.FormatTime(info.BronzeTime), 28, Ui.TextDim, TextAnchor.MiddleCenter, false);
                Ui.Fill(ml.rectTransform);
            }

            string[] labels = { "Retry", "Next track", "Tracks", "Menu" };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = Ui.Button(c0, labels[i], 28, i == 0 ? Ui.ButtonStyle.Primary : Ui.ButtonStyle.Normal, () => FinishButton(k));
                Ui.Place(Ui.Rect(b.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(i % 2 == 0 ? -160f : 160f, -362f - (i / 2) * 82f), new Vector2(300f, 66f));
            }
            RefreshTimes();
        }

        void FinishButton(int i)
        {
            if (i == 0) game.Retry();
            else if (i == 1) game.NextTrack();
            else if (i == 2) game.ShowTracks();
            else game.ShowMain();
        }

        void Update()
        {
            if (Settings.ShowFps)
            {
                fpsTime += Time.unscaledDeltaTime;
                fpsFrames++;
                if (fpsTime > 0.5f)
                {
                    fps.text = Mathf.RoundToInt(fpsFrames / fpsTime) + " fps";
                    fpsTime = 0f;
                    fpsFrames = 0;
                }
            }
            else if (fps.text.Length > 0) fps.text = "";

            if (race == null || !root.activeSelf) return;
            float dt = Time.unscaledDeltaTime;

            float shown = race.Clock;
            if (race.Phase == Race.State.Driving && !race.Paused)
                shown += Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            timer.text = Records.FormatTime(shown);
            timer.color = race.Phase == Race.State.Finished ? Ui.Accent : Ui.TextColor;
            cps.text = race.CheckpointCount > 0 ? race.CheckpointsHit + " / " + race.CheckpointCount : "";

            hint.text = race.Phase == Race.State.Ready ? "Press W or Up to go" : "";

            var car = race.Car;
            float v = car != null ? car.Speed : 0f;
            bool mph = Settings.UseMph;
            speed.text = Mathf.RoundToInt(v * (mph ? 2.23694f : 3.6f)).ToString();
            unit.text = mph ? "mph" : "km/h";
            var barRect = speedBar.rectTransform.parent as RectTransform;
            speedBar.rectTransform.sizeDelta = new Vector2(barRect.rect.width * Mathf.Clamp01(v / 95f), 0f);
            float boost = car != null ? car.Boost : 0f;
            boostGlow.color = new Color(1f, 0.55f, 0.1f, boost * 0.35f);
            speedBar.color = Color.Lerp(Ui.Accent, new Color(1f, 0.4f, 0.1f), boost);

            if (splitTimer > 0f && (splitTimer -= dt) <= 0f) splitBg.gameObject.SetActive(false);
            if (messageTimer > 0f)
            {
                messageTimer -= dt;
                var c = message.color;
                c.a = Mathf.Clamp01(messageTimer / 0.4f);
                message.color = c;
                if (messageTimer <= 0f) message.text = "";
            }
        }
    }
}
