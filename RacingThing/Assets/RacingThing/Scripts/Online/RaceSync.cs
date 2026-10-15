using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RelayNetworking.Client;
using RelayNetworking.Common;
using UnityEngine;

namespace RacingThing
{
    // Keeps everyone in a room on the same race. The host writes the state into room
    // properties (so people who join late, or a new host, can always read it back) and
    // everyone else just follows along:
    //
    //   lobby -> loading (everyone builds the track) -> race (shared GO time) -> results -> lobby
    //
    // Finish times go out as events. Every client keeps the list, so if the host drops out
    // mid-race whoever takes over can still hand out the results.
    public class RaceSync
    {
        public const string TrackKey = "track";
        public const string PhaseKey = "phase";
        public const string RaceKey = "race";
        public const string RacersKey = "racers";
        public const string GoKey = "go";
        public const string EndKey = "end";
        public const string ResultsKey = "results";

        public const string Lobby = "lobby";
        public const string Loading = "loading";
        public const string Racing = "race";
        public const string Done = "results";

        public const float Countdown = 4f;        // GO comes this long after the last car has loaded
        public const float FinishWindow = 30f;    // time everyone else gets once someone finishes
        const float LoadTimeout = 25f;            // stop waiting for slow loaders
        const float ResultsLinger = 45f;          // host heads back to the lobby on its own after this
        const float MaxRaceTime = 15f * 60f;

        public struct Result
        {
            public int PlayerId;
            public string Name;
            public int Car;
            public int Ms;          // -1 if they didn't finish
            public bool Finished { get { return Ms >= 0; } }
        }

        public struct Standing
        {
            public int PlayerId;
            public string Name;
            public int Car;
            public bool IsMe;
            public bool Finished;
            public int Ms;
            public double Progress;
        }

        // someone crossed the line: player id, name, finishing position (1 = first), time in ms
        public event Action<int, string, int, int> SomeoneFinished;

        readonly Online online;
        readonly Game game;

        readonly Dictionary<int, int> finishes = new Dictionary<int, int>();
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, int> cars = new Dictionary<int, int>();
        readonly List<int> racers = new List<int>();
        readonly List<Result> results = new List<Result>();
        string racersRaw, resultsRaw;

        int loadingFor = -1;     // race id whose track we've started building
        int loadedFor = -1;      // race id whose track is ready here
        int goSentFor = -1, endSentFor = -1, resultsSentFor = -1;
        string lastPhase;
        float phaseTime;

        public RaceSync(Online online, Game game)
        {
            this.online = online;
            this.game = game;
        }

        // ------------------------------------------------------------ what the room says

        public string Phase { get { return online.RoomProperty(PhaseKey) as string ?? Lobby; } }
        public int RaceId { get { return Int(RaceKey, 0); } }
        public double GoTime { get { return Double(GoKey); } }
        public double EndTime { get { return Double(EndKey); } }
        public static double Now { get { return RelayNetwork.ServerTime; } }

        public int Track
        {
            get { return Mathf.Clamp(Int(TrackKey, 0), 0, Mathf.Max(0, TrackList.All.Count - 1)); }
        }

        public IReadOnlyList<int> Racers
        {
            get
            {
                string raw = online.RoomProperty(RacersKey) as string ?? "";
                if (raw != racersRaw)
                {
                    racersRaw = raw;
                    racers.Clear();
                    foreach (string part in raw.Split(','))
                    {
                        int id;
                        if (int.TryParse(part, out id)) racers.Add(id);
                    }
                }
                return racers;
            }
        }

        public IReadOnlyList<Result> Results
        {
            get
            {
                string raw = online.RoomProperty(ResultsKey) as string ?? "";
                if (raw != resultsRaw)
                {
                    resultsRaw = raw;
                    results.Clear();
                    foreach (string entry in raw.Split(';'))
                    {
                        string[] f = entry.Split('|');
                        if (f.Length < 4) continue;
                        var r = new Result();
                        int.TryParse(f[0], out r.PlayerId);
                        int.TryParse(f[1], out r.Ms);
                        int.TryParse(f[2], out r.Car);
                        r.Name = f[3];
                        results.Add(r);
                    }
                }
                return results;
            }
        }

        public bool IsRacer(int playerId)
        {
            var list = Racers;
            for (int i = 0; i < list.Count; i++) if (list[i] == playerId) return true;
            return false;
        }

        public bool ImRacing { get { return online.Me != null && IsRacer(online.Me.PlayerId); } }

        // where on the starting line this player goes
        public int SlotOf(int playerId)
        {
            var list = Racers;
            for (int i = 0; i < list.Count; i++) if (list[i] == playerId) return i;
            return 0;
        }

