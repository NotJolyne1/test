using UnityEngine;

namespace PolyRush
{
    /// <summary>Brakes to a stop after the finish line.</summary>
    public class AutoBrakeInput : ICarInput
    {
        public float Throttle { get { return 0f; } }
        public float Brake { get { return 1f; } }
        public float Steer { get { return 0f; } }
        public bool Drift { get { return false; } }
        public void Tick(float dt) { }
    }

    /// <summary>
    /// Runs one time-trial session on a built TrackWorld: spawning, countdown, timing (fixed-step,
    /// interpolated at the line), checkpoints in any order, boost pads, respawn at the last
    /// checkpoint with its saved speed (Enter), instant restart (R), ghosts and records.
    /// </summary>
    public class RaceController : MonoBehaviour
    {
        public enum State { Countdown, Racing, Finished }

        public State CurrentState { get; private set; }
        public float RaceTime { get; private set; }
        public int CheckpointsTaken { get; private set; }
        public int CheckpointTotal { get { return path.Checkpoints.Count; } }
        public bool Paused { get; private set; }
        public TrackWorld World { get { return world; } }
        public CarController Car { get { return car; } }

        TrackWorld world;
        TrackPath path;
        CameraRig rig;
        RaceHud hud;
        GameManager game;

        CarController car;
        LocalCarInput input;
        CarEffects carFx;
        readonly AutoBrakeInput autoBrake = new AutoBrakeInput();

        float countdown;
        int lastBeep;
        int nearest;
        Vector3 prevPos;
        bool[] cpTaken;
        float[] splits;
        float[] bestSplits;
        bool hasCpState;
        Vector3 cpPos, cpVel, cpAng;
        Quaternion cpRot;
        bool wasOnBoost;
        float flippedTimer;
        bool missingWarned;
        int fixedCounter;

        GhostData recording;
        GhostData bestGhost;
        GameObject ghostObj;
        float finishTime;
        bool reloadGhost;

        public void Init(TrackWorld world, CameraRig rig, RaceHud hud, GameManager game)
        {
            this.world = world;
            this.path = world.Path;
            this.rig = rig;
            this.hud = hud;
            this.game = game;
            SpawnCar();
            LoadGhost();
            rig.Target = car;
            rig.CurrentMode = CameraRig.Mode.Race;
            hud.Bind(this, world.Info);
            Restart();
        }

        // ------------------------------------------------------------------ setup

        void SpawnCar()
        {
            var go = new GameObject("PlayerCar");
            go.layer = 2; // Ignore Raycast: keeps wheel rays and the camera from hitting the car
            go.transform.SetParent(transform, false);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var mat = new PhysicMaterial("CarBody");
            mat.dynamicFriction = 0.05f;
            mat.staticFriction = 0.05f;
            mat.bounciness = 0f;
            mat.frictionCombine = PhysicMaterialCombine.Minimum;
            mat.bounceCombine = PhysicMaterialCombine.Minimum;

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.9f, 0.68f, 4.3f);
            box.sharedMaterial = mat;
            var cabin = go.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0f, 1.08f, -0.3f);
            cabin.size = new Vector3(1.4f, 0.5f, 2.0f);
            cabin.sharedMaterial = mat;

