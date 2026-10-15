using UnityEngine;

namespace RacingThing
{
    // Arcade car on four suspension rays. Grip and steering are faked on purpose - the goal is
    // something that feels quick and slidey like PolyTrack, not a simulation.
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public const float TopSpeed = 76f;   // ~275 km/h

        public float acceleration = 24f;
        public float braking = 42f;
        public float reverseSpeed = 18f;
        public float boostAccel = 30f;
        public float boostTopSpeed = 100f;

        public float grip = 10f;
        public float driftGrip = 2.4f;
        public float maxCornering = 36f;
        public float wheelBase = 2.7f;
        public float steerResponse = 12f;

        public float suspension = 0.42f;
        public float spring = 32000f;
        public float damper = 3600f;
        public float mountHeight = 0.7f;

        public float airGravity = 0.6f;      // extra, on top of normal gravity

        public ICarInput Input;
        public CarModel.Parts Model;

        // set by the race every physics step
        [System.NonSerialized] public bool OnBoost;
        [System.NonSerialized] public bool OnTrack;
        [System.NonSerialized] public float RoadCurvature;

        public Rigidbody Body { get; private set; }
        public bool Grounded { get; private set; }
        public int WheelsDown { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float Slip { get; private set; }
        public bool Drifting { get; private set; }
        public float AirTime { get; private set; }
        public float Boost { get; private set; }
        public float Throttle { get; private set; }
        public float Landing { get; private set; }   // impact speed of the last landing, read and cleared by sound/camera
        public float Speed { get { return Body ? Body.velocity.magnitude : 0f; } }

        readonly Vector3[] mounts = new Vector3[4];
        readonly float[] compression = new float[4];
        readonly float[] wheelDrop = new float[4];
        float spin;
        bool wasGrounded;
        int mask;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 1100f;
            Body.drag = 0f;
            Body.angularDrag = 0.5f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 15f;
            for (int i = 0; i < 4; i++)
                mounts[i] = new Vector3(CarModel.Wheels[i].x, mountHeight, CarModel.Wheels[i].z);
            mask = ~((1 << 2) | (1 << 1));
        }

        void Start()
        {
            Body.centerOfMass = new Vector3(0f, 0.35f, -0.05f);
            Body.inertiaTensor = new Vector3(1250f, 1400f, 450f);
            Body.inertiaTensorRotation = Quaternion.identity;
        }

        public void Freeze(bool frozen)
        {
            Body.isKinematic = frozen;
            Body.interpolation = frozen ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }

        public void Teleport(Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angular)
        {
            bool kinematic = Body.isKinematic;
            Body.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            Body.position = pos;
            Body.rotation = rot;
            Body.isKinematic = kinematic;
            if (!kinematic)
            {
                Body.velocity = velocity;
                Body.angularVelocity = angular;
            }
            for (int i = 0; i < 4; i++) compression[i] = 0.25f;
            wasGrounded = true;
            AirTime = 0f;
            OnBoost = false;
        }

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            float throttle = 0f, brake = 0f, steer = 0f;
            bool drift = false;
            if (Input != null)
            {
                throttle = Input.Throttle;
                brake = Input.Brake;
                steer = Input.Steer;
                drift = Input.Drift;
            }
            Throttle = throttle;