        public bool HasFinished(int playerId) { return finishes.ContainsKey(playerId); }
        public int FinishedCount { get { return finishes.Count; } }

        int Int(string key, int fallback)
        {
            object v = online.RoomProperty(key);
            return v is int ? (int)v : fallback;
        }

        double Double(string key)
        {
            object v = online.RoomProperty(key);
            if (v is double) return (double)v;
            if (v is float) return (float)v;
            return 0.0;
        }

        // ------------------------------------------------------------ host controls

        public bool CanStart { get { return online.IsHost && Phase == Lobby && TrackList.All.Count > 0; } }

        public void PickTrack(int index)
        {
            if (!online.IsHost || Phase != Lobby) return;
            int n = TrackList.All.Count;
            index = (index % n + n) % n;
            online.SetRoomProperties(c => c[TrackKey] = index);
            PlayerPrefs.SetInt("rt_last_track", index);
        }

        public void StartRace()
        {
            if (!CanStart) return;
            var ids = new StringBuilder();
            foreach (var p in online.Players)
            {
                if (ids.Length > 0) ids.Append(',');
                ids.Append(p.PlayerId);
            }
            int next = RaceId + 1;
            online.SetRoomProperties(c =>
            {
                c[PhaseKey] = Loading;
                c[RaceKey] = next;
                c[RacersKey] = ids.ToString();
                c[GoKey] = 0.0;
                c[EndKey] = 0.0;
                c[ResultsKey] = null;
            });
        }

        public void BackToLobby()
        {
            if (!online.IsHost || Phase == Lobby) return;
            online.SetRoomProperties(c => c[PhaseKey] = Lobby);
        }

        // ------------------------------------------------------------ finishing

        public void ReportFinish(float seconds)
        {
            var me = online.Me;
            if (me == null || finishes.ContainsKey(me.PlayerId)) return;
            int ms = Mathf.RoundToInt(seconds * 1000f);
            Record(me.PlayerId, ms);
            string payload = RaceId.ToString(CultureInfo.InvariantCulture) + ":" + ms.ToString(CultureInfo.InvariantCulture);
            RelayNetwork.SendEvent(Online.FinishEvent, Encoding.UTF8.GetBytes(payload), RelayNetworking.Common.Protocol.EventTarget.Others);
        }

        public void FinishReceived(int playerId, string payload)
        {
            string[] parts = payload.Split(':');
            int race, ms;
            if (parts.Length != 2 || !int.TryParse(parts[0], out race) || !int.TryParse(parts[1], out ms)) return;
            if (race != RaceId || !IsRacer(playerId) || finishes.ContainsKey(playerId)) return;
            Record(playerId, ms);
        }

        void Record(int playerId, int ms)
        {
            finishes[playerId] = ms;
            var p = RelayNetwork.GetPlayer(playerId);
            if (p != null)
            {
                names[playerId] = Online.NameOf(p);
                cars[playerId] = Online.CarOf(p);
            }
            int place = 1;
            foreach (var other in finishes) if (other.Key != playerId && other.Value < ms) place++;
            if (SomeoneFinished != null) SomeoneFinished(playerId, NameFor(playerId), place, ms);
        }

        string NameFor(int playerId)
        {
            var p = RelayNetwork.GetPlayer(playerId);
            if (p != null) return Online.NameOf(p);
            string n;
            return names.TryGetValue(playerId, out n) ? n : "Player " + playerId;
        }

        int CarFor(int playerId)
        {
            var p = RelayNetwork.GetPlayer(playerId);
            if (p != null) return Online.CarOf(p);
            int c;
            return cars.TryGetValue(playerId, out c) ? c : 0;
        }

        // ------------------------------------------------------------ live order

        readonly List<Standing> standings = new List<Standing>();

        public List<Standing> Standings()
        {
            standings.Clear();
            var me = online.Me;
            foreach (int id in Racers)
            {
                var p = RelayNetwork.GetPlayer(id);
                int ms;
                bool done = finishes.TryGetValue(id, out ms);
                if (p == null && !done) continue;   // left before finishing
                var s = new Standing
                {
                    PlayerId = id,
                    Name = NameFor(id),
                    Car = CarFor(id),
                    IsMe = me != null && me.PlayerId == id,
                    Finished = done,
                    Ms = done ? ms : -1,
                };
                if (!done) s.Progress = s.IsMe ? game.LocalProgress() : NetCar.ProgressOf(id);
                standings.Add(s);
            }
            standings.Sort((a, b) =>
            {
                if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
                if (a.Finished) return a.Ms.CompareTo(b.Ms);
                return b.Progress.CompareTo(a.Progress);
            });
            return standings;
        }

        public int MyPlace()
        {
            var list = Standings();
            for (int i = 0; i < list.Count; i++) if (list[i].IsMe) return i + 1;
            return 0;
        }

