using System.Collections.Generic;
using RelayNetworking.Client;
using RelayNetworking.Common.Data;
using RelayNetworking.Common.Rpc;
using RelayNetworking.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace RacingThing
{
    // Sits on the NetCar prefab, which NetworkManager uses as the player object: the server
    // spawns one for every player in the room and removes it when they leave.
    //
    // Your own copy just follows your real car so NetworkTransform has something to send.
    // Everyone else's draws a car that plays back those updates a little in the past, so it
    // moves smoothly between packets. The cars don't collide, same as ghosts.
    public class NetCar : NetworkBehaviour
    {
        static readonly Dictionary<int, NetCar> byOwner = new Dictionary<int, NetCar>();

        public static NetCar Local { get; private set; }

        // how many checkpoints the owner has been through, so the live order is right
        // even where the track passes over itself
        [NetworkVariable] int checkpoints;

        struct Snapshot
        {
            public float Time;
            public Vector3 Pos;
            public Quaternion Rot;
        }

        const float Delay = 0.12f;          // play remote cars back this far behind
        const float MaxExtrapolate = 0.3f;  // after that, hold still until the next packet

        readonly List<Snapshot> snaps = new List<Snapshot>();
        GameObject view;
        CarModel.Parts parts;
        int viewColor = -1;
        AudioSource hornSource;
        Text nameTag;
        Vector3 shownPos;
        Quaternion shownRot = Quaternion.identity;
        float wheelSpin;
        int pathHint = -1;
        double progress;
        int progressFrame = -1;
        bool started;

        public int OwnerId { get { return NetworkObject != null ? NetworkObject.OwnerPlayerId : 0; } }
        bool Mine { get { return NetworkObject != null && NetworkObject.IsMine; } }

        void Start()
        {
            // NetworkManager fills in the owner after Instantiate, so this is the first place it's known
            started = true;
            byOwner[OwnerId] = this;
            if (Mine) Local = this;
            else RelayNetwork.OnObjectTransform += OnTransform;
        }

        void OnDestroy()
        {
            if (!started) return;
            NetCar current;
            if (byOwner.TryGetValue(OwnerId, out current) && current == this) byOwner.Remove(OwnerId);
            if (Local == this) Local = null;
            RelayNetwork.OnObjectTransform -= OnTransform;
            if (view != null) Destroy(view);
            if (nameTag != null) Destroy(nameTag.gameObject);
        }

        // ------------------------------------------------------------ horn

        public void Honk()
        {
            if (!Mine) return;
            PlayHorn(null);
            SendRpc(nameof(RpcHonk), RpcTarget.Others);
        }

        [RelayRpc]
        void RpcHonk()
        {
            PlayHorn(view != null && view.activeSelf ? hornSource : null);
        }

        static void PlayHorn(AudioSource at)
        {
            var clip = Sfx.Horn;
            if (at != null) at.PlayOneShot(clip, Settings.EffectsVolume);
            else Sfx.PlayClip(clip, 0.6f);
        }

        // ------------------------------------------------------------ owner side

        protected override void Update()
        {
            base.Update();   // sends the network variable when it changes
            if (!started || !Mine) return;
            var game = Game.Instance;
            var car = game != null && game.View == Game.OnlineView.Race ? game.LocalCar : null;
            if (car == null) return;
            transform.SetPositionAndRotation(car.transform.position, car.transform.rotation);
            checkpoints = game.LocalCheckpoints;
        }

        // ------------------------------------------------------------ everyone else's car

        void OnTransform(TransformData data)
        {
            if (NetworkObject == null || data.NetworkId != NetworkObject.NetworkId) return;
            var s = new Snapshot
            {
                Time = Time.time,
                Pos = new Vector3(data.Position.X, data.Position.Y, data.Position.Z),
                Rot = new Quaternion(data.Rotation.X, data.Rotation.Y, data.Rotation.Z, data.Rotation.W),
            };
            if (snaps.Count > 0)
            {
                var last = snaps[snaps.Count - 1];
                // a big jump is a respawn, don't slide across the map to get there
                if ((s.Pos - last.Pos).sqrMagnitude > 40f * 40f) snaps.Clear();
                // packets that arrive bunched up still get spread out a bit
                else s.Time = Mathf.Max(s.Time, last.Time + 0.015f);
            }
            snaps.Add(s);
            while (snaps.Count > 2 && snaps[1].Time < Time.time - 1f) snaps.RemoveAt(0);
        }

        bool ShouldShow()
        {
            var game = Game.Instance;
            var online = Online.Instance;
            if (game == null || online == null || !online.InRoom) return false;
            return game.View == Game.OnlineView.Race && snaps.Count > 0 && online.Race.IsRacer(OwnerId);
        }

        void LateUpdate()
        {
            if (!started || Mine) return;
            bool show = ShouldShow();
            if (!show)
            {
                if (view != null && view.activeSelf) view.SetActive(false);
                if (nameTag != null && nameTag.gameObject.activeSelf) nameTag.gameObject.SetActive(false);
                return;
            }

            var owner = RelayNetwork.GetPlayer(OwnerId);
            int color = Online.CarOf(owner);
            if (view == null || viewColor != color) BuildView(color);
            if (!view.activeSelf) view.SetActive(true);

            Sample(Time.time - Delay, out shownPos, out shownRot);
            Vector3 moved = shownPos - view.transform.position;
            view.transform.SetPositionAndRotation(shownPos, shownRot);

            // roll the wheels by however far the car went
            float forward = Vector3.Dot(moved, shownRot * Vector3.forward);
            wheelSpin = Mathf.Repeat(wheelSpin + forward / CarModel.WheelRadius * Mathf.Rad2Deg, 360f);
            foreach (var w in parts.Wheels) w.localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);

            PlaceTag(Online.NameOf(owner), Settings.CarColors[color]);
        }

        void Sample(float t, out Vector3 pos, out Quaternion rot)
        {
            int n = snaps.Count;
            if (n == 1 || t <= snaps[0].Time)
            {
                pos = snaps[0].Pos;
                rot = snaps[0].Rot;
                return;
            }
            for (int i = 0; i < n - 1; i++)
            {
                var a = snaps[i];
                var b = snaps[i + 1];
                if (t > b.Time) continue;
                float k = Mathf.InverseLerp(a.Time, b.Time, t);
                pos = Vector3.LerpUnclamped(a.Pos, b.Pos, k);
                rot = Quaternion.SlerpUnclamped(a.Rot, b.Rot, k);
                return;
            }
            // past the newest packet: keep going the way it was heading for a moment
            var p = snaps[n - 2];
            var q = snaps[n - 1];
            float span = Mathf.Max(0.001f, q.Time - p.Time);
            float ahead = Mathf.Min(t - q.Time, MaxExtrapolate);
            pos = q.Pos + (q.Pos - p.Pos) * (ahead / span);
            rot = q.Rot;
        }

        void BuildView(int color)
        {
            if (view != null) Destroy(view);
            viewColor = color;
            view = new GameObject("Remote car " + OwnerId);
            parts = CarModel.Build(view.transform, Settings.CarColors[color], Materials.CarPaint, Materials.CarMatte);
            view.transform.SetPositionAndRotation(shownPos, shownRot);

            hornSource = view.AddComponent<AudioSource>();
            hornSource.playOnAwake = false;
            hornSource.spatialBlend = 1f;
            hornSource.minDistance = 8f;
            hornSource.maxDistance = 180f;
            hornSource.rolloffMode = AudioRolloffMode.Linear;
        }

        void PlaceTag(string text, Color color)
        {
            var cam = Camera.main;
            if (nameTag == null)
            {
                var hud = Game.Instance != null ? Game.Instance.TagLayer : null;
                if (hud == null) return;
                nameTag = Ui.Label(hud, text, 22, Color.white);
                nameTag.rectTransform.sizeDelta = new Vector2(300f, 30f);
                Ui.AddOutline(nameTag, new Color(0f, 0f, 0f, 0.6f), 2f);
            }
            if (cam == null) return;
            Vector3 head = shownPos + shownRot * Vector3.up * 1.9f;
            Vector3 screen = cam.WorldToScreenPoint(head);
            float far = Vector3.Distance(cam.transform.position, head);
            bool on = screen.z > 0.5f && far < 260f;
            if (nameTag.gameObject.activeSelf != on) nameTag.gameObject.SetActive(on);
            if (!on) return;

            var canvas = nameTag.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            nameTag.rectTransform.anchorMin = nameTag.rectTransform.anchorMax = Vector2.zero;
            nameTag.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y) / scale;
            nameTag.text = text;
            var c = Color.Lerp(color, Color.white, 0.45f);
            c.a = Mathf.Clamp01(1.4f - far / 200f);
            nameTag.color = c;
        }

        // ------------------------------------------------------------ race order

        double Progress()
        {
            if (progressFrame == Time.frameCount) return progress;
            progressFrame = Time.frameCount;
            var game = Game.Instance;
            var world = game != null ? game.World : null;
            if (world == null || snaps.Count == 0)
            {
                progress = 0.0;
                return progress;
            }
            var path = world.Path;
            Vector3 at = view != null && view.activeSelf ? shownPos : snaps[snaps.Count - 1].Pos;
            int i = path.Nearest(at, pathHint, 40);
            if ((path.Pos[i] - at).sqrMagnitude > 50f * 50f) i = path.Nearest(at, -1, 0);
            pathHint = i;
            progress = checkpoints * 1000000.0 + path.Dist[i];
            return progress;
        }

        public static double ProgressOf(int playerId)
        {
            NetCar car;
            return byOwner.TryGetValue(playerId, out car) ? car.Progress() : 0.0;
        }
    }
}
