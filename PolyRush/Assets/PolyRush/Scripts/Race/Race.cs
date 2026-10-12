using UnityEngine;

namespace PolyRush
{
    // One time-trial session. The clock starts the moment you touch a drive key,
    // R restarts instantly, Enter puts you back at the last checkpoint with the speed you had.
    public class Race : MonoBehaviour
    {
        public enum State { Ready, Driving, Finished }

        public State Phase { get; private set; }
        public float Clock { get; private set; }
        public int CheckpointsHit { get; private set; }
        public int CheckpointCount { get { return path.Checkpoints.Count; } }
        public bool Paused { get; private set; }
        public CarController Car { get { return car; } }
        public TrackWorld World { get { return world; } }

        TrackWorld world;
        TrackPath path;
        ChaseCam cam;
        Hud hud;
        Game game;

        CarController car;
        CarFx fx;
        readonly KeyboardInput keys = new KeyboardInput();
        readonly BrakeInput coast = new BrakeInput();

        bool[] taken;
        float[] splits, bestSplits;
        int nearest;
        Vector3 lastPoint;
        bool haveCheckpoint;
        Vector3 cpPos, cpVel, cpSpin;
        Quaternion cpRot;
        bool wasBoosting, warnedMissing;
        float upsideDown;
        int ticks;
        float finishedAt;
        bool newGhost;

        Ghost recording, best;
        GameObject ghostCar;

        public void Init(TrackWorld world, ChaseCam cam, Hud hud, Game game)
        {
            this.world = world;
            path = world.Path;
            this.cam = cam;
            this.hud = hud;
            this.game = game;
            SpawnCar();
            LoadGhost();
            cam.Target = car;
            cam.Menu = false;
            hud.Bind(this, world.Info);
            Restart();
        }

        void SpawnCar()
        {
            var go = new GameObject("Player");
            go.layer = 2; // "Ignore Raycast" so the wheel rays and camera don't hit the car itself
            go.transform.SetParent(transform, false);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;

            var slick = new PhysicMaterial("Car") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f };
            slick.frictionCombine = PhysicMaterialCombine.Minimum;
            slick.bounceCombine = PhysicMaterialCombine.Minimum;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.58f, 0.02f);
            box.size = new Vector3(1.95f, 0.66f, 4.4f);
            box.sharedMaterial = slick;
            var top = go.AddComponent<BoxCollider>();
            top.center = new Vector3(0f, 1.0f, -0.3f);
            top.size = new Vector3(1.3f, 0.45f, 2.1f);
            top.sharedMaterial = slick;

