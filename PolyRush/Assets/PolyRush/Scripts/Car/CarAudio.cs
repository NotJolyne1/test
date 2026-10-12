using UnityEngine;

namespace PolyRush
{
    /// <summary>Engine with simulated gear shifts, wind and tyre squeal, all driven by the physics state.</summary>
    [RequireComponent(typeof(CarController))]
    public class CarAudio : MonoBehaviour
    {
        static AudioClip engineClip, windClip, skidClip;

        CarController car;
        AudioSource engine, wind, skid;
        float rpm;
        float gearBlend;
        public bool Muted;

        void Awake()
        {
            car = GetComponent<CarController>();
            if (engineClip == null) engineClip = AudioSynth.EngineLoop();
            if (windClip == null) windClip = AudioSynth.NoiseLoop("Wind", 0.06f, 2f, 21);
            if (skidClip == null) skidClip = AudioSynth.SkidLoop();
            engine = MakeSource(engineClip);
            wind = MakeSource(windClip);
            skid = MakeSource(skidClip);
        }

        AudioSource MakeSource(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.dopplerLevel = 0f;
            s.Play();
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Muted || Time.timeScale < 0.01f)
            {
                engine.volume = wind.volume = skid.volume = 0f;
                return;
            }
            float speed = Mathf.Abs(car.ForwardSpeed);
            // 6 virtual gears spread over the speed range
            float gearSpan = car.maxSpeed / 5.2f;
            float g = speed / gearSpan;
            int gear = Mathf.Min(5, Mathf.FloorToInt(g));
            float inGear = g - gear;
            if (gear >= 5) inGear = Mathf.Clamp01((speed - 5f * gearSpan) / (car.boostMaxSpeed - 5f * gearSpan));
            float targetRpm = 0.25f + inGear * 0.75f;
            if (!car.Grounded) targetRpm = Mathf.Max(targetRpm, 0.3f + car.EngineLoad * 0.6f);
            if (speed < 1f) targetRpm = 0.18f + car.EngineLoad * 0.35f;
            rpm = Mathf.Lerp(rpm, targetRpm, dt * 10f);
            gearBlend = Mathf.Lerp(gearBlend, gear, dt * 4f);

            float ev = GameSettings.EngineVolume;
            engine.pitch = 0.65f + rpm * 1.25f + gearBlend * 0.05f;
            engine.volume = ev * (0.32f + car.EngineLoad * 0.35f + rpm * 0.15f);

            float speedK = Mathf.Clamp01(car.Speed / 80f);
            wind.volume = ev * speedK * speedK * 0.55f;
            wind.pitch = 0.8f + speedK * 0.6f;

            float slip = car.Grounded ? Mathf.Clamp01((car.SlipSpeed - 3f) / 10f) : 0f;
            if (car.Drifting) slip = Mathf.Max(slip, 0.5f);
            skid.volume = Mathf.Lerp(skid.volume, GameSettings.EffectsVolume * slip * 0.45f, dt * 12f);
            skid.pitch = 0.9f + speedK * 0.25f;
        }
    }
}
