using System.Collections.Generic;
using RelayNetworking.Common;
using UnityEngine;
using UnityEngine.UI;

namespace RacingThing
{
    // The two multiplayer screens: "Multiplayer" (name, server, host or join with a code)
    // and the lobby you sit in between races.
    public partial class Menus
    {
        Online online;

        // multiplayer page
        InputField nameField, serverField, codeField;
        Text statusText;
        Button hostButton, joinButton, cancelButton;
        string lastProblem;

        // lobby
        Transform playerList;
        Text playerCount, trackName, trackFacts, trackHint, startNote, raceNote, pingText, chatLog, codeLabel, carName;
        Image carSwatch;
        Button startButton, prevTrack, nextTrack;
        InputField chatField;
        readonly List<string> chat = new List<string>();
        float pingTimer;

        void ListenOnline()
        {
            online = Online.Instance;
            if (online == null) return;
            online.Changed += OnOnlineChanged;
            online.Line += OnChatLine;
        }

        void OnDestroy()
        {
            if (online == null) return;
            online.Changed -= OnOnlineChanged;
            online.Line -= OnChatLine;
        }

        void OnOnlineChanged()
        {
            if (page == Page.Online) RefreshStatus();
            else if (page == Page.Lobby) RefreshLobby();
        }

        void OnChatLine(string text, Color color)
        {
            chat.Add("<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>");
            if (chat.Count > 60) chat.RemoveAt(0);
            if (page == Page.Lobby) RefreshChat();
        }

        void UpdateOnline()
        {
            if (page != Page.Lobby || pingText == null) return;
            if ((pingTimer -= Time.unscaledDeltaTime) > 0f) return;
            pingTimer = 1f;
            pingText.text = online.Ping > 0 ? "Ping " + online.Ping + " ms" : "";
        }

        static string SavedName()
        {
            string n = PlayerPrefs.GetString("rt_name", "");
            if (n.Length == 0)
            {
                n = "Racer" + Random.Range(100, 1000);
                PlayerPrefs.SetString("rt_name", n);
            }
            return n;
        }

        // ------------------------------------------------------------ multiplayer page

        public void ShowOnline(string problem)
        {
            HideAll();
            page = Page.Online;
            lastProblem = problem;
            var t = pageRoot.transform;

            var side = Ui.Plain(t, "Shade", new Color(0.02f, 0.04f, 0.1f, 0.45f));
            Ui.Place(side.rectTransform, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900f, 0f));

            var back = Ui.Button(t, "Back", 26, Ui.ButtonStyle.Normal, () =>
            {
                if (online.State != Online.Status.Offline) online.Leave();
                game.ShowMain();
            });
            TopLeft(back, 60f, -48f, 160f, 60f);
            var head = Ui.Label(t, "Multiplayer", 64, Color.white, TextAnchor.MiddleLeft);
            TopLeft(head, 100f, -150f, 760f, 80f);
            Ui.AddShadow(head, 4f, 0.4f);
            var sub = Ui.Label(t, "One of you hosts and gets a room code. Everyone else types it in.", 24, Ui.TextDim, TextAnchor.MiddleLeft, false);
            TopLeft(sub, 104f, -222f, 760f, 34f);

            TopLeft(Ui.Label(t, "Your name", 22, Ui.TextDim, TextAnchor.MiddleLeft, false), 104f, -290f, 300f, 30f);
            nameField = Ui.Input(t, "Pick a name", SavedName(), 30, 16);
            TopLeft(nameField, 100f, -324f, 520f, 64f);

            TopLeft(Ui.Label(t, "Server", 22, Ui.TextDim, TextAnchor.MiddleLeft, false), 104f, -406f, 300f, 30f);
            serverField = Ui.Input(t, "IP address of the relay server", PlayerPrefs.GetString("rt_server", Online.DefaultServer), 26, 80);
            TopLeft(serverField, 100f, -440f, 520f, 58f);

            hostButton = Ui.Button(t, "Host a game", 34, Ui.ButtonStyle.Primary, Host);
            TopLeft(hostButton, 100f, -540f, 520f, 80f);