            Transform t = transform;
            Vector3 up = t.up;
            Vector3 normal = Vector3.zero;
            int down = 0;
            float rayLength = suspension + CarModel.WheelRadius;

            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = t.TransformPoint(mounts[i]);
                RaycastHit hit;
                if (Physics.Raycast(origin, -up, out hit, rayLength, mask, QueryTriggerInteraction.Ignore))
                {
                    float c = Mathf.Clamp(1f - (hit.distance - CarModel.WheelRadius) / suspension, 0f, 1.2f);
                    float vel = (c - compression[i]) / dt;
                    compression[i] = c;
                    float force = (spring * c + damper * vel) * suspension;
                    if (c > 1f) force += spring * 3f * (c - 1f) * suspension;
                    Body.AddForceAtPosition(up * Mathf.Max(0f, force), origin);
                    normal += hit.normal;
                    down++;
                    wheelDrop[i] = hit.distance - CarModel.WheelRadius;
                }
                else
                {
                    compression[i] = 0f;
                    wheelDrop[i] = Mathf.Lerp(wheelDrop[i], suspension, dt * 10f);
                }
            }

            WheelsDown = down;
            Grounded = down > 0;
            Vector3 v = Body.velocity;

            if (Grounded)
                Drive(t, v, normal.normalized, down / 4f, throttle, brake, steer, drift, dt);
            else
                Fly(t, v, steer, dt);

            Boost = Mathf.MoveTowards(Boost, OnBoost ? 1f : 0f, dt * (OnBoost ? 6f : 1.5f));
            wasGrounded = Grounded;
            spin = (spin + Vector3.Dot(Body.velocity, t.forward) / CarModel.WheelRadius * Mathf.Rad2Deg * dt) % 360f;
        }

        void Drive(Transform t, Vector3 v, Vector3 n, float contact, float throttle, float brake, float steer, bool drift, float dt)
        {
            if (!wasGrounded && AirTime > 0.2f)
            {
                // soak up most of the landing so jumps don't bounce
                float into = Vector3.Dot(v, n);
                Landing = Mathf.Max(Landing, -into);
                if (into < 0f)
                {
                    v -= n * into * 0.8f;
                    Body.velocity = v;
                }
            }
            AirTime = 0f;

            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, n).normalized;
            Vector3 right = Vector3.Cross(n, fwd);
            float vf = Vector3.Dot(v, fwd);
            float vs = Vector3.Dot(v, right);
            ForwardSpeed = vf;

            float a = 0f;
            if (throttle > 0.01f)
            {
                if (vf < -1f) a += braking * throttle;
                else
                {
                    float x = Mathf.Clamp01(vf / TopSpeed);
                    a += throttle * acceleration * (1f - x * x);
                }
            }
            if (brake > 0.01f)
            {
                if (vf > 1f) a -= braking * brake;
                else if (vf > -reverseSpeed) a -= brake * acceleration * 0.6f;
            }
            if (throttle < 0.01f && brake < 0.01f) a -= Mathf.Sign(vf) * Mathf.Min(Mathf.Abs(vf), 2.5f);
            a -= Mathf.Sign(vf) * 0.0004f * vf * vf;
            if (vf > TopSpeed && !OnBoost) a -= (vf - TopSpeed) * 0.5f;
            if (OnBoost && vf < boostTopSpeed) a += boostAccel;
            Body.AddForce(fwd * a * contact, ForceMode.Acceleration);

            // sideways grip. The car can turn faster than the tyres can follow at speed, which is
            // where the slides come from.
            Drifting = drift && Mathf.Abs(vf) > 12f;
            float g = Drifting ? driftGrip : grip;
            float cap = Drifting ? maxCornering * 0.45f : maxCornering;
            Body.AddForce(right * Mathf.Clamp(-vs * g, -cap, cap) * contact, ForceMode.Acceleration);
            Slip = Mathf.Abs(vs);

            float speed = Mathf.Abs(vf);
            float steerLock = Mathf.Lerp(34f, 9f, Mathf.Clamp01(speed / TopSpeed)) * Mathf.Deg2Rad;
            float yaw = vf / wheelBase * Mathf.Tan(steerLock) * steer;
            float yawCap = maxCornering * 1.3f / Mathf.Max(speed, 4f);
            if (Drifting) { yaw *= 1.4f; yawCap *= 1.6f; }
            yaw = Mathf.Clamp(yaw, -yawCap, yawCap);
            float current = Vector3.Dot(Body.angularVelocity, n);
            Body.AddTorque(n * (yaw - current) * steerResponse * contact, ForceMode.Acceleration);

            Body.AddForce(-n * Mathf.Min(0.004f * v.sqrMagnitude, 16f) * contact, ForceMode.Acceleration);

            // Loops and dips: push the car along the curve so the springs only ever carry about
            // one g. Without this the suspension bottoms out in a loop.
            float k = OnTrack ? RoadCurvature : 0f;
            if (k > 0.004f)
            {
                float hold = v.sqrMagnitude * k - Vector3.Dot(Physics.gravity, n);
                if (hold > 0f)
                    Body.AddForce(n * (hold + Physics.gravity.y) * contact, ForceMode.Acceleration);
            }

            // keep the car's up lined up with the road and let it pitch with the curve
            Vector3 av = Body.angularVelocity;
            Vector3 nonYaw = av - n * Vector3.Dot(av, n);
            Vector3 expected = -right * (vf * k);
            Body.AddTorque((Vector3.Cross(t.up, n) * 20f - (nonYaw - expected) * 3f) * contact, ForceMode.Acceleration);
        }

        void Fly(Transform t, Vector3 v, float steer, float dt)
        {
            AirTime += dt;
            Drifting = false;
            Slip = 0f;
            ForwardSpeed = Vector3.Dot(v, t.forward);
            Body.AddForce(Physics.gravity * airGravity, ForceMode.Acceleration);

            // after a moment in the air, gently point the nose where the car is going and level out
            if (AirTime > 0.3f && v.sqrMagnitude > 64f)
            {
                Vector3 pitch = Vector3.Cross(t.forward, v.normalized);
                pitch -= Vector3.up * Vector3.Dot(pitch, Vector3.up) * 0.8f;
                Vector3 roll = Vector3.Cross(t.up, Vector3.ProjectOnPlane(Vector3.up, t.forward).normalized);
                Body.AddTorque((pitch * 1.4f + roll) * 3f - Body.angularVelocity * 1.2f, ForceMode.Acceleration);
            }
            Body.AddTorque(Vector3.up * steer * 1.8f, ForceMode.Acceleration);
        }

        public float TakeLanding()
        {
            float l = Landing;
            Landing = 0f;
            return l;
        }

        void LateUpdate()
        {
            if (Model == null) return;
            float steer = Input != null ? Input.Steer : 0f;
            float angle = steer * Mathf.Lerp(28f, 8f, Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / TopSpeed));
            for (int i = 0; i < 4; i++)
            {
                var w = Model.Wheels[i];
                Vector3 rest = CarModel.Wheels[i];
                float y = mountHeight - Mathf.Clamp(wheelDrop[i], 0.02f, suspension);
                w.localPosition = new Vector3(rest.x, Mathf.Min(y, rest.y + 0.16f), rest.z);
                w.localRotation = Quaternion.Euler(spin, i < 2 ? angle : 0f, 0f);
            }
        }
    }
}
