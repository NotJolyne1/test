using UnityEngine;

namespace PolyRush
{
    // Engine made of several recorded-rpm loops that get crossfaded and pitched, the way most
    // racing games do it, plus tyre squeal, road rumble, wind and wall scrapes.
    [RequireComponent(typeof(CarController))]
    public class CarSound : MonoBehaviour
    {
        static readonly int[] Rpms = { 900, 2000, 3200, 4500, 5800, 7200, 8600 };
        static readonly float[] GearTop = { 0f, 14f, 26f, 38f, 50f, 63f, 200f };
        const float IdleRpm = 900f, RedLine = 8400f;

        CarController car;
        AudioSource[] engine;
        AudioSource skid, road, wind, scrape;
        float rpm = IdleRpm, load;
        int gear = 1;
        float shiftTimer;
        float scraping;

        void Awake()
        {
            car = GetComponent<CarController>();
            engine = new AudioSource[Rpms.Length];
            for (int i = 0; i < Rpms.Length; i++) engine[i] = Loop("engine_" + Rpms[i]);
            skid = Loop("skid");
            road = Loop("road");
            wind = Loop("wind");
            scrape = Loop("scrape");
        }

        AudioSource Loop(string clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Sfx.Clip(clip);
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            if (s.clip != null)
            {
                s.time = Random.Range(0f, s.clip.length * 0.9f);
                s.Play();
            }
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.timeScale < 0.01f) return;

            // fake gearbox: 6 gears spread over the speed range
            float speed = Mathf.Abs(car.ForwardSpeed);
            int wanted = 1;
            while (wanted < 6 && speed > GearTop[wanted]) wanted++;
            if (wanted != gear)
            {
                gear = wanted;
                shiftTimer = 0.12f;
            }
            float lo = GearTop[gear - 1], hi = gear == 6 ? CarController.TopSpeed * 1.1f : GearTop[gear];
            float inGear = Mathf.Clamp01((speed - lo * 0.7f) / (hi - lo * 0.7f));
            float target = Mathf.Lerp(2600f, RedLine, inGear);
            if (gear == 1) target = Mathf.Lerp(IdleRpm, RedLine * 0.85f, Mathf.Clamp01(speed / GearTop[1]));
            if (!car.Grounded) target = Mathf.Lerp(rpm, IdleRpm + car.Throttle * 6000f, 0.5f);
            if (speed < 1f) target = IdleRpm + car.Throttle * 2400f;
            if (shiftTimer > 0f) { shiftTimer -= dt; target *= 0.82f; }
            rpm = Mathf.Lerp(rpm, target, 1f - Mathf.Exp(-dt * (target > rpm ? 9f : 6f)));
            load = Mathf.Lerp(load, car.Throttle, dt * 8f);

            float vol = Settings.EngineVolume;
            float level = vol * (0.42f + 0.38f * load) * (0.8f + 0.2f * rpm / RedLine);
            for (int i = 0; i < Rpms.Length; i++)
            {
                // equal-power crossfade between the two nearest samples
                float w = 0f;
                if (i < Rpms.Length - 1 && rpm >= Rpms[i] && rpm <= Rpms[i + 1])
                    w = Mathf.Cos(0.5f * Mathf.PI * (rpm - Rpms[i]) / (Rpms[i + 1] - Rpms[i]));
                else if (i > 0 && rpm >= Rpms[i - 1] && rpm <= Rpms[i])
                    w = Mathf.Sin(0.5f * Mathf.PI * (rpm - Rpms[i - 1]) / (Rpms[i] - Rpms[i - 1]));
                else if ((i == 0 && rpm < Rpms[0]) || (i == Rpms.Length - 1 && rpm > Rpms[i]))
                    w = 1f;
                engine[i].volume = level * w;
                engine[i].pitch = Mathf.Clamp(rpm / Rpms[i], 0.5f, 2f);
            }

            float fx = Settings.EffectsVolume;
            float k = Mathf.Clamp01(car.Speed / 90f);
            float slide = car.Grounded ? Mathf.Clamp01((car.Slip - 4f) / 9f) : 0f;
            if (car.Drifting && car.Grounded) slide = Mathf.Max(slide, 0.55f);
            skid.volume = Mathf.Lerp(skid.volume, fx * slide * 0.5f, dt * 14f);
            skid.pitch = 0.92f + k * 0.2f;
            road.volume = car.Grounded ? vol * k * 0.45f : Mathf.Lerp(road.volume, 0f, dt * 8f);
            road.pitch = 0.7f + k * 0.6f;
            wind.volume = vol * k * k * 0.5f;
            wind.pitch = 0.85f + k * 0.4f;
            scraping = Mathf.MoveTowards(scraping, 0f, dt * 6f);
            scrape.volume = fx * scraping * 0.5f;

            float landed = car.TakeLanding();
            if (landed > 4f) Sfx.Play(Sound.Land, Mathf.Clamp01(landed / 14f));
        }

        void OnCollisionEnter(Collision c)
        {
            float hit = c.relativeVelocity.magnitude;
            if (hit > 6f) Sfx.Play(Sound.Hit, Mathf.Clamp01(hit / 30f));
        }

        void OnCollisionStay(Collision c)
        {
            if (car.Speed > 8f) scraping = Mathf.Min(1f, car.Speed / 40f);
        }
    }
}