            var or = Ui.Label(t, "or join one", 22, Ui.TextDim, TextAnchor.MiddleLeft, false);
            TopLeft(or, 104f, -644f, 300f, 30f);
            codeField = Ui.Input(t, "Room code", "", 34, 8);
            codeField.onValidateInput += (text, index, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            codeField.onEndEdit.AddListener(_ =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Join();
            });
            TopLeft(codeField, 100f, -680f, 300f, 76f);
            joinButton = Ui.Button(t, "Join", 32, Ui.ButtonStyle.Normal, Join);
            TopLeft(joinButton, 420f, -680f, 200f, 76f);

            statusText = Ui.Label(t, "", 24, Ui.TextDim, TextAnchor.UpperLeft, false);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            TopLeft(statusText, 104f, -790f, 720f, 90f);
            cancelButton = Ui.Button(t, "Cancel", 24, Ui.ButtonStyle.Normal, () => online.Leave());
            TopLeft(cancelButton, 100f, -890f, 200f, 56f);

            var help = Ui.Label(t, "Whoever runs the RelayNetworking server: everyone needs its IP here. Same PC as the server? Use 127.0.0.1", 18, new Color(1f, 1f, 1f, 0.45f), TextAnchor.LowerLeft, false);
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Place(help.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(104f, 40f), new Vector2(720f, 60f));

            RefreshStatus();
        }

        void Host()
        {
            if (!SaveDetails()) return;
            online.HostGame(nameField.text, serverField.text);
        }

        void Join()
        {
            if (online.State != Online.Status.Offline || !SaveDetails()) return;
            string code = Online.CleanCode(codeField.text);
            if (code.Length < 3)
            {
                lastProblem = "Type in the room code first.";
                RefreshStatus();
                return;
            }
            online.JoinGame(nameField.text, serverField.text, code);
        }

        bool SaveDetails()
        {
            string name = Online.CleanName(nameField.text);
            if (name.Length == 0)
            {
                lastProblem = "You need a name.";
                RefreshStatus();
                return false;
            }
            nameField.text = name;
            PlayerPrefs.SetString("rt_name", name);
            PlayerPrefs.SetString("rt_server", serverField.text.Trim());
            lastProblem = null;
            return true;
        }

        void RefreshStatus()
        {
            if (statusText == null) return;
            bool busy = online.State != Online.Status.Offline && online.State != Online.Status.InRoom;
            hostButton.interactable = joinButton.interactable = !busy;
            cancelButton.gameObject.SetActive(busy);
            switch (online.State)
            {
                case Online.Status.Connecting: statusText.text = "Connecting to " + online.ServerAddress + "..."; break;
                case Online.Status.SigningIn: statusText.text = "Connected. Signing in..."; break;
                case Online.Status.Joining: statusText.text = "Getting into room " + online.Code + "..."; break;
                default: statusText.text = lastProblem ?? ""; break;
            }
            statusText.color = busy ? Ui.TextColor : Ui.Bad;
        }

        // ------------------------------------------------------------ lobby

        public void ShowLobby()
        {
            HideAll();
            page = Page.Lobby;
            var t = pageRoot.transform;

            var shade = Ui.Plain(t, "Shade", new Color(0.02f, 0.04f, 0.1f, 0.45f));
            Ui.Fill(shade.rectTransform);

            var leave = Ui.Button(t, "Leave", 26, Ui.ButtonStyle.Normal, () => online.Leave());
            TopLeft(leave, 60f, -48f, 160f, 60f);
            var settings = Ui.Button(t, "Settings", 26, Ui.ButtonStyle.Normal, () => ShowSettings());
            Ui.Place(Ui.Rect(settings.gameObject), Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -48f), new Vector2(190f, 60f));

            // room code, big, since that's what people read out to each other
            TopLeft(Ui.Label(t, "Room code", 24, Ui.TextDim, TextAnchor.MiddleLeft, false), 64f, -140f, 300f, 30f);
            codeLabel = Ui.Label(t, online.Code ?? "", 96, Ui.Accent, TextAnchor.MiddleLeft);
            TopLeft(codeLabel, 58f, -168f, 520f, 110f);
            Ui.AddShadow(codeLabel, 4f, 0.4f);
            Button copy = null;
            copy = Ui.Button(t, "Copy", 22, Ui.ButtonStyle.Normal, () =>
            {
                GUIUtility.systemCopyBuffer = online.Code ?? "";
                Ui.ButtonLabel(copy).text = "Copied";
            });
            TopLeft(copy, 520f, -196f, 130f, 52f);