            car = go.AddComponent<CarController>();
            car.Input = keys;
            car.Model = CarModel.Build(go.transform, Settings.CarColors[Settings.CarColor], Materials.CarPaint, Materials.CarMatte);
            SetLayer(car.Model.Root, 2);
            go.AddComponent<CarSound>();
            fx = go.AddComponent<CarFx>();
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        void LoadGhost()
        {
            best = Records.LoadGhost(world.Info.Id);
            bestSplits = Records.BestSplits(world.Info.Id);
            if (ghostCar != null) Destroy(ghostCar);
            ghostCar = null;
            if (best == null) return;

            ghostCar = new GameObject("Ghost");
            ghostCar.transform.SetParent(transform, false);
            var mat = new Material(Materials.Ghost);
            Color c = Settings.CarColors[Mathf.Clamp(best.CarColor, 0, Settings.CarColors.Length - 1)];
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(Mathf.Lerp(c.r, 1f, 0.4f), Mathf.Lerp(c.g, 1f, 0.4f), Mathf.Lerp(c.b, 1f, 0.4f), 0.3f));
            var parts = CarModel.Build(ghostCar.transform, Color.white, mat, mat, false);
            foreach (var r in parts.Renderers) r.receiveShadows = false;
        }

        public void Restart()
        {
            Pause(false);
            if (newGhost)
            {
                newGhost = false;
                LoadGhost();
            }
            Phase = State.Ready;
            Clock = 0f;
            CheckpointsHit = 0;
            taken = new bool[path.Checkpoints.Count];
            splits = new float[path.Checkpoints.Count];
            haveCheckpoint = false;
            warnedMissing = false;
            upsideDown = 0f;
            ticks = 0;
            recording = new Ghost { CarColor = Settings.CarColor };

            int s = path.SpawnIndex;
            car.Freeze(true);
            car.Teleport(path.Pos[s] + path.Up[s] * 0.05f, path.RotationAt(s), Vector3.zero, Vector3.zero);
            car.Input = keys;
            keys.Enabled = true;
            nearest = s;
            lastPoint = Point();
            if (fx != null) fx.Clear();
            cam.Snap();
            hud.Clear();
            if (ghostCar != null) PlaceGhost(0f);
        }

        void Go()
        {
            Phase = State.Driving;
            Clock = 0f;
            car.Freeze(false);
            car.Teleport(car.transform.position, car.transform.rotation, Vector3.zero, Vector3.zero);
            lastPoint = Point();
        }

        public void Respawn()
        {
            if (Phase != State.Driving || !haveCheckpoint)
            {
                Restart();
                return;
            }
            car.Teleport(cpPos, cpRot, cpVel, cpSpin);
            nearest = path.Nearest(cpPos, -1, 0);
            lastPoint = Point();
            upsideDown = 0f;
            if (fx != null) fx.Clear();
            cam.Snap();
            Sfx.Play(Sound.Respawn);
        }

        public void Pause(bool on)
        {
            if (Paused == on) return;
            Paused = on;
            Time.timeScale = on ? 0f : 1f;
            AudioListener.pause = on;
            hud.ShowPause(on);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = true;
        }

        void Update()
        {
            Cursor.visible = Paused || Phase == State.Finished || game.MenuOpen;
            if (game.MenuOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                if (Phase == State.Finished) game.ShowTracks();
                else Pause(!Paused);
                return;
            }
            if (Paused) return;

            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton3))
            {
                Restart();
                return;
            }
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton2);
            if (enter)
            {
                if (Phase == State.Finished) { Restart(); return; }
                if (Phase == State.Driving) Respawn();
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                Settings.CameraMode = (Settings.CameraMode + 1) % Settings.CameraNames.Length;
                cam.Snap();
            }

            keys.Tick(Time.deltaTime);
            if (Phase == State.Ready && keys.AnyDriveKey) Go();

            if (ghostCar != null)
            {
                bool show = Settings.ShowGhost;
                if (ghostCar.activeSelf != show) ghostCar.SetActive(show);
                if (show)
                {
                    float t = 0f;
                    if (Phase == State.Driving) t = Clock + (Time.time - Time.fixedTime);
                    else if (Phase == State.Finished) t = Clock + (Time.time - finishedAt);
                    PlaceGhost(t);
                }
            }
        }

        void PlaceGhost(float t)
        {
            Vector3 p;
            Quaternion r;
            best.Sample(t, out p, out r);
            ghostCar.transform.SetPositionAndRotation(p, r);
        }

        Vector3 Point() { return car.Body.position + car.transform.up * 0.5f; }

        void FixedUpdate()
        {
            if (Phase != State.Driving) return;
            float dt = Time.fixedDeltaTime;
            Vector3 now = Point();

            int n = path.Nearest(now, nearest, 40);
            if ((path.Pos[n] - now).sqrMagnitude > 50f * 50f) n = path.Nearest(now, -1, 0);
            nearest = n;

            // tell the car about the road under it (loops need this)
            Vector3 local = car.Body.position - path.Pos[n];
            float side = Vector3.Dot(local, path.Right[n]);
            float above = Vector3.Dot(local, path.Up[n]);
            car.OnTrack = path.HasRoad(n) && Mathf.Abs(side) < TrackPath.HalfWidth + 1f && above > -1f && above < 3f;
            car.RoadCurvature = path.Curvature[n];

            for (int k = 0; k < path.Checkpoints.Count; k++)
            {
                float f;
                if (taken[k] || !Crossed(path.Checkpoints[k], lastPoint, now, out f)) continue;
                taken[k] = true;
                float t = Clock - dt + f * dt;
                splits[CheckpointsHit] = t;
                float? diff = null;
                if (bestSplits != null && CheckpointsHit < bestSplits.Length) diff = t - bestSplits[CheckpointsHit];
                CheckpointsHit++;
                haveCheckpoint = true;
                cpPos = car.Body.position;
                cpRot = car.Body.rotation;
                cpVel = car.Body.velocity;
                cpSpin = car.Body.angularVelocity;
                Sfx.Play(Sound.Checkpoint);
                hud.Checkpoint(t, diff);
            }

            float ff;
            if (Crossed(path.FinishIndex, lastPoint, now, out ff))
            {
                if (CheckpointsHit >= CheckpointCount)
                {
                    Finish(Clock - dt + ff * dt);
                    return;
                }
                if (!warnedMissing)
                {
                    warnedMissing = true;
                    Sfx.Play(Sound.Wrong);
                    hud.Message("Missed a checkpoint", Ui.Bad);
                }
            }

            bool boosting = false;
            if (car.WheelsDown > 0)
                foreach (var b in path.Boosts)
                    if (n >= b.Start && n <= b.End && Mathf.Abs(side) < TrackPath.HalfWidth && above > -0.5f && above < 2.5f)
                        boosting = true;
            if (boosting && !wasBoosting) Sfx.Play(Sound.Boost);
            wasBoosting = boosting;
            car.OnBoost = boosting;

            float landed = car.Landing;
            if (landed > 10f) cam.Shake(Mathf.Clamp01(landed / 25f));

            bool lost = now.y < path.MinY - 25f || (path.Pos[n] - now).sqrMagnitude > 300f * 300f;
            if (world.Theme.HasWater && now.y < world.Theme.WaterLevel - 1f) lost = true;
            if (car.transform.up.y < -0.2f && car.Speed < 4f && !path.InLoop(n)) upsideDown += dt; else upsideDown = 0f;
            if (lost || upsideDown > 1.2f)
            {
                Respawn();
                return;
            }

            if (++ticks % 2 == 1) recording.Add(car.Body.position, car.Body.rotation);
            lastPoint = now;
            Clock += dt;
        }

        bool Crossed(int sample, Vector3 a, Vector3 b, out float frac)
        {
            frac = 0f;
            Vector3 p = path.Pos[sample], f = path.Fwd[sample];
            float s0 = Vector3.Dot(a - p, f), s1 = Vector3.Dot(b - p, f);
            if (!(s0 < 0f && s1 >= 0f)) return false;
            frac = Mathf.Clamp01(-s0 / (s1 - s0));
            Vector3 hit = Vector3.Lerp(a, b, frac) - p;
            return Mathf.Abs(Vector3.Dot(hit, path.Right[sample])) < TrackPath.HalfWidth + 2f
                && Mathf.Abs(Vector3.Dot(hit, path.Up[sample]) - 3f) < 8f;
        }

        void Finish(float t)
        {
            Phase = State.Finished;
            Clock = t;
            finishedAt = Time.time;
            recording.FinishTime = t;
            recording.Add(car.Body.position, car.Body.rotation);
            car.Input = coast;
            car.OnBoost = false;
            keys.Enabled = false;

            float before = Records.BestTime(world.Info.Id);
            Medal oldMedal = Records.MedalFor(world.Info, before);
            bool record = Records.SubmitRun(world.Info.Id, t, splits, recording);
            newGhost = record;
            Medal medal = Records.MedalFor(world.Info, t);
            Sfx.Play(Sound.Finish);
            hud.Finished(t, before, record, medal, medal > oldMedal);
        }
    }
}