        // ------------------------------------------------------------ keeping up

        public void Reset()
        {
            finishes.Clear();
            names.Clear();
            cars.Clear();
            racersRaw = resultsRaw = null;
            racers.Clear();
            results.Clear();
            loadingFor = loadedFor = -1;
            goSentFor = endSentFor = resultsSentFor = -1;
            lastPhase = null;
            phaseTime = 0f;
        }

        public void RoomChanged()
        {
            // a new race wipes the old finishes
            if (Phase == Loading && loadingFor != RaceId && finishes.Count > 0) finishes.Clear();
        }

        public void PlayerLeft(RelayPlayer p)
        {
            if (p == null) return;
            names[p.PlayerId] = Online.NameOf(p);
            cars[p.PlayerId] = Online.CarOf(p);
        }

        public void Update(float dt)
        {
            string phase = Phase;
            if (phase != lastPhase)
            {
                lastPhase = phase;
                phaseTime = 0f;
            }
            phaseTime += dt;

            Follow(phase);
            if (online.IsHost) Lead(phase);
        }

        // what every client does
        void Follow(string phase)
        {
            int race = RaceId;
            bool active = phase == Loading || phase == Racing;

            if (active && ImRacing && loadingFor != race)
            {
                if (game.Loading) return;   // still building the menu track, start ours after
                loadingFor = race;
                finishes.Clear();
                game.LoadOnlineTrack(Track, () => TrackReady(race));
                return;
            }

            if (phase == Lobby && game.View != Game.OnlineView.Lobby && !game.Loading)
            {
                game.ShowLobby();
                return;
            }
            // sit on the grid as soon as the track is built; the countdown starts once the host sets GO
            if (active && loadedFor == race && ImRacing && game.View != Game.OnlineView.Race && !game.Loading)
                game.BeginOnlineRace(SlotOf(online.Me.PlayerId));
        }

        void TrackReady(int race)
        {
            if (!online.InRoom) return;
            if (race != RaceId || Phase == Lobby)
            {
                game.ShowLobby();
                return;
            }
            loadedFor = race;
            online.SetMyProperty(Online.LoadedKey, race);
        }

        // what the host does on top
        void Lead(string phase)
        {
            int race = RaceId;
            if (phase == Loading && goSentFor != race)
            {
                if (EveryoneLoaded(race) || phaseTime > LoadTimeout)
                {
                    goSentFor = race;
                    double go = Now + Countdown;
                    online.SetRoomProperties(c =>
                    {
                        c[PhaseKey] = Racing;
                        c[GoKey] = go;
                    });
                }
            }
            else if (phase == Racing && resultsSentFor != race)
            {
                if (finishes.Count > 0 && EndTime <= 0.0 && endSentFor != race)
                {
                    endSentFor = race;
                    double end = Now + FinishWindow;
                    online.SetRoomProperties(c => c[EndKey] = end);
                }

                int left = 0;
                foreach (int id in Racers)
                    if (RelayNetwork.GetPlayer(id) != null && !finishes.ContainsKey(id)) left++;

                bool timeUp = EndTime > 0.0 && Now > EndTime;
                if (left == 0 || timeUp || phaseTime > MaxRaceTime)
                {
                    resultsSentFor = race;
                    string table = BuildResults();
                    online.SetRoomProperties(c =>
                    {
                        c[PhaseKey] = Done;
                        c[ResultsKey] = table;
                    });
                }
            }
            else if (phase == Done && phaseTime > ResultsLinger)
            {
                BackToLobby();
            }
        }

        bool EveryoneLoaded(int race)
        {
            foreach (int id in Racers)
            {
                var p = RelayNetwork.GetPlayer(id);
                if (p == null) continue;
                object v = p.Properties[Online.LoadedKey];
                if (!(v is int) || (int)v != race) return false;
            }
            return true;
        }

        string BuildResults()
        {
            var rows = new List<Result>();
            foreach (var f in finishes)
                rows.Add(new Result { PlayerId = f.Key, Ms = f.Value, Name = NameFor(f.Key), Car = CarFor(f.Key) });
            rows.Sort((a, b) => a.Ms.CompareTo(b.Ms));
            foreach (int id in Racers)
                if (!finishes.ContainsKey(id) && RelayNetwork.GetPlayer(id) != null)
                    rows.Add(new Result { PlayerId = id, Ms = -1, Name = NameFor(id), Car = CarFor(id) });

            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                if (sb.Length > 0) sb.Append(';');
                string name = r.Name.Replace("|", "").Replace(";", "");
                sb.Append(r.PlayerId).Append('|').Append(r.Ms).Append('|').Append(r.Car).Append('|').Append(name);
            }
            return sb.ToString();
        }
    }
}