            car = go.AddComponent<CarController>();
            input = new LocalCarInput();
            car.Input = input;
            car.Visual = CarModelBuilder.Build(go.transform, GameSettings.CarColors[GameSettings.CarColor], MaterialLibrary.CarPaint, MaterialLibrary.CarMatte);
            SetLayer(car.Visual.Root, 2);
            go.AddComponent<CarAudio>();
            carFx = go.AddComponent<CarEffects>();
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        void LoadGhost()
        {
            bestGhost = Records.LoadGhost(world.Info.Id);
            bestSplits = Records.BestSplits(world.Info.Id);
            if (ghostObj != null) Destroy(ghostObj);
            ghostObj = null;
            if (bestGhost == null) return;
            ghostObj = new GameObject("Ghost");
            ghostObj.transform.SetParent(transform, false);
            var gm = new Material(MaterialLibrary.Ghost);
            if (gm.HasProperty("_Color"))
            {
                Color c = GameSettings.CarColors[Mathf.Clamp(bestGhost.CarColor, 0, GameSettings.CarColors.Length - 1)];
                gm.SetColor("_Color", new Color(Mathf.Lerp(c.r, 1f, 0.4f), Mathf.Lerp(c.g, 1f, 0.4f), Mathf.Lerp(c.b, 1f, 0.4f), 0.32f));
            }
            var vis = CarModelBuilder.Build(ghostObj.transform, Color.white, gm, gm, false);
            foreach (var r in vis.Renderers) r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ flow

        public void Restart()
        {
            SetPaused(false);
            if (reloadGhost)
            {
                reloadGhost = false;
                LoadGhost();
            }
            CurrentState = State.Countdown;
            countdown = 2.4f;
            lastBeep = 4;
            RaceTime = 0f;
            CheckpointsTaken = 0;
            cpTaken = new bool[path.Checkpoints.Count];
            splits = new float[path.Checkpoints.Count];
            hasCpState = false;
            missingWarned = false;
            flippedTimer = 0f;
            fixedCounter = 0;
            finishTime = -1f;
            recording = new GhostData();
            recording.CarColor = GameSettings.CarColor;

            int s = path.SpawnIndex;
            Vector3 pos = path.Pos[s] + path.Up[s] * 0.05f;
            car.SetFrozen(true);
            car.ResetTo(pos, path.RotationAt(s), Vector3.zero, Vector3.zero);
            car.Input = input;
            input.Enabled = true;
            nearest = s;
            prevPos = CarPoint();
            if (carFx != null) carFx.ClearAll();
            rig.SnapBehindTarget();
            hud.OnRestart();
            if (ghostObj != null) PlaceGhost(0f);
        }

        void Go()
        {
            CurrentState = State.Racing;
            car.SetFrozen(false);
            car.ResetTo(car.transform.position, car.transform.rotation, Vector3.zero, Vector3.zero);
            RaceTime = 0f;
            prevPos = CarPoint();
            AudioManager.Play(Sfx.GoBeep);
            hud.ShowCountdown("GO!", true);
        }

        public void Respawn()
        {
            if (CurrentState == State.Finished) return;
            if (!hasCpState || CurrentState != State.Racing)
            {
                Restart();
                return;
            }
            car.ResetTo(cpPos, cpRot, cpVel, cpAng);
            nearest = path.Nearest(cpPos, -1, 0);
            prevPos = CarPoint();
            flippedTimer = 0f;
            if (carFx != null) carFx.ClearAll();
            rig.SnapBehindTarget();
            AudioManager.Play(Sfx.Respawn);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (hud != null) hud.ShowPause(paused);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = true;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            // hide the mouse while driving, show it for menus
            Cursor.visible = Paused || CurrentState == State.Finished || (game != null && game.IsOverlayOpen);
            if (game != null && game.IsOverlayOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                if (CurrentState == State.Finished) game.ExitToTrackSelect();
                else SetPaused(!Paused);
                return;
            }
            if (Paused) return;

            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton3))
            {
                Restart();
                return;
            }
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton2)) && CurrentState == State.Racing)
                Respawn();
            if (Input.GetKeyDown(KeyCode.C))
            {
                GameSettings.CameraMode = (GameSettings.CameraMode + 1) % GameSettings.CameraNames.Length;
                rig.SnapBehindTarget();
            }
            if (CurrentState == State.Finished && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                Restart();
                return;
            }

            input.Tick(Time.deltaTime);

            if (CurrentState == State.Countdown)
            {
                countdown -= Time.deltaTime;
                int n = Mathf.CeilToInt(countdown / 0.6f);
                if (n < lastBeep && n >= 1 && n <= 3)
                {
                    lastBeep = n;
                    AudioManager.Play(Sfx.CountBeep);
                    hud.ShowCountdown(n.ToString(), false);
                }
                if (countdown <= 0f) Go();
            }

            if (ghostObj != null)
            {
                bool show = GameSettings.ShowGhost;
                if (ghostObj.activeSelf != show) ghostObj.SetActive(show);
                if (show)
                {
                    float t = CurrentState == State.Countdown ? 0f : (CurrentState == State.Finished ? finishTime + (Time.time - finishedAt) : RaceTime + (Time.time - Time.fixedTime));
                    PlaceGhost(t);
                }
            }
        }

        float finishedAt;

        void PlaceGhost(float t)
        {
            Vector3 p;
            Quaternion r;
            bestGhost.Sample(t, out p, out r);
            ghostObj.transform.SetPositionAndRotation(p, r);
        }

        Vector3 CarPoint()
        {
            return car.Body.position + car.transform.up * 0.5f;
        }

        void FixedUpdate()
        {
            if (CurrentState != State.Racing) return;
            float dt = Time.fixedDeltaTime;
            Vector3 cur = CarPoint();

            // nearest centre-line sample (local search, global fallback after teleports)
            int n = path.Nearest(cur, nearest, 50);
            if ((path.Pos[n] - cur).sqrMagnitude > 60f * 60f) n = path.Nearest(cur, -1, 0);
            nearest = n;

            // checkpoints (any order) - exact plane crossing, cannot be skipped at high speed
            for (int k = 0; k < path.Checkpoints.Count; k++)
            {
                if (cpTaken[k]) continue;
                float frac;
                if (Crossed(path.Checkpoints[k], prevPos, cur, out frac))
                {
                    cpTaken[k] = true;
                    float t = RaceTime - dt + frac * dt;
                    splits[CheckpointsTaken] = t;
                    float? delta = null;
                    if (bestSplits != null && CheckpointsTaken < bestSplits.Length) delta = t - bestSplits[CheckpointsTaken];
                    CheckpointsTaken++;
                    hasCpState = true;
                    cpPos = car.Body.position;
                    cpRot = car.Body.rotation;
                    cpVel = car.Body.velocity;
                    cpAng = car.Body.angularVelocity;
                    AudioManager.Play(Sfx.Checkpoint);
                    hud.OnCheckpoint(CheckpointsTaken, CheckpointTotal, t, delta);
                }
            }

            // finish
            float ff;
            if (Crossed(path.FinishIndex, prevPos, cur, out ff))
            {
                if (CheckpointsTaken >= CheckpointTotal)
                {
                    Finish(RaceTime - dt + ff * dt);
                    return;
                }
                if (!missingWarned)
                {
                    missingWarned = true;
                    AudioManager.Play(Sfx.Wrong);
                    hud.Flash("MISSING CHECKPOINTS  " + CheckpointsTaken + "/" + CheckpointTotal, UIKit.Bad);
                }
            }

            // boost pads
            bool onBoost = false;
            if (car.GroundedWheels > 0)
            {
                foreach (var b in path.Boosts)
                {
                    if (n < b.Start || n > b.End) continue;
                    Vector3 local = car.Body.position - path.Pos[n];
                    float lat = Vector3.Dot(local, path.Right[n]);
                    float h = Vector3.Dot(local, path.Up[n]);
                    if (Mathf.Abs(lat) < 4.6f && h > -0.5f && h < 2.5f) onBoost = true;
                }
            }
            if (onBoost && !wasOnBoost) AudioManager.Play(Sfx.Boost);
            wasOnBoost = onBoost;
            car.OnBoost = onBoost;

            // impacts
            if (car.LastImpact > 9f)
            {
                rig.AddShake(Mathf.Clamp01(car.LastImpact / 25f));
            }

            // fell off / drowned / flipped -> respawn
            bool fell = cur.y < path.MinY - 25f;
            if (world.Theme.HasWater && cur.y < world.Theme.WaterLevel - 1.0f) fell = true;
            if ((path.Pos[n] - cur).sqrMagnitude > 400f * 400f) fell = true;
            if (car.transform.up.y < -0.1f && car.Speed < 4f) flippedTimer += dt; else flippedTimer = 0f;
            if (fell || flippedTimer > 1.2f)
            {
                Respawn();
                return;
            }

            // ghost recording (50 Hz)
            fixedCounter++;
            if (fixedCounter % Mathf.Max(1, Mathf.RoundToInt(GhostData.Interval / dt)) == 0 || recording.Positions.Count == 0)
                recording.Add(car.Body.position, car.Body.rotation);

            prevPos = cur;
            RaceTime += dt;
        }

        bool Crossed(int sample, Vector3 a, Vector3 b, out float frac)
        {
            frac = 0f;
            Vector3 p = path.Pos[sample];
            Vector3 f = path.Fwd[sample];
            float s0 = Vector3.Dot(a - p, f);
            float s1 = Vector3.Dot(b - p, f);
            if (!(s0 < 0f && s1 >= 0f)) return false;
            frac = Mathf.Clamp01(-s0 / (s1 - s0));
            Vector3 hit = Vector3.Lerp(a, b, frac) - p;
            float lat = Vector3.Dot(hit, path.Right[sample]);
            float h = Vector3.Dot(hit, path.Up[sample]);
            return Mathf.Abs(lat) < TrackPath.HalfWidth + 2.5f && h > -3f && h < 11f;
        }

        void Finish(float time)
        {
            CurrentState = State.Finished;
            finishTime = time;
            finishedAt = Time.time;
            RaceTime = time;
            recording.FinishTime = time;
            recording.Add(car.Body.position, car.Body.rotation);
            car.Input = autoBrake;
            car.OnBoost = false;
            input.Enabled = false;

            float prevBest = Records.BestTime(world.Info.Id);
            Medal prevMedal = Records.MedalFor(world.Info, prevBest);
            bool record = Records.SubmitRun(world.Info.Id, time, splits, recording);
            reloadGhost = record;
            Medal medal = Records.MedalFor(world.Info, time);
            AudioManager.Play(Sfx.Finish);
            hud.ShowFinish(time, prevBest, record, medal, medal > prevMedal);
        }
    }
}