            // players
            var players = Ui.Panel(t, "Players", Ui.PanelColor);
            TopLeft(players, 60f, -300f, 820f, 470f);
            TopLeft(Ui.Label(players.transform, "Players", 30, Color.white, TextAnchor.MiddleLeft), 28f, -20f, 300f, 40f);
            playerCount = Ui.Label(players.transform, "", 24, Ui.TextDim, TextAnchor.MiddleRight, false);
            Ui.Place(playerCount.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-28f, -20f), new Vector2(200f, 40f));
            playerList = Ui.Node("List", players.transform).transform;
            Ui.Fill(Ui.Rect(playerList.gameObject));

            // chat
            var chatBox = Ui.Panel(t, "Chat", Ui.PanelColor);
            Ui.Place(chatBox.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60f, 40f), new Vector2(820f, 250f));
            chatLog = Ui.Label(chatBox.transform, "", 22, Ui.TextColor, TextAnchor.LowerLeft, false);
            chatLog.horizontalOverflow = HorizontalWrapMode.Wrap;
            chatLog.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.Fill(chatLog.rectTransform);
            chatLog.rectTransform.offsetMin = new Vector2(22f, 76f);
            chatLog.rectTransform.offsetMax = new Vector2(-22f, -14f);
            chatField = Ui.Input(chatBox.transform, "Say something (Enter)", "", 22, 100);
            Ui.Place(Ui.Rect(chatField.gameObject), Vector2.zero, Vector2.right, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-28f, 50f));
            chatField.onEndEdit.AddListener(text =>
            {
                if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
                online.SendChat(text);
                chatField.text = "";
                chatField.ActivateInputField();
            });

            // track and car
            var right = Ui.Panel(t, "Race", Ui.PanelColor);
            Ui.Place(right.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -140f), new Vector2(880f, 600f));
            var r = right.transform;
            TopLeft(Ui.Label(r, "Track", 24, Ui.TextDim, TextAnchor.MiddleLeft, false), 36f, -28f, 300f, 30f);
            trackName = Ui.Label(r, "", 52, Color.white, TextAnchor.MiddleLeft);
            TopLeft(trackName, 104f, -62f, 680f, 70f);
            prevTrack = Ui.Button(r, "<", 34, Ui.ButtonStyle.Normal, () => online.Race.PickTrack(online.Race.Track - 1));
            TopLeft(prevTrack, 30f, -66f, 60f, 60f);
            nextTrack = Ui.Button(r, ">", 34, Ui.ButtonStyle.Normal, () => online.Race.PickTrack(online.Race.Track + 1));
            Ui.Place(Ui.Rect(nextTrack.gameObject), Vector2.one, Vector2.one, Vector2.one, new Vector2(-30f, -66f), new Vector2(60f, 60f));
            trackFacts = Ui.Label(r, "", 22, Ui.TextDim, TextAnchor.MiddleLeft, false);
            TopLeft(trackFacts, 106f, -136f, 700f, 32f);
            trackHint = Ui.Label(r, "", 20, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft, false);
            TopLeft(trackHint, 106f, -172f, 700f, 30f);

            TopLeft(Ui.Label(r, "Your car", 24, Ui.TextDim, TextAnchor.MiddleLeft, false), 36f, -236f, 300f, 30f);
            var prevCar = Ui.Button(r, "<", 30, Ui.ButtonStyle.Normal, () => ChangeCar(-1));
            TopLeft(prevCar, 30f, -270f, 56f, 56f);
            carSwatch = Ui.Panel(r, "Swatch", Color.white, Ui.Circle);
            TopLeft(carSwatch, 104f, -280f, 36f, 36f);
            carName = Ui.Label(r, "", 30, Color.white, TextAnchor.MiddleLeft);
            TopLeft(carName, 154f, -270f, 300f, 56f);
            var nextCar = Ui.Button(r, ">", 30, Ui.ButtonStyle.Normal, () => ChangeCar(1));
            TopLeft(nextCar, 420f, -270f, 56f, 56f);

            raceNote = Ui.Label(r, "", 24, Ui.Accent, TextAnchor.MiddleCenter, false);
            raceNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Place(raceNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(800f, 70f));
            startButton = Ui.Button(r, "Start race", 40, Ui.ButtonStyle.Primary, () => online.Race.StartRace());
            Ui.Place(Ui.Rect(startButton.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(560f, 92f));
            startNote = Ui.Label(r, "", 24, Ui.TextDim, TextAnchor.MiddleCenter, false);
            Ui.Place(startNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(800f, 36f));

            pingText = Ui.Label(t, "", 20, Ui.TextDim, TextAnchor.LowerRight, false);
            Ui.Place(pingText.rectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-60f, 40f), new Vector2(300f, 30f));
            pingTimer = 0f;

            RefreshLobby();
            RefreshChat();
        }

        void ChangeCar(int d)
        {
            int n = Settings.CarColors.Length;
            Settings.CarColor = ((Settings.CarColor + d) % n + n) % n;
            online.ShareCar();
            RefreshLobby();
        }

        void RefreshLobby()
        {
            if (page != Page.Lobby || playerList == null || !online.InRoom) return;
            var sync = online.Race;
            bool host = online.IsHost;
            codeLabel.text = online.Code ?? "";

            // players
            foreach (Transform c in playerList) Destroy(c.gameObject);
            var list = online.Players;
            playerCount.text = list.Count + " / " + Online.MaxPlayers;
            for (int i = 0; i < list.Count; i++) PlayerRow(list[i], i, host);

            // track
            var info = TrackList.All[sync.Track];
            var theme = Theme.Get(info.ThemeId);
            trackName.text = (info.Index + 1) + ".  " + info.Name;
            trackFacts.text = theme.DisplayName + "   " + info.DifficultyName + "   " + Mathf.RoundToInt(info.Length) + " m   " + info.Checkpoints + " checkpoints";
            prevTrack.gameObject.SetActive(host);
            nextTrack.gameObject.SetActive(host);
            trackHint.text = host ? "You're the host, so you pick." : "The host picks the track.";

            carSwatch.color = Settings.CarColors[Settings.CarColor];
            carName.text = Settings.CarColorNames[Settings.CarColor];

            // what's going on
            string phase = sync.Phase;
            bool racing = phase != null && phase != RaceSync.Lobby;
            raceNote.text = racing ? "There's a race going on. You'll be in the next one." : "";
            startButton.gameObject.SetActive(host && !racing);
            startButton.interactable = sync.CanStart;
            var leader = online.Host;
            if (host) startNote.text = racing ? "" : list.Count < 2 ? "You can start on your own, or wait for friends." : "Start when everyone's in.";
            else startNote.text = leader != null && !racing ? "Waiting for " + Online.NameOf(leader) + " to start the race" : "";
        }

        void PlayerRow(RelayPlayer p, int i, bool host)
        {
            bool me = p == online.Me;
            var row = Ui.Panel(playerList, "Player", me ? new Color(1f, 0.72f, 0.16f, 0.16f) : (i % 2 == 0 ? Ui.PanelLight : new Color(1f, 1f, 1f, 0.03f)), Ui.RoundedSmall);
            row.raycastTarget = false;
            Ui.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f - i * 48f), new Vector2(-40f, 44f));
            var rt = row.transform;

            var dot = Ui.Panel(rt, "Car", Settings.CarColors[Online.CarOf(p)], Ui.Circle);
            dot.raycastTarget = false;
            Ui.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(24f, 24f));
            var name = Ui.Label(rt, Online.NameOf(p) + (me ? "  (you)" : ""), 26, Ui.TextColor, TextAnchor.MiddleLeft, false);
            Ui.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(360f, 40f));
            if (p.IsLeader)
            {
                var tag = Ui.Panel(rt, "Host", Ui.Accent, Ui.RoundedSmall);
                tag.raycastTarget = false;
                Ui.Place(tag.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(80f, 30f));
                Ui.Fill(Ui.Label(tag.transform, "HOST", 18, new Color(0.12f, 0.08f, 0.02f)).rectTransform);
            }

            if (!host || me) return;
            var kick = Ui.Button(rt, "Kick", 20, Ui.ButtonStyle.Normal, () => online.Kick(p));
            Ui.Place(Ui.Rect(kick.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(90f, 36f));
            Ui.ButtonLabel(kick).color = Ui.Bad;
            var promote = Ui.Button(rt, "Make host", 20, Ui.ButtonStyle.Normal, () => online.MakeHost(p));
            Ui.Place(Ui.Rect(promote.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-106f, 0f), new Vector2(130f, 36f));
        }

        void RefreshChat()
        {
            if (chatLog == null) return;
            int from = Mathf.Max(0, chat.Count - 7);
            chatLog.text = string.Join("\n", chat.GetRange(from, chat.Count - from));
        }
    }
}
