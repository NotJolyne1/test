using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// Arcade raycast-vehicle physics tuned for a snappy, grippy, high-speed feel:
    /// four spring/damper suspension rays, direct yaw-rate steering, strong lateral grip with an
    /// optional drift, downforce, speed-dependent steering, a landing assist and in-air
    /// self-righting so jumps feel clean.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public const float DefaultMaxSpeed = 62f; // m/s (~223 km/h)

        [Header("Engine")]
        public float maxSpeed = DefaultMaxSpeed;
        public float acceleration = 22f;
        public float brakeDecel = 30f;
        public float reverseSpeed = 16f;
        public float boostAccel = 34f;
        public float boostMaxSpeed = 88f;

        [Header("Grip / steering")]
        public float lateralGrip = 11f;
        public float driftGrip = 2.2f;
        public float maxLateralAccel = 34f;
        public float wheelBase = 2.63f;
        public float steerResponse = 12f;

        [Header("Suspension")]
        public float suspensionLength = 0.45f;
        public float springStrength = 32000f;
        public float damperStrength = 3600f;
        public float mountHeight = 0.72f;

        [Header("Air")]
        public float airGravityExtra = 0.6f;   // keep in sync with Tools/track_tool.py (1 + this)
        public float airAlign = 3.2f;

        public ICarInput Input;
        public CarModelBuilder.CarVisual Visual;

        public Rigidbody Body { get; private set; }
        public bool Grounded { get; private set; }
        public int GroundedWheels { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SlipSpeed { get; private set; }
        public bool Drifting { get; private set; }
        public float AirTime { get; private set; }
        public bool OnBoost;           // set by the race controller every physics step
        public float BoostVisual { get; private set; }
        public float LastImpact { get; private set; }
        public float EngineLoad { get; private set; }

        public float Speed { get { return Body != null ? Body.velocity.magnitude : 0f; } }

        Vector3[] mounts;
        float[] lastCompression = new float[4];
        float[] wheelOffset = new float[4];
        float wheelSpin;
        bool wasGrounded;
        int layerMask;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 1200f;
            Body.drag = 0f;
            Body.angularDrag = 0.6f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 14f;

            mounts = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var w = CarModelBuilder.WheelPositions[i];
                mounts[i] = new Vector3(w.x, mountHeight, w.z);
            }
            layerMask = ~((1 << 2) | (1 << 1)); // ignore "Ignore Raycast" (car) and "TransparentFX"
        }

        void Start()
        {
            Body.centerOfMass = new Vector3(0f, 0.36f, -0.05f);
            Body.inertiaTensor = new Vector3(1350f, 1550f, 500f);
            Body.inertiaTensorRotation = Quaternion.identity;
        }

        public void SetFrozen(bool frozen)
        {
            Body.isKinematic = frozen;
            if (frozen) Body.interpolation = RigidbodyInterpolation.None;
            else Body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Teleports the car (used for start, restart and checkpoint respawns).</summary>
        public void ResetTo(Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angular)
        {
            bool kin = Body.isKinematic;
            Body.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            Body.position = pos;
            Body.rotation = rot;
            Body.isKinematic = kin;
            if (!kin)
            {
                Body.velocity = velocity;
                Body.angularVelocity = angular;
            }
            for (int i = 0; i < 4; i++) lastCompression[i] = 0.3f;
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

            Transform t = transform;
            Vector3 up = t.up;
            Vector3 normalSum = Vector3.zero;
            int grounded = 0;

            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = t.TransformPoint(mounts[i]);
                RaycastHit hit;
                float rayLen = suspensionLength + CarModelBuilder.WheelRadius;
                if (Physics.Raycast(origin, -up, out hit, rayLen, layerMask, QueryTriggerInteraction.Ignore))
                {
                    float compression = 1f - (hit.distance - CarModelBuilder.WheelRadius) / suspensionLength;
                    compression = Mathf.Clamp(compression, 0f, 1.2f);
                    float compVel = (compression - lastCompression[i]) / dt;
                    lastCompression[i] = compression;
                    float force = springStrength * compression * suspensionLength + damperStrength * compVel * suspensionLength;
                    if (compression >= 1f) force += springStrength * 2f * (compression - 1f) * suspensionLength + 20000f * Mathf.Max(0f, compVel) * suspensionLength;
                    force = Mathf.Max(0f, force);
                    Body.AddForceAtPosition(up * force, origin);
                    normalSum += hit.normal;
                    grounded++;
                    wheelOffset[i] = hit.distance - CarModelBuilder.WheelRadius;
                }
                else
                {
                    lastCompression[i] = 0f;
                    wheelOffset[i] = Mathf.Lerp(wheelOffset[i], suspensionLength, dt * 10f);
                }
            }

            GroundedWheels = grounded;
            Grounded = grounded > 0;
            Vector3 v = Body.velocity;

            if (Grounded)
            {
                Vector3 n = normalSum.normalized;

                // Landing assist: remove most of the velocity that points into the ground.
                if (!wasGrounded && AirTime > 0.2f)
                {
                    float vn = Vector3.Dot(v, n);
                    LastImpact = Mathf.Max(0f, -vn);
                    if (vn < 0f)
                    {
                        v -= n * vn * 0.8f;
                        Body.velocity = v;
                    }
                }
                AirTime = 0f;

                Vector3 fwd = Vector3.ProjectOnPlane(t.forward, n).normalized;
                Vector3 right = Vector3.Cross(n, fwd);
                float vF = Vector3.Dot(v, fwd);
                float vLat = Vector3.Dot(v, right);
                float gripFrac = grounded / 4f;
                ForwardSpeed = vF;

                // --- longitudinal
                float a = 0f;
                EngineLoad = 0f;
                if (throttle > 0.01f)
                {
                    if (vF < -1f) a += brakeDecel * throttle;
                    else
                    {
                        float x = Mathf.Clamp01(vF / maxSpeed);
                        a += throttle * acceleration * (1f - x * x);
                        EngineLoad = throttle;
                    }
                }
                if (brake > 0.01f)
                {
                    if (vF > 1f) a -= brakeDecel * brake;
                    else if (vF > -reverseSpeed) { a -= brake * acceleration * 0.6f; EngineLoad = brake * 0.6f; }
                }
                if (throttle < 0.01f && brake < 0.01f) a -= Mathf.Sign(vF) * Mathf.Min(Mathf.Abs(vF), 2.2f);
                a -= Mathf.Sign(vF) * 0.00045f * vF * vF;                    // aero drag
                if (vF > maxSpeed && !OnBoost) a -= (vF - maxSpeed) * 0.45f; // bleed off boost speed
                if (OnBoost && vF < boostMaxSpeed) a += boostAccel;
                Body.AddForce(fwd * a * gripFrac, ForceMode.Acceleration);

                // --- lateral grip
                Drifting = drift && Mathf.Abs(vF) > 12f;
                float grip = Drifting ? driftGrip : lateralGrip;
                float latCap = Drifting ? maxLateralAccel * 0.45f : maxLateralAccel;
                float latA = Mathf.Clamp(-vLat * grip, -latCap, latCap);
                Body.AddForce(right * latA * gripFrac, ForceMode.Acceleration);
                SlipSpeed = Mathf.Abs(vLat);

                // --- steering: drive the yaw rate directly (very responsive)
                float speedAbs = Mathf.Abs(vF);
                float steerAngle = Mathf.Lerp(34f, 8f, Mathf.Clamp01(speedAbs / maxSpeed)) * Mathf.Deg2Rad;
                float yawGeo = vF / wheelBase * Mathf.Tan(steerAngle) * steer;
                float yawCap = maxLateralAccel * 1.05f / Mathf.Max(speedAbs, 4f);
                if (Drifting) yawCap *= 1.6f;
                float targetYaw = Mathf.Clamp(yawGeo * (Drifting ? 1.4f : 1f), -yawCap, yawCap);
                float curYaw = Vector3.Dot(Body.angularVelocity, n);
                Body.AddTorque(n * (targetYaw - curYaw) * steerResponse * gripFrac, ForceMode.Acceleration);

                // --- downforce (sticks to banked turns at speed)
                Body.AddForce(-n * Mathf.Min(0.0045f * v.sqrMagnitude, 18f) * gripFrac, ForceMode.Acceleration);

                // --- damp roll/pitch wobble a little
                Vector3 av = Body.angularVelocity;
                Vector3 yawPart = n * Vector3.Dot(av, n);
                Body.AddTorque(-(av - yawPart) * 2.5f, ForceMode.Acceleration);
            }
            else
            {
                AirTime += dt;
                Drifting = false;
                SlipSpeed = 0f;
                EngineLoad = throttle * 0.4f;
                ForwardSpeed = Vector3.Dot(v, t.forward);
                Body.AddForce(Physics.gravity * airGravityExtra, ForceMode.Acceleration);

                // Self-righting: point the nose along the flight path and level the wings.
                if (v.sqrMagnitude > 64f && AirTime > 0.05f)
                {
                    Vector3 vdir = v.normalized;
                    Vector3 pitchAxis = Vector3.Cross(t.forward, vdir);
                    pitchAxis -= Vector3.up * Vector3.Dot(pitchAxis, Vector3.up) * 0.8f;
                    Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, t.forward).normalized;
                    Vector3 rollAxis = Vector3.Cross(t.up, levelUp);
                    Body.AddTorque((pitchAxis * 1.4f + rollAxis) * airAlign - Body.angularVelocity * 1.2f, ForceMode.Acceleration);
                }
                // small yaw control in the air
                Body.AddTorque(Vector3.up * steer * 1.6f, ForceMode.Acceleration);
            }

            BoostVisual = Mathf.MoveTowards(BoostVisual, OnBoost ? 1f : 0f, dt * (OnBoost ? 6f : 1.5f));
            wasGrounded = Grounded;
            wheelSpin += Vector3.Dot(Body.velocity, t.forward) / CarModelBuilder.WheelRadius * Mathf.Rad2Deg * dt;
            wheelSpin %= 360f;
        }

        void LateUpdate()
        {
            if (Visual == null) return;
            float steer = Input != null ? Input.Steer : 0f;
            float steerDeg = steer * Mathf.Lerp(30f, 10f, Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / maxSpeed));
            for (int i = 0; i < 4; i++)
            {
                var w = Visual.Wheels[i];
                if (w == null) continue;
                Vector3 basePos = CarModelBuilder.WheelPositions[i];
                // wheel centre sits 'offset' below the mount
                float y = mountHeight - Mathf.Clamp(wheelOffset[i], 0.02f, suspensionLength);
                w.localPosition = new Vector3(basePos.x, Mathf.Min(y, basePos.y + 0.18f), basePos.z);
                w.localRotation = Quaternion.Euler(wheelSpin, i < 2 ? steerDeg : 0f, 0f);
            }
        }
    }
}
