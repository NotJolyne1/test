using System.Collections.Generic;
using UnityEngine;

namespace RacingThing
{
    public enum Sound { Click, Hover, Checkpoint, Finish, Boost, Respawn, Land, Hit, Wrong, Count, Go }

    // One-shot sounds. Clips live in Resources/Audio (made by Tools/make_sounds.py).
    public class Sfx : MonoBehaviour
    {
        static Sfx instance;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        AudioSource ui, game;

        public static AudioClip Clip(string name)
        {
            AudioClip c;
            if (!cache.TryGetValue(name, out c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                if (c == null) Debug.LogWarning("Missing sound Audio/" + name);
                cache[name] = c;
            }
            return c;
        }

        void Awake()
        {
            instance = this;
            ui = gameObject.AddComponent<AudioSource>();
            game = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = game.playOnAwake = false;
            ui.ignoreListenerPause = true;
        }

        public static void Play(Sound s, float volume = 1f)
        {
            if (instance == null) return;
            float v = Settings.EffectsVolume * volume;
            var src = s == Sound.Click || s == Sound.Hover ? instance.ui : instance.game;
            AudioClip clip = null;
            switch (s)
            {
                case Sound.Click: clip = Clip("click"); v *= 0.8f; break;
                case Sound.Hover: clip = Clip("hover"); v *= 0.5f; break;
                case Sound.Checkpoint: clip = Clip("checkpoint"); break;
                case Sound.Finish: clip = Clip("finish"); break;
                case Sound.Boost: clip = Clip("boost"); v *= 0.7f; break;
                case Sound.Respawn: clip = Clip("respawn"); v *= 0.8f; break;
                case Sound.Land: clip = Clip("land"); break;
                case Sound.Hit: clip = Clip("hit"); break;
                case Sound.Wrong: clip = Clip("wrong"); v *= 0.7f; break;
                case Sound.Count: clip = Beep(ref countBeep, 660f, 0.16f); v *= 0.6f; break;
                case Sound.Go: clip = Beep(ref goBeep, 990f, 0.5f); v *= 0.6f; break;
            }
            if (clip != null) src.PlayOneShot(clip, v);
        }

        public static void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (instance == null || clip == null) return;
            instance.game.PlayOneShot(clip, Settings.EffectsVolume * volume);
        }

        // ------------------------------------------------------------ made on the fly
        // A few short sounds are simple enough to build in code instead of shipping a wav.

        static AudioClip countBeep, goBeep, horn;

        // start lights: a plain tone with a soft edge so it doesn't click
        static AudioClip Beep(ref AudioClip cache, float freq, float length)
        {
            if (cache != null) return cache;
            const int rate = 44100;
            int n = (int)(rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Min(1f, (length - t) / 0.03f);
                data[i] = env * 0.6f * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t));
            }
            cache = AudioClip.Create("beep" + (int)freq, n, 1, rate, false);
            cache.SetData(data, 0);
            return cache;
        }

        // two slightly off notes, which is what makes a car horn sound like a car horn
        public static AudioClip Horn
        {
            get
            {
                if (horn != null) return horn;
                const int rate = 44100;
                const float length = 0.42f;
                int n = (int)(rate * length);
                var data = new float[n];
                float p1 = 0f, p2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    p1 += 415f / rate;
                    p2 += 523f / rate;
                    float a = (p1 % 1f) < 0.5f ? 1f : -1f;
                    float b = (p2 % 1f) * 2f - 1f;
                    float env = Mathf.Min(1f, t / 0.01f) * Mathf.Min(1f, (length - t) / 0.06f);
                    data[i] = env * 0.22f * (a + b);
                }
                // knock the harsh top off with a quick low-pass
                float y = 0f;
                for (int i = 0; i < n; i++)
                {
                    y += (data[i] - y) * 0.25f;
                    data[i] = y;
                }
                horn = AudioClip.Create("horn", n, 1, rate, false);
                horn.SetData(data, 0);
                return horn;
            }
        }
    }
}
