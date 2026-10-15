using UnityEngine;

namespace RacingThing
{
    // The car only reads this, so a network player or replay can drive it later.
    public interface ICarInput
    {
        float Throttle { get; }
        float Brake { get; }
        float Steer { get; }
        bool Drift { get; }
        void Tick(float dt);
    }

    public class KeyboardInput : ICarInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public bool Drift { get; private set; }
        public bool Enabled = true;

        float steer;

        public bool AnyDriveKey
        {
            get
            {
                return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
                       Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ||
                       Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ||
                       Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ||
                       Mathf.Abs(Stick("Vertical")) > 0f || Mathf.Abs(Stick("Horizontal")) > 0f;
            }
        }

        public void Tick(float dt)
        {
            if (!Enabled)
            {
                Throttle = Brake = Steer = steer = 0f;
                Drift = false;
                return;
            }
            float padV = Stick("Vertical"), padH = Stick("Horizontal");
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            float target = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            if (Mathf.Abs(padH) > Mathf.Abs(target)) target = padH;

            // ease keyboard steering in, snap it back faster
            bool returning = Mathf.Abs(target) < Mathf.Abs(steer) || Mathf.Sign(target) != Mathf.Sign(steer);
            steer = Mathf.MoveTowards(steer, target, (returning ? 10f : 6.5f) * dt);

            Throttle = up ? 1f : Mathf.Clamp01(padV);
            Brake = down ? 1f : Mathf.Clamp01(-padV);
            Steer = steer;
            Drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton1);
        }

        static float Stick(string axis)
        {
            float v = Input.GetAxisRaw(axis);
            return Mathf.Abs(v) < 0.25f ? 0f : v;
        }
    }

    public class BrakeInput : ICarInput
    {
        public float Throttle { get { return 0f; } }
        public float Brake { get { return 1f; } }
        public float Steer { get { return 0f; } }
        public bool Drift { get { return false; } }
        public void Tick(float dt) { }
    }
}
