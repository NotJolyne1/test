using System;
using System.Collections.Generic;
using System.Text;
using RelayNetworking.Client;
using RelayNetworking.Client.Transport;
using RelayNetworking.Common;
using RelayNetworking.Common.Callbacks;
using RelayNetworking.Common.Protocol;
using RelayNetworking.Common.Results;
using RelayNetworking.Common.Rooms;
using UnityEngine;

namespace RacingThing
{
    // Everything that talks to the relay server: connecting, signing in, the room you're in
    // and who else is in it. The race itself (start, countdown, finishes) is in RaceSync.
    //
    // The room code is just the room name on the server. One person hosts (makes the room),
    // everyone else types the code in. The server's "leader" is the host.
    public class Online : MonoBehaviour
    {
        public const string AppId = "racing-thing";
        public const int MaxPlayers = 8;
        public const int DefaultPort = 7777;

        // event codes. 1 is used by the RelayNetworking test window, so start higher
        public const byte ChatEvent = 10;
        public const byte FinishEvent = 11;

        // player properties
        public const string CarKey = "car";
        public const string LoadedKey = "loaded";

        public enum Status { Offline, Connecting, SigningIn, Joining, InRoom }

        public static Online Instance { get; private set; }

        public Status State { get; private set; }
        public string Code { get; private set; }
        public string ServerAddress { get; private set; }
        public bool Reconnecting { get; private set; }
        public int Ping { get; private set; }
        public RaceSync Race { get; private set; }

        public bool InRoom { get { return State == Status.InRoom && RelayNetwork.CurrentRoom != null; } }
        // false while a "Make host" is on its way, so a second click can't send a leader-only
        // command after the server has already taken the role away (it disconnects you for that)
        public bool IsHost { get { return InRoom && RelayNetwork.IsLeader && handingOver <= 0f; } }
        public RelayPlayer Me { get { return RelayNetwork.LocalPlayer; } }
        public IReadOnlyList<RelayPlayer> Players { get { return RelayNetwork.ActivePlayers; } }
        // RelayNetwork.Leader is only filled in after a leader change, so look at the players instead
        public RelayPlayer Host
        {
            get
            {
                var list = RelayNetwork.ActivePlayers;
                for (int i = 0; i < list.Count; i++) if (list[i].IsLeader) return list[i];
                return null;
            }
        }

        // the lobby redraws on this
        public event Action Changed;
        // lines for the lobby chat box: text, colour
        public event Action<string, Color> Line;
        // we're out of the room for good, with a reason to show (null if we left on purpose)
        public event Action<string> Dropped;

        const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        Game game;
        bool creating;
        int createTries;
        float waitTimer;
        bool leaving;           // we started the disconnect ourselves
        bool dropAfterPoll;     // got told to leave while the network was mid-poll
        string dropReason;
        float pingTimer, chatCooldown, handingOver;

        public void Init(Game game)
        {
            Instance = this;
            this.game = game;
            Race = new RaceSync(this, game);
        }

        void OnEnable()
        {
            RelayCallbacks.Connected += OnConnected;
            RelayCallbacks.Authenticated += OnAuthenticated;
            RelayCallbacks.RoomCreated += OnRoomEntered;
            RelayCallbacks.RoomJoined += OnRoomEntered;
            RelayCallbacks.RoomJoinFailed += OnJoinFailed;
            RelayCallbacks.RoomCreationFailed += OnCreateFailed;
            RelayCallbacks.PlayerJoined += OnPlayerJoined;
            RelayCallbacks.PlayerLeft += OnPlayerLeft;
            RelayCallbacks.LeaderChanged += OnLeaderChanged;
            RelayCallbacks.PlayerPropertiesChanged += OnPlayerProperties;
            RelayCallbacks.RoomPropertiesChanged += OnRoomProperties;
            RelayCallbacks.PingUpdated += OnPing;
            RelayNetwork.OnDisconnected += OnDisconnected;
            RelayNetwork.OnReconnecting += OnReconnecting;
            RelayNetwork.OnReconnected += OnReconnected;
            RelayNetwork.OnEvent += OnEvent;
        }

