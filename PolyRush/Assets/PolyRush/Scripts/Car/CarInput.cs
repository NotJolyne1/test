using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// Driving input. The car only talks to this interface, so a network or AI driver
    /// can be plugged in later (multiplayer) without touching the physics.
    /// </summary>
    public interface ICarInput
    {
        float Throttle { get; }  // 0..1
        float Brake { get; }     // 0..1 (brake, then reverse)
        float Steer { get; }     // -1..1
        bool Drift { get; }
        void Tick(float dt);
    }

    /// <summary>Keyboard (WASD / arrows) + gamepad through the default Input Manager axes.</summary>
    public class LocalCarInput : ICarInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public bool Drift { get; private set; }
        public bool Enabled = true;

        float steerSmoothed;

        public void Tick(float dt)
        {
            if (!Enabled)
            {
                Throttle = Brake = Steer = 0f;
                steerSmoothed = 0f;
                Drift = false;
                return;
            }
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            float padV = SafeAxis("Vertical");
            float padH = SafeAxis("Horizontal");

            float throttle = up ? 1f : Mathf.Clamp01(padV);
            float brake = down ? 1f : Mathf.Clamp01(-padV);
            float steerTarget = (right ? 1f : 0f) - (left ? 1f : 0f);
            if (Mathf.Abs(padH) > Mathf.Abs(steerTarget)) steerTarget = padH;

            // Keyboard steering ramps in quickly and snaps back faster.
            float rate = (Mathf.Abs(steerTarget) < Mathf.Abs(steerSmoothed) || Mathf.Sign(steerTarget) != Mathf.Sign(steerSmoothed)) ? 9f : 6f;
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, steerTarget, rate * dt);

            Throttle = throttle;
            Brake = brake;
            Steer = steerSmoothed;
            Drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton1);
        }

        static float SafeAxis(string name)
        {
            // Input.GetAxis treats keys too; we only want analogue sticks here, so ignore tiny values.
            float v = Input.GetAxisRaw(name);
            return Mathf.Abs(v) < 0.2f ? 0f : v;
        }
    }
}
