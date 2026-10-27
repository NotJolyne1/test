// Browser players for the relay server. Just drop this file into RelayNetworking.Server
// (anywhere in that folder) and run the server as normal. Nothing else to change.
//
// Browsers can't use UDP, so the web version of Racing Thing connects here over a WebSocket
// instead. This runs inside the relay server process on its own port (8080 by default) and
// also hands out the game page: put index.html next to the server exe (or in the folder you
// run it from) and everyone opens http://<server address>:8080.
//
// Rooms here are separate from the UDP rooms. Same rules as the Unity version: one player
// hosts and gets a room code, the host's room properties drive the race, the host can kick.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RelayNetworking.Server.Web
{
    public static class WebRelay
    {
        const int MaxPlayers = 8;
        const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        static readonly object Gate = new object();
        static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        static readonly HashSet<Player> Everyone = new HashSet<Player>();
        static int nextId = 1;
        static Timer? pinger;

        // starts by itself when the server starts, so Program.cs doesn't need changing
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void AutoStart() => Start(8080);

        static bool started;

        public static void Start(int port = 8080)
        {
            if (started) return;
            started = true;
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Web] Couldn't open port {port} for browser players: {e.Message}");
                return;
            }
            var t = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "WebRelay" };
            t.Start();
            pinger = new Timer(_ => PingAll(), null, 15000, 15000);
            Console.WriteLine($"[Web] Browser players: open http://<this computer's address>:{port}");
            if (FindPage() == null) Console.WriteLine("[Web] (index.html isn't next to the server, so people will need to open the game file themselves)");
        }

        static void AcceptLoop(TcpListener listener)
        {
            while (true)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch (Exception e) { Console.WriteLine($"[Web] {e.Message}"); Thread.Sleep(500); continue; }
                var t = new Thread(() => Serve(c)) { IsBackground = true };
                t.Start();
            }
        }

        // ------------------------------------------------------------ http + handshake

        static string? FindPage()
        {
            foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var p = Path.Combine(dir, "index.html");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static void Serve(TcpClient client)
        {
            client.NoDelay = true;
            NetworkStream s;
            try { s = client.GetStream(); } catch { client.Close(); return; }
            try
            {
                var head = ReadHead(s);
                if (head == null) { client.Close(); return; }
                var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var first = lines[0].Split(' ');
                string url = first.Length > 1 ? first[1] : "/";
                string? key = null, host = null;
                foreach (var l in lines)
                {
                    int i = l.IndexOf(':');
                    if (i <= 0) continue;
                    string name = l.Substring(0, i).Trim();
                    if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase)) key = l.Substring(i + 1).Trim();
                    else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)) host = l.Substring(i + 1).Trim();
                }
                if (key == null) { ServePage(s, first[0], url, host); client.Close(); return; }

                string accept;
                using (var sha = SHA1.Create())
                    accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var reply = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                s.Write(reply, 0, reply.Length);

                var p = new Player(client, s);
                lock (Gate) { p.Id = nextId++; Everyone.Add(p); }
                p.ReadLoop();
                lock (Gate)
                {
                    Everyone.Remove(p);
                    if (p.Room != null) Log($"{p.Name} disconnected");
                    LeaveRoom(p, "disconnected");
                }
            }
            catch { }
            try { client.Close(); } catch { }
        }

        static string? ReadHead(Stream s)
        {
            var buf = new List<byte>();
            int b;
            while ((b = s.ReadByte()) >= 0)
            {
                buf.Add((byte)b);
                int n = buf.Count;
                if (n >= 4 && buf[n - 4] == 13 && buf[n - 3] == 10 && buf[n - 2] == 13 && buf[n - 1] == 10) return Encoding.ASCII.GetString(buf.ToArray());
                if (n > 16384) return null;
            }
            return null;
        }

        // The page is stamped with the address it was asked for, so it still connects back here
        // when another website fetches it or shows it in a frame. Any site may fetch it.
        static void ServePage(Stream s, string method, string url, string? host)
        {
            string? page = FindPage();
            bool wanted = url == "/" || url.StartsWith("/index.html") || url.StartsWith("/?");
            byte[] body;
            string status;
            if (method == "OPTIONS") { body = Array.Empty<byte>(); status = "204 No Content"; }
            else if (wanted && page != null)
            {
                string html = File.ReadAllText(page);
                if (host != null && host.All(c => char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '-'))
                {
                    int at = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                    if (at >= 0) html = html.Insert(at + 6, "<script>window.GAME_SERVER=\"" + host + "\";</script>");
                }
                body = Encoding.UTF8.GetBytes(html);
                status = "200 OK";
            }
            else { body = Encoding.UTF8.GetBytes(page == null ? "index.html isn't next to the relay server" : ""); status = "404 Not Found"; }
            var h = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-cache\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, OPTIONS\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            s.Write(h, 0, h.Length);
            s.Write(body, 0, body.Length);
            s.Flush();
        }

        static void PingAll()
        {
            List<Player> all;
            lock (Gate) all = Everyone.ToList();
            foreach (var p in all)
            {
                if (!p.Alive) { p.Close(); continue; }
                p.Alive = false;
                p.Control(0x9, Array.Empty<byte>());
            }
        }

        static void Log(string s) => Console.WriteLine($"[Web] {DateTime.Now:HH:mm:ss}  {s}");

        // ------------------------------------------------------------ rooms

        class Room
        {
            public string Code = "";
            public int Host;
            public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
            public readonly List<int> Order = new List<int>();
            public Dictionary<string, object?> Props = new Dictionary<string, object?>();
        }

        static string NewCode()
        {
            while (true)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 5; i++) sb.Append(CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]);
                if (!Rooms.ContainsKey(sb.ToString())) return sb.ToString();
            }
        }

        static Dictionary<string, object?> Info(Player p) => new Dictionary<string, object?> { ["id"] = p.Id, ["name"] = p.Name, ["props"] = p.Props };

        static Dictionary<string, object?> Msg(string t, params (string, object?)[] kv)
        {
            var d = new Dictionary<string, object?> { ["t"] = t };
            foreach (var e in kv) d[e.Item1] = e.Item2;
            return d;
        }

        static void ToRoom(Room r, Dictionary<string, object?> msg, Player? except = null)
        {
            string data = Json.Write(msg);
            foreach (var p in r.Players.Values) if (p != except) p.SendText(data);
        }

        static void LeaveRoom(Player p, string? reason = null)
        {
            var r = p.Room;
            if (r == null) return;
            r.Players.Remove(p.Id);
            r.Order.Remove(p.Id);
            p.Room = null;
            if (r.Players.Count == 0) { Rooms.Remove(r.Code); Log($"room {r.Code} closed"); return; }
            ToRoom(r, Msg("left", ("id", p.Id), ("reason", reason ?? "left")));
            if (r.Host == p.Id)
            {
                r.Host = r.Order[0];
                ToRoom(r, Msg("host", ("id", r.Host)));
            }
        }

        static string CleanName(object? o)
        {
            var s = new string((o as string ?? "").Where(c => c >= 32 && c != '<' && c != '>').ToArray()).Trim();
            if (s.Length > 16) s = s.Substring(0, 16);
            return s.Length > 0 ? s : "Racer" + RandomNumberGenerator.GetInt32(100, 1000);
        }

        static Dictionary<string, object?> CleanChanges(object? o)
        {
            var outd = new Dictionary<string, object?>();
            if (o is Dictionary<string, object?> d)
                foreach (var kv in d) if (kv.Key.Length <= 32) outd[kv.Key] = kv.Value;
            return outd;
        }

        static void Apply(Dictionary<string, object?> target, Dictionary<string, object?> changes)
        {
            foreach (var kv in changes) { if (kv.Value == null) target.Remove(kv.Key); else target[kv.Key] = kv.Value; }
        }

        static int AsInt(object? o) => o is double d ? (int)d : 0;

        static void Handle(Player p, Dictionary<string, object?> m)
        {
            string t = m.TryGetValue("t", out var tv) ? tv as string ?? "" : "";
            if (t != "car" && !p.Allowed("msg")) return;
            var room = p.Room;
            bool isHost = room != null && room.Host == p.Id;
            m.TryGetValue("c", out var c);
            switch (t)
            {
                case "hello":
                    p.Name = CleanName(m.GetValueOrDefault("name"));
                    p.Send(Msg("welcome", ("id", p.Id), ("time", Now())));
                    break;

                case "ping":
                    p.Send(Msg("pong", ("c", c), ("s", Now())));
                    break;

                case "create":
                {
                    if (room != null) LeaveRoom(p);
                    var r = new Room { Code = NewCode(), Host = p.Id, Props = CleanChanges(m.GetValueOrDefault("props")) };
                    r.Players[p.Id] = p; r.Order.Add(p.Id);
                    Rooms[r.Code] = r;
                    p.Room = r;
                    p.Props = CleanChanges(m.GetValueOrDefault("me"));
                    p.Send(Joined(r, p));
                    Log($"{p.Name} made room {r.Code}");
                    break;
                }

                case "join":
                {
                    string code = new string(((m.GetValueOrDefault("code") as string) ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
                    if (!Rooms.TryGetValue(code, out var r)) { p.Send(Msg("error", ("what", "join"), ("reason", "No room with the code " + code + "."))); return; }
                    if (r.Players.Count >= MaxPlayers) { p.Send(Msg("error", ("what", "join"), ("reason", "That room is full."))); return; }
                    if (room != null) LeaveRoom(p);
                    p.Props = CleanChanges(m.GetValueOrDefault("me"));
                    r.Players[p.Id] = p; r.Order.Add(p.Id);
                    p.Room = r;
                    p.Send(Joined(r, p));
                    ToRoom(r, Msg("joinedRoom", ("player", Info(p))), p);
                    Log($"{p.Name} joined {r.Code} ({r.Players.Count} in it)");
                    break;
                }

                case "leave":
                    LeaveRoom(p);
                    break;

                case "kick":
                {
                    if (!isHost) return;
                    if (!room.Players.TryGetValue(AsInt(m.GetValueOrDefault("id")), out var target) || target == p) return;
                    target.Send(Msg("kicked"));
                    LeaveRoom(target, "kicked");
                    Log($"{p.Name} kicked {target.Name}");
                    break;
                }

                case "makeHost":
                {
                    int id = AsInt(m.GetValueOrDefault("id"));
                    if (!isHost || !room.Players.ContainsKey(id)) return;
                    room.Host = id;
                    ToRoom(room, Msg("host", ("id", id)));
                    break;
                }

                case "props":
                {
                    if (!isHost || !p.Allowed("props")) return;
                    var ch = CleanChanges(c);
                    Apply(room.Props, ch);
                    ToRoom(room, Msg("props", ("c", ch)));
                    break;
                }

                case "me":
                {
                    if (room == null || !p.Allowed("props")) return;
                    var ch = CleanChanges(c);
                    Apply(p.Props, ch);
                    ToRoom(room, Msg("me", ("id", p.Id), ("c", ch)));
                    break;
                }

                case "chat":
                {
                    if (room == null || !p.Allowed("chat")) return;
                    string text = (m.GetValueOrDefault("text") as string ?? "");
                    if (text.Length > 100) text = text.Substring(0, 100);
                    text = text.Trim();
                    if (text.Length > 0) ToRoom(room, Msg("chat", ("id", p.Id), ("text", text)));
                    break;
                }

                case "event":
                {
                    if (room == null) return;
                    string kind = m.GetValueOrDefault("kind") as string ?? "";
                    if (kind.Length > 20) kind = kind.Substring(0, 20);
                    ToRoom(room, Msg("event", ("from", p.Id), ("kind", kind), ("data", m.GetValueOrDefault("data"))), p);
                    break;
                }

                case "car":
                    // where this car is, about 20 times a second, straight on to everyone else
                    if (room != null) ToRoom(room, Msg("car", ("id", p.Id), ("p", m.GetValueOrDefault("p")), ("q", m.GetValueOrDefault("q")), ("c", c), ("r", m.GetValueOrDefault("r"))), p);
                    break;

                case "honk":
                    if (room != null && p.Allowed("chat")) ToRoom(room, Msg("honk", ("id", p.Id)), p);
                    break;
            }
        }

        static Dictionary<string, object?> Joined(Room r, Player you) => Msg("joined",
            ("code", r.Code), ("you", you.Id), ("host", r.Host),
            ("players", r.Order.Select(id => (object?)Info(r.Players[id])).ToList()), ("props", r.Props));

        static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ------------------------------------------------------------ one browser

        class Player
        {
            public int Id;
            public string Name = "Racer";
            public Dictionary<string, object?> Props = new Dictionary<string, object?>();
            public Room? Room;
            public volatile bool Alive = true;
            readonly TcpClient client;
            readonly Stream stream;
            readonly object writeLock = new object();
            readonly Dictionary<string, Queue<long>> windows = new Dictionary<string, Queue<long>>();

            public Player(TcpClient c, Stream s) { client = c; stream = s; }

            public bool Allowed(string kind)
            {
                int n; long ms;
                switch (kind) { case "chat": n = 5; ms = 3000; break; case "props": n = 20; ms = 1000; break; default: n = 80; ms = 1000; break; }
                long now = Environment.TickCount64;
                if (!windows.TryGetValue(kind, out var w)) windows[kind] = w = new Queue<long>();
                while (w.Count > 0 && now - w.Peek() > ms) w.Dequeue();
                if (w.Count >= n) return false;
                w.Enqueue(now);
                return true;
            }

            public void Send(Dictionary<string, object?> msg) => SendText(Json.Write(msg));

            public void SendText(string text) => Frame(0x1, Encoding.UTF8.GetBytes(text));

            public void Control(int op, byte[] payload) => Frame(op, payload);

            void Frame(int op, byte[] data)
            {
                byte[] head;
                if (data.Length < 126) head = new byte[] { (byte)(0x80 | op), (byte)data.Length };
                else if (data.Length < 65536) head = new byte[] { (byte)(0x80 | op), 126, (byte)(data.Length >> 8), (byte)data.Length };
                else
                {
                    head = new byte[10]; head[0] = (byte)(0x80 | op); head[1] = 127;
                    for (int i = 0; i < 8; i++) head[9 - i] = (byte)((long)data.Length >> (8 * i));
                }
                try
                {
                    lock (writeLock) { stream.Write(head, 0, head.Length); stream.Write(data, 0, data.Length); }
                }
                catch { Close(); }
            }

            public void Close() { try { client.Close(); } catch { } }

            bool ReadExact(byte[] buf, int count)
            {
                int got = 0;
                while (got < count)
                {
                    int n = stream.Read(buf, got, count - got);
                    if (n <= 0) return false;
                    got += n;
                }
                return true;
            }

            public void ReadLoop()
            {
                var h = new byte[8];
                var message = new MemoryStream();
                try
                {
                    while (true)
                    {
                        if (!ReadExact(h, 2)) return;
                        bool fin = (h[0] & 0x80) != 0;
                        int op = h[0] & 0x0f;
                        bool masked = (h[1] & 0x80) != 0;
                        long len = h[1] & 0x7f;
                        if (len == 126) { if (!ReadExact(h, 2)) return; len = (h[0] << 8) | h[1]; }
                        else if (len == 127) { if (!ReadExact(h, 8)) return; len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | h[i]; }
                        if (len > 65536) return;
                        var mask = new byte[4];
                        if (masked && !ReadExact(mask, 4)) return;
                        var payload = new byte[len];
                        if (!ReadExact(payload, (int)len)) return;
                        if (masked) for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i & 3];

                        if (op == 0x8) { Control(0x8, Array.Empty<byte>()); return; }
                        if (op == 0x9) { Control(0xA, payload); continue; }
                        if (op == 0xA) { Alive = true; continue; }
                        if (op == 0x1 || op == 0x0) message.Write(payload, 0, payload.Length);
                        if (!fin) continue;
                        string text = Encoding.UTF8.GetString(message.ToArray());
                        message.SetLength(0);
                        Alive = true;
                        if (Json.Read(text) is Dictionary<string, object?> m && m.ContainsKey("t"))
                        {
                            try { lock (Gate) Handle(this, m); }
                            catch (Exception e) { Log("error: " + e.Message); }
                        }
                    }
                }
                catch { }
            }
        }

        // ------------------------------------------------------------ just enough JSON

        static class Json
        {
            public static object? Read(string s)
            {
                try { int i = 0; var v = Value(s, ref i); return v; }
                catch { return null; }
            }

            static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object? Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object?>();
                    i++; Skip(s, ref i);
                    if (s[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string k = Str(s, ref i);
                        Skip(s, ref i); i++; // :
                        d[k] = Value(s, ref i);
                        Skip(s, ref i);
                        if (s[i++] == '}') return d;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object?>();
                    i++; Skip(s, ref i);
                    if (s[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (s[i++] == ']') return l;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int st = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // "
                while (true)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
            }

            public static string Write(object? v) { var sb = new StringBuilder(); W(sb, v); return sb.ToString(); }

            static void W(StringBuilder sb, object? v)
            {
                if (v == null) sb.Append("null");
                else if (v is bool) sb.Append((bool)v ? "true" : "false");
                else if (v is string) Q(sb, (string)v);
                else if (v is int) sb.Append(((int)v).ToString(CultureInfo.InvariantCulture));
                else if (v is double)
                {
                    double d = (double)v;
                    if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                    else if (d == Math.Floor(d) && Math.Abs(d) < 1e15) sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
                    else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                }
                else if (v is Dictionary<string, object?>)
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in (Dictionary<string, object?>)v) { if (!first) sb.Append(','); first = false; Q(sb, kv.Key); sb.Append(':'); W(sb, kv.Value); }
                    sb.Append('}');
                }
                else if (v is List<object?>)
                {
                    var l = (List<object?>)v;
                    sb.Append('[');
                    for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(','); W(sb, l[i]); }
                    sb.Append(']');
                }
                else Q(sb, v.ToString() ?? "");
            }

            static void Q(StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (char c in s)
                {
                    if (c == '"') sb.Append("\\\"");
                    else if (c == '\\') sb.Append("\\\\");
                    else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                }
                sb.Append('"');
            }
        }
    }
}