        void OnDisable()
        {
            RelayCallbacks.Connected -= OnConnected;
            RelayCallbacks.Authenticated -= OnAuthenticated;
            RelayCallbacks.RoomCreated -= OnRoomEntered;
            RelayCallbacks.RoomJoined -= OnRoomEntered;
            RelayCallbacks.RoomJoinFailed -= OnJoinFailed;
            RelayCallbacks.RoomCreationFailed -= OnCreateFailed;
            RelayCallbacks.PlayerJoined -= OnPlayerJoined;
            RelayCallbacks.PlayerLeft -= OnPlayerLeft;
            RelayCallbacks.LeaderChanged -= OnLeaderChanged;
            RelayCallbacks.PlayerPropertiesChanged -= OnPlayerProperties;
            RelayCallbacks.RoomPropertiesChanged -= OnRoomProperties;
            RelayCallbacks.PingUpdated -= OnPing;
            RelayNetwork.OnDisconnected -= OnDisconnected;
            RelayNetwork.OnReconnecting -= OnReconnecting;
            RelayNetwork.OnReconnected -= OnReconnected;
            RelayNetwork.OnEvent -= OnEvent;
        }

        // ------------------------------------------------------------ names and codes

        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? "").Trim())
            {
                if (char.IsControl(c) || c == ':' || c == ';' || c == '|' || c == '<' || c == '>') continue;
                sb.Append(c);
                if (sb.Length >= 16) break;
            }
            return sb.ToString().Trim();
        }

        public static string CleanCode(string code)
        {
            var sb = new StringBuilder();
            foreach (char c in (code ?? "").ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 8 ? sb.ToString(0, 8) : sb.ToString();
        }

        static string NewCode()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append(CodeLetters[UnityEngine.Random.Range(0, CodeLetters.Length)]);
            return sb.ToString();
        }

        public static string NameOf(RelayPlayer p)
        {
            if (p == null) return "Someone";
            return string.IsNullOrEmpty(p.Username) ? "Player " + p.PlayerId : p.Username;
        }

        public static int CarOf(RelayPlayer p)
        {
            object v = p != null ? p.Properties[CarKey] : null;
            int car = v is int ? (int)v : 0;
            return Mathf.Clamp(car, 0, Settings.CarColors.Length - 1);
        }

        // ------------------------------------------------------------ connecting

        public bool NetworkReady { get { return FindObjectOfType<NetworkManager>() != null; } }

        public void HostGame(string name, string server)
        {
            Begin(name, server, NewCode(), true);
        }

        public void JoinGame(string name, string server, string code)
        {
            Begin(name, server, CleanCode(code), false);
        }

        void Begin(string name, string server, string code, bool create)
        {
            if (State != Status.Offline) Shutdown(null);

            if (!NetworkReady)
            {
                Fail("Networking isn't set up in this scene. In Unity use Racing Thing > Fix Project Setup.");
                return;
            }

            string address;
            int port;
            SplitAddress(server, out address, out port);
            ServerAddress = address + (port != DefaultPort ? ":" + port : "");
            Code = code;
            creating = create;
            createTries = 0;

            // the server signs people in by name only, no passwords
            RelayNetwork.Authentication.Type = AuthenticationType.None;
            RelayNetwork.Authentication.Username = CleanName(name);
            RelayNetwork.EnableDropConnection = true;   // lets the host kick us
            RelayNetwork.Logger = Log;

            SetState(Status.Connecting, 10f);
            try
            {
                RelayNetwork.Connect(new RelayClientOptions { Host = address, Port = port });
            }
            catch (Exception e)
            {
                Debug.LogWarning("Connect failed: " + e.Message);
                Fail("Couldn't find a server called " + address + ".");
            }
        }

        // the client logs every ping, which buries everything else in the console
        static void Log(string text)
        {
            if (Debug.isDebugBuild && !text.Contains("Ping")) Debug.Log(text);
        }

        public static void SplitAddress(string server, out string address, out int port)
        {
            address = (server ?? "").Trim();
            port = DefaultPort;
            int colon = address.LastIndexOf(':');
            // a single colon means host:port, more than one is probably an IPv6 address
            if (colon > 0 && address.IndexOf(':') == colon)
            {
                int p;
                if (int.TryParse(address.Substring(colon + 1), out p) && p > 0 && p < 65536) port = p;
                address = address.Substring(0, colon);
            }
            if (address.Length == 0) address = DefaultServer;
        }

        // whatever the RelayNetworking client is set up to use out of the box
        public static string DefaultServer { get { return new RelayClientOptions().Host; } }

        void SetState(Status s, float wait = 0f)
        {
            State = s;
            waitTimer = wait;
            Changed?.Invoke();
        }

        void OnConnected()
        {
            // also fires during an automatic reconnect, which signs itself back in
            if (State != Status.Connecting) return;
            SetState(Status.SigningIn, 8f);
            RelayNetwork.Authenticate(AppId);
        }

        void OnAuthenticated(RelayPlayer me)
        {
            if (State != Status.SigningIn) return;
            SetState(Status.Joining, 8f);
            if (creating) MakeRoom();
            else RelayNetwork.JoinRoom(Code);
        }

        void MakeRoom()
        {
            createTries++;
            var options = new RoomOptions { MaxPlayers = MaxPlayers, IsOpen = true, IsVisible = false };
            options.CustomProperties[RaceSync.TrackKey] = Mathf.Clamp(PlayerPrefs.GetInt("rt_last_track", 0), 0, TrackList.All.Count - 1);
            options.CustomProperties[RaceSync.PhaseKey] = RaceSync.Lobby;
            RelayNetwork.CreateRoom(Code, options);
        }

        void OnCreateFailed(string reason)
        {
            if (State != Status.Joining) return;
            Fail(reason == "rate limited" ? "Made too many rooms too fast. Wait a minute and try again." : "The server couldn't make a room (" + reason + ").");
        }

        void OnJoinFailed(JoinRoomResult result)
        {
            if (State != Status.Joining) return;
            switch (result)
            {
                case JoinRoomResult.RoomNotFound: Fail("There's no room with the code " + Code + "."); break;
                case JoinRoomResult.RoomFull: Fail("That room is full."); break;
                case JoinRoomResult.RoomClosed: Fail("That room isn't letting anyone in."); break;
                default: Fail("Couldn't join " + Code + " (" + result + ")."); break;
            }
        }

        void OnRoomEntered(Room room)
        {
            if (State != Status.Joining) return;
            Code = room.Name;
            SetState(Status.InRoom);
            Race.Reset();
            ShareCar();
            game.ShowLobby();
            Say(creating ? "Room made. Send your friends the code " + Code + "." : "You joined " + Code + ".", Ui.TextDim);
        }

        void Fail(string why)
        {
            Shutdown(why);
        }

        // ------------------------------------------------------------ leaving

        public void Leave()
        {
            Shutdown(null);
        }

        void Shutdown(string reason)
        {
            bool wasOnline = State != Status.Offline;
            leaving = true;
            if (RelayNetwork.Connected || wasOnline) RelayNetwork.Disconnect();
            leaving = false;
            dropAfterPoll = false;
            Reconnecting = false;
            Code = null;
            Race.Reset();
            SetState(Status.Offline);
            Dropped?.Invoke(reason);
        }

        void OnDisconnected()
        {
            if (leaving || State == Status.Offline) return;

            // The client also raises this when it's been told to leave a room but the
            // connection is still up. That only happens when the host kicks us.
            string reason;
            if (RelayNetwork.Connected && State == Status.InRoom) reason = "The host kicked you from the room.";
            else if (State == Status.Connecting) reason = "Couldn't reach the server at " + ServerAddress + ". Is it running?";
            else if (State == Status.SigningIn) reason = "The server didn't let us in.";
            else reason = "Lost the connection to the server.";

            // we're inside the network poll right now, so do the actual disconnect next frame
            dropAfterPoll = true;
            dropReason = reason;
        }

        void OnReconnecting()
        {
            Reconnecting = true;
            Changed?.Invoke();
        }

        void OnReconnected()
        {
            Reconnecting = false;
            Say("Reconnected.", Ui.TextDim);
            ShareCar();
            Race.Reconnected();
            Changed?.Invoke();
        }

        void OnApplicationQuit()
        {
            leaving = true;
        }

        // ------------------------------------------------------------ the room

        void OnPlayerJoined(RelayPlayer p)
        {
            if (!InRoom) return;
            Say(NameOf(p) + " joined.", Ui.Good);
            if (game.View != Game.OnlineView.Race) Sfx.Play(Sound.Checkpoint, 0.5f);
            // New players only get names when they join, not everyone's properties,
            // so say our car colour again for their benefit.
            ShareCar();
            Changed?.Invoke();
        }

        void OnPlayerLeft(RelayPlayer p)
        {
            if (!InRoom) return;
            Say(NameOf(p) + " left.", Ui.TextDim);
            Race.PlayerLeft(p);
            Changed?.Invoke();
        }

        void OnLeaderChanged(RelayPlayer p)
        {
            handingOver = 0f;
            if (!InRoom) return;
            Say(p == Me ? "You're the host now." : NameOf(p) + " is the host now.", Ui.Accent);
            Changed?.Invoke();
        }

        void OnPlayerProperties(RelayPlayer p)
        {
            if (InRoom) Changed?.Invoke();
        }

        void OnRoomProperties(Room room)
        {
            if (!InRoom) return;
            Race.RoomChanged();
            Changed?.Invoke();
        }

        void OnPing(int ms)
        {
            Ping = ms;
        }

        public void ShareCar()
        {
            if (!InRoom) return;
            var changes = new PropertyChanges();
            changes[CarKey] = Settings.CarColor;
            RelayNetwork.UpdateProperties(changes);
        }

        public void SetMyProperty(string key, object value)
        {
            if (!InRoom) return;
            var changes = new PropertyChanges();
            changes[key] = value;
            RelayNetwork.UpdateProperties(changes);
        }

        // false if it couldn't be sent (not host, or the connection is mid-reconnect)
        public bool SetRoomProperties(Action<PropertyChanges> build)
        {
            if (!IsHost || !RelayNetwork.Connected) return false;
            var changes = new PropertyChanges();
            build(changes);
            return RelayNetwork.UpdateRoomProperties(changes);
        }

        public object RoomProperty(string key)
        {
            var room = RelayNetwork.CurrentRoom;
            return room != null ? room.Properties[key] : null;
        }

        public void Kick(RelayPlayer p)
        {
            if (!IsHost || p == null || p == Me) return;
            if (RelayNetwork.DropConnection(p)) Say("You kicked " + NameOf(p) + ".", Ui.TextDim);
        }

        public void MakeHost(RelayPlayer p)
        {
            if (!IsHost || p == null || p == Me) return;
            if (RelayNetwork.TransferLeader(p))
            {
                handingOver = 4f;
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------ chat and events

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || !InRoom || chatCooldown > 0f) return;
            if (text.Length > 100) text = text.Substring(0, 100);
            chatCooldown = 0.4f;
            RelayNetwork.SendEvent(ChatEvent, Encoding.UTF8.GetBytes(text), EventTarget.All);
        }

        public void Say(string text, Color color)
        {
            Line?.Invoke(text, color);
        }

        void OnEvent(RelayEvent e)
        {
            if (!InRoom) return;
            string text = e.Payload != null ? Encoding.UTF8.GetString(e.Payload) : "";
            switch (e.EventCode)
            {
                case ChatEvent:
                    var from = e.Player;
                    Color c = Settings.CarColors[CarOf(from)];
                    Say("<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(c, Color.white, 0.35f)) + ">" + NameOf(from) + "</color>  " + Escape(text), Ui.TextColor);
                    break;
                case FinishEvent:
                    Race.FinishReceived(e.SenderPlayerId, text);
                    break;
            }
        }

        // uGUI rich text would happily render <color> tags that people type in
        static string Escape(string s)
        {
            return s.Replace("<", "<​");
        }

        // ------------------------------------------------------------ frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (chatCooldown > 0f) chatCooldown -= dt;
            if (handingOver > 0f && (handingOver -= dt) <= 0f) Changed?.Invoke();

            if (dropAfterPoll)
            {
                dropAfterPoll = false;
                Shutdown(dropReason);
                return;
            }

            if (waitTimer > 0f && State != Status.InRoom && State != Status.Offline)
            {
                waitTimer -= dt;
                if (waitTimer <= 0f) TimedOut();
            }

            if (!InRoom) return;
            if ((pingTimer -= dt) <= 0f)
            {
                pingTimer = 3f;
                RelayNetwork.Ping();
            }
            Race.Update(dt);
        }

        void TimedOut()
        {
            switch (State)
            {
                case Status.Connecting:
                    Fail("Couldn't reach the server at " + ServerAddress + ". Is it running?");
                    break;
                case Status.SigningIn:
                    Fail("The server never answered the sign in.");
                    break;
                case Status.Joining:
                    // the server doesn't say anything when a room name is already taken, so try another code
                    if (creating && createTries < 3)
                    {
                        Code = NewCode();
                        waitTimer = 8f;
                        MakeRoom();
                    }
                    else Fail(creating ? "The server didn't make the room." : "The server never answered.");
                    break;
            }
        }
    }
}
