using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RacingThing
{
    // The parts of the race HUD that only show up online: countdown, live order, who finished,
    // the results card and the Esc menu (which doesn't pause, the others are still driving).
    public partial class Hud
    {
        const string SoloKeys = "R restart    Enter checkpoint    C camera    Esc pause";
        const string OnlineKeys = "Enter or R  last checkpoint    H  horn    C  camera    Esc  menu";

        GameObject onlineRoot, onlineMenu, resultsPanel, board;
        Text countdown, waiting, place, placeOf, banner, endsIn, reconnecting, mineFinished;
        Transform resultsCard;
        Button resultsLobby;
        Text resultsFooter;
        readonly List<Text[]> boardRows = new List<Text[]>();
        readonly List<Image> boardDots = new List<Image>();
        RaceSync sync;
        float goFlash, bannerTimer, boardTimer;
        int resultsShownFor = -1;

        public bool OnlineMenuOpen { get { return onlineMenu != null && onlineMenu.activeSelf; } }

        void BuildOnline(Transform r)
        {
            onlineRoot = Ui.Node("Online", r);
            Ui.Fill(Ui.Rect(onlineRoot));
            var o = onlineRoot.transform;

            countdown = Ui.Label(o, "", 190, Color.white);
            Ui.Place(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(800f, 240f));
            Ui.AddOutline(countdown, new Color(0f, 0f, 0f, 0.5f), 4f);

            waiting = Ui.Label(o, "", 34, Color.white);
            Ui.Place(waiting.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1200f, 50f));
            Ui.AddOutline(waiting, new Color(0f, 0f, 0f, 0.45f), 2f);

            // place, top right
            place = Ui.Label(o, "", 84, Color.white, TextAnchor.UpperRight);
            Ui.Place(place.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-110f, -14f), new Vector2(300f, 100f));
            Ui.AddShadow(place, 4f, 0.5f);
            placeOf = Ui.Label(o, "", 34, Ui.TextDim, TextAnchor.UpperLeft);
            Ui.Place(placeOf.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-28f, -46f), new Vector2(82f, 50f));
            Ui.AddShadow(placeOf, 2f, 0.5f);

            // everyone in order, under it
            board = Ui.Node("Board", o);
            Ui.Place(Ui.Rect(board), Vector2.one, Vector2.one, Vector2.one, new Vector2(-28f, -124f), new Vector2(380f, 8 * 38f));
            for (int i = 0; i < Online.MaxPlayers; i++)
            {
                var bg = Ui.Panel(board.transform, "Row", new Color(0.05f, 0.07f, 0.13f, 0.55f), Ui.RoundedSmall);
                bg.raycastTarget = false;
                Ui.Place(bg.rectTransform, Vector2.up, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -i * 38f), new Vector2(0f, 34f));
                var pos = Ui.Label(bg.transform, "", 20, Ui.TextDim, TextAnchor.MiddleLeft);
                Ui.Place(pos.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(40f, 30f));
                var dot = Ui.Panel(bg.transform, "Dot", Color.white, Ui.Circle);
                dot.raycastTarget = false;
                Ui.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(16f, 16f));
                var name = Ui.Label(bg.transform, "", 20, Ui.TextColor, TextAnchor.MiddleLeft, false);
                Ui.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(190f, 30f));
                var time = Ui.Label(bg.transform, "", 20, Ui.TextDim, TextAnchor.MiddleRight, false);
                Ui.Place(time.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(120f, 30f));
                boardRows.Add(new[] { pos, name, time });
                boardDots.Add(dot);
                bg.gameObject.SetActive(false);
            }

            banner = Ui.Label(o, "", 36, Ui.Accent);
            Ui.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(1200f, 50f));
            Ui.AddOutline(banner, new Color(0f, 0f, 0f, 0.5f), 2f);

            endsIn = Ui.Label(o, "", 28, Color.white);
            Ui.Place(endsIn.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(800f, 40f));
            Ui.AddOutline(endsIn, new Color(0f, 0f, 0f, 0.5f), 2f);

            mineFinished = Ui.Label(o, "", 44, Color.white);
            Ui.Place(mineFinished.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1200f, 120f));
            Ui.AddOutline(mineFinished, new Color(0f, 0f, 0f, 0.5f), 3f);

            reconnecting = Ui.Label(o, "Connection dropped, reconnecting...", 30, Ui.Bad);
            Ui.Place(reconnecting.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1000f, 44f));
            Ui.AddOutline(reconnecting, new Color(0f, 0f, 0f, 0.6f), 2f);
            reconnecting.gameObject.SetActive(false);

            BuildResults(o);
            BuildOnlineMenu(o);
            onlineRoot.SetActive(false);
        }

        void BuildResults(Transform o)
        {
            resultsPanel = Ui.Node("Results", o);
            Ui.Fill(Ui.Rect(resultsPanel));
            var dim = Ui.Plain(resultsPanel.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.45f));
            dim.raycastTarget = true;
            Ui.Fill(dim.rectTransform);
            var card = Ui.Panel(resultsPanel.transform, "Card", Ui.PanelColor);
            Ui.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 720f));
            resultsCard = card.transform;

            resultsLobby = Ui.Button(resultsPanel.transform, "Back to lobby", 28, Ui.ButtonStyle.Primary, () =>
            {
                if (sync != null) sync.BackToLobby();
            });
            Ui.Place(Ui.Rect(resultsLobby.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, -310f), new Vector2(320f, 64f));
            var leave = Ui.Button(resultsPanel.transform, "Leave room", 28, Ui.ButtonStyle.Normal, LeaveRoom);
            Ui.Place(Ui.Rect(leave.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170f, -310f), new Vector2(320f, 64f));
            resultsFooter = Ui.Label(resultsPanel.transform, "", 20, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(resultsFooter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -372f), new Vector2(700f, 30f));
            resultsPanel.SetActive(false);
        }

        void BuildOnlineMenu(Transform o)
        {
            onlineMenu = Ui.Node("Online menu", o);
            Ui.Fill(Ui.Rect(onlineMenu));
            var dim = Ui.Plain(onlineMenu.transform, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.55f));
            dim.raycastTarget = true;
            Ui.Fill(dim.rectTransform);
            var card = Ui.Panel(onlineMenu.transform, "Card", Ui.PanelColor);
            Ui.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 440f));
            var t = Ui.Label(card.transform, "Menu", 50, Ui.TextColor);
            Ui.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(480f, 70f));
            var note = Ui.Label(card.transform, "The race keeps going while this is open", 20, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(480f, 30f));

            string[] labels = { "Back to the race", "Settings", "Leave room" };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = Ui.Button(card.transform, labels[i], 30, i == 0 ? Ui.ButtonStyle.Primary : Ui.ButtonStyle.Normal, () => OnlineMenuButton(k));
                Ui.Place(Ui.Rect(b.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f - i * 84f), new Vector2(400f, 68f));
            }
            onlineMenu.SetActive(false);
        }

        void OnlineMenuButton(int i)
        {
            if (i == 0) ShowOnlineMenu(false);
            else if (i == 1) game.OpenSettings();
            else LeaveRoom();
        }

        void LeaveRoom()
        {
            if (Online.Instance != null) Online.Instance.Leave();
        }

        public void ShowOnlineMenu(bool on)
        {
            if (onlineMenu != null) onlineMenu.SetActive(on);
        }

        // ------------------------------------------------------------ binding

        void BindOnline(Race r)
        {
            UnbindOnline();
            onlineRoot.SetActive(r.Online);
            if (!r.Online) return;
            sync = r.Sync;
            sync.SomeoneFinished += OnSomeoneFinished;
            resultsShownFor = -1;
            resultsPanel.SetActive(false);
            onlineMenu.SetActive(false);
            mineFinished.text = "";
            banner.text = "";
            endsIn.text = "";
            countdown.text = "";
            goFlash = 0f;
            boardTimer = 0f;
        }

        void UnbindOnline()
        {
            if (sync != null) sync.SomeoneFinished -= OnSomeoneFinished;
            sync = null;
            if (onlineRoot != null) onlineRoot.SetActive(false);
        }

        void OnSomeoneFinished(int playerId, string who, int position, int ms)
        {
            var me = Online.Instance != null ? Online.Instance.Me : null;
            if (me != null && me.PlayerId == playerId) return;   // our own finish has its own message
            banner.text = position == 1 ? who + " won the race!" : who + " finished " + Ordinal(position);
            banner.color = position == 1 ? Ui.Accent : Color.white;
            bannerTimer = 4f;
        }

        public void FinishedOnline(float time)
        {
            int at = sync != null ? sync.FinishedCount : 1;
            mineFinished.text = (at == 1 ? "You won!" : "You finished " + Ordinal(at)) + "\n<size=34>" + Records.FormatTime(time) + "</size>";
            mineFinished.color = at == 1 ? Ui.Accent : Color.white;
            splitBg.gameObject.SetActive(false);
        }

        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        // ------------------------------------------------------------ every frame

        void UpdateOnline(float dt)
        {
            if (sync == null) return;
            var online = Online.Instance;
            reconnecting.gameObject.SetActive(online != null && online.Reconnecting);

            // countdown
            float left = race.CountdownLeft;
            if (race.Phase == Race.State.Ready)
            {
                if (float.IsNaN(left))
                {
                    countdown.text = "";
                    waiting.text = "Waiting for everyone to load the track...";
                }
                else
                {
                    waiting.text = left > 3f ? "Get ready" : "";
                    countdown.text = left > 3f ? "" : Mathf.CeilToInt(left).ToString();
                    countdown.color = Color.white;
                    countdown.rectTransform.localScale = Vector3.one * (1f + 0.25f * Mathf.Repeat(left, 1f));
                }
                goFlash = 0.9f;
            }
            else
            {
                waiting.text = "";
                if (goFlash > 0f)
                {
                    goFlash -= dt;
                    countdown.text = "GO!";
                    countdown.color = new Color(Ui.Good.r, Ui.Good.g, Ui.Good.b, Mathf.Clamp01(goFlash / 0.3f));
                    countdown.rectTransform.localScale = Vector3.one;
                }
                else countdown.text = "";
            }

            // the clock shows the shared race time while waiting so it doesn't look stuck
            if (race.Phase == Race.State.Ready) timer.text = Records.FormatTime(0f);

            if ((boardTimer -= dt) <= 0f)
            {
                boardTimer = 0.2f;
                RefreshBoard();
            }

            // someone's finished, the rest have a limited time
            double end = sync.EndTime;
            if (sync.Phase == RaceSync.Racing && end > 0.0)
            {
                int secs = Mathf.Max(0, Mathf.CeilToInt((float)(end - RaceSync.Now)));
                endsIn.text = race.Phase == Race.State.Finished ? "Waiting for the others  " + secs : "Race ends in " + secs;
                endsIn.color = secs <= 10 ? Ui.Bad : Color.white;
            }
            else endsIn.text = "";

            if (bannerTimer > 0f && (bannerTimer -= dt) <= 0f) banner.text = "";

            if (sync.Phase == RaceSync.Done) ShowResults();
            else if (resultsPanel.activeSelf) resultsPanel.SetActive(false);
        }

        void RefreshBoard()
        {
            var list = sync.Standings();
            int mine = 0;
            for (int i = 0; i < boardRows.Count; i++)
            {
                var row = boardRows[i];
                var bg = row[0].transform.parent.gameObject;
                bool on = i < list.Count && list.Count > 1;
                if (bg.activeSelf != on) bg.SetActive(on);
                if (!on) continue;
                var s = list[i];
                if (s.IsMe) mine = i + 1;
                row[0].text = (i + 1).ToString();
                row[1].text = s.Name;
                row[1].color = s.IsMe ? Ui.Accent : Ui.TextColor;
                row[2].text = s.Finished ? Records.FormatTime(s.Ms / 1000f) : "";
                boardDots[i].color = Settings.CarColors[Mathf.Clamp(s.Car, 0, Settings.CarColors.Length - 1)];
            }
            if (mine == 0)
                for (int i = 0; i < list.Count; i++) if (list[i].IsMe) mine = i + 1;

            bool racing = list.Count > 1 && race.Phase != Race.State.Ready;
            place.text = racing && mine > 0 ? Ordinal(mine) : "";
            placeOf.text = racing && mine > 0 ? "/" + list.Count : "";
        }

        void ShowResults()
        {
            var host = Online.Instance != null && Online.Instance.IsHost;
            resultsLobby.gameObject.SetActive(host);
            resultsFooter.text = host ? "Everyone goes back to the lobby when you do" : "Waiting for the host to go back to the lobby";
            if (resultsPanel.activeSelf && resultsShownFor == sync.RaceId) return;

            resultsShownFor = sync.RaceId;
            resultsPanel.SetActive(true);
            onlineMenu.SetActive(false);
            mineFinished.text = "";
            endsIn.text = "";
            foreach (Transform c in resultsCard) Destroy(c.gameObject);

            var results = sync.Results;
            var me = Online.Instance != null ? Online.Instance.Me : null;
            string headline = "Nobody finished";
            Color headColor = Ui.TextColor;
            if (results.Count > 0 && results[0].Finished)
            {
                bool mineWon = me != null && results[0].PlayerId == me.PlayerId;
                headline = mineWon ? "You won!" : results[0].Name + " wins!";
                headColor = Color.Lerp(Settings.CarColors[Mathf.Clamp(results[0].Car, 0, Settings.CarColors.Length - 1)], Color.white, 0.3f);
                Sfx.Play(mineWon ? Sound.Finish : Sound.Checkpoint);
            }
            var head = Ui.Label(resultsCard, headline, 64, headColor);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(720f, 84f));
            Ui.AddShadow(head, 4f, 0.4f);
            var sub = Ui.Label(resultsCard, info != null ? info.Name : "", 24, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(720f, 32f));

            float best = results.Count > 0 && results[0].Finished ? results[0].Ms / 1000f : 0f;
            for (int i = 0; i < results.Count && i < Online.MaxPlayers; i++)
            {
                var r = results[i];
                bool isMe = me != null && r.PlayerId == me.PlayerId;
                float y = -170f - i * 58f;
                var row = Ui.Panel(resultsCard, "Row", isMe ? new Color(1f, 0.72f, 0.16f, 0.18f) : (i % 2 == 0 ? Ui.PanelLight : new Color(1f, 1f, 1f, 0.03f)), Ui.RoundedSmall);
                row.raycastTarget = false;
                Ui.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(680f, 50f));
                var rt = row.transform;
                var pos = Ui.Label(rt, r.Finished ? Ordinal(i + 1) : "-", 26, i == 0 && r.Finished ? Ui.Accent : Ui.TextColor, TextAnchor.MiddleLeft);
                Ui.Place(pos.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(80f, 40f));
                var dot = Ui.Panel(rt, "Dot", Settings.CarColors[Mathf.Clamp(r.Car, 0, Settings.CarColors.Length - 1)], Ui.Circle);
                dot.raycastTarget = false;
                Ui.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(22f, 22f));
                var name = Ui.Label(rt, r.Name + (isMe ? "  (you)" : ""), 26, Ui.TextColor, TextAnchor.MiddleLeft, false);
                Ui.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(330f, 40f));
                string time = r.Finished ? Records.FormatTime(r.Ms / 1000f) : "DNF";
                if (r.Finished && i > 0) time += "   <color=#9AA6BF>" + Records.FormatDelta(r.Ms / 1000f - best) + "</color>";
                var tl = Ui.Label(rt, time, 24, r.Finished ? Ui.TextColor : Ui.TextDim, TextAnchor.MiddleRight, false);
                Ui.Place(tl.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(260f, 40f));
            }
        }
    }
}
