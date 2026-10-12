using UnityEngine;

namespace PolyRush
{
    /// <summary>Generates every sound effect procedurally (no audio files needed).</summary>
    public static class AudioSynth
    {
        const int Rate = 44100;

        static AudioClip Make(string name, float[] data, bool loop = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Engine loop: harmonic-rich tone at 55 Hz (exactly 55 cycles per second so it loops seamlessly).</summary>
        public static AudioClip EngineLoop()
        {
            int n = Rate;
            var d = new float[n];
            var rng = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float ph = 2f * Mathf.PI * 55f * t;
                float s = 0f;
                s += Mathf.Sin(ph) * 0.55f;
                s += Mathf.Sin(ph * 2f) * 0.32f;
                s += Mathf.Sin(ph * 3f + 0.4f) * 0.22f;
                s += Mathf.Sin(ph * 4f) * 0.14f;
                s += Mathf.Sin(ph * 6f + 1.1f) * 0.08f;
                // combustion pulses
                float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(ph * 0.5f)), 6f);
                s *= 0.75f + pulse * 0.5f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.15f;
                s += lp * 0.12f;
                d[i] = Mathf.Clamp(s * 0.45f, -1f, 1f);
            }
            return Make("Engine", d, true);
        }

        /// <summary>Filtered noise loop (wind / tyre roar).</summary>
        public static AudioClip NoiseLoop(string name, float cutoff, float seconds, int seed)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var d = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)rng.NextDouble() * 2f - 1f;
                lp += (x - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                d[i] = lp2;
            }
            Normalize(d, 0.6f);
            // crossfade the loop seam
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                float a = d[i], b = d[n - fade + i];
                d[i] = Mathf.Lerp(b, a, k);
            }
            var trimmed = new float[n - fade];
            System.Array.Copy(d, 0, trimmed, 0, n - fade);
            return Make(name, trimmed, true);
        }

        /// <summary>Tyre squeal: band-limited noise with a resonant whistle.</summary>
        public static AudioClip SkidLoop()
        {
            int n = Rate;
            var d = new float[n];
            var rng = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = (float)rng.NextDouble() * 2f - 1f;
                lp += (x - lp) * 0.35f;
                float tone = Mathf.Sin(2f * Mathf.PI * 820f * t + Mathf.Sin(2f * Mathf.PI * 7f * t) * 2f) * 0.35f;
                d[i] = (lp * 0.55f + tone) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 13f * t));
            }
            Normalize(d, 0.5f);
            return Make("Skid", d, true);
        }

        public static AudioClip Beep(float freq, float dur, float volume = 0.5f)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Clamp01((dur - t) * 12f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f) * env * volume;
            }
            return Make("Beep" + freq, d);
        }

        static void AddNote(float[] d, float start, float freq, float dur, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 5f);
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.1f;
                d[s0 + i] += s * env * vol;
            }
        }

        public static AudioClip Chime()
        {
            var d = new float[Mathf.RoundToInt(Rate * 0.8f)];
            AddNote(d, 0f, 880f, 0.6f, 0.4f);
            AddNote(d, 0.09f, 1318.5f, 0.7f, 0.35f);
            return Make("Chime", d);
        }

        public static AudioClip Fanfare()
        {
            var d = new float[Mathf.RoundToInt(Rate * 1.8f)];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++) AddNote(d, i * 0.11f, notes[i], 1.2f, 0.32f);
            AddNote(d, 0.5f, 1046.5f, 1.3f, 0.25f);
            AddNote(d, 0.5f, 1318.5f, 1.3f, 0.2f);
            return Make("Fanfare", d);
        }

        public static AudioClip Whoosh()
        {
            int n = Mathf.RoundToInt(Rate * 0.7f);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float cutoff = Mathf.Lerp(0.05f, 0.5f, t);
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * cutoff;
                float env = Mathf.Sin(t * Mathf.PI);
                d[i] = lp * env * 0.9f;
            }
            Normalize(d, 0.55f);
            return Make("Whoosh", d);
        }

        public static AudioClip Click()
        {
            int n = Mathf.RoundToInt(Rate * 0.05f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 1600f * t) * Mathf.Exp(-t * 90f) * 0.35f;
            }
            return Make("Click", d);
        }

        public static AudioClip Thud()
        {
            int n = Mathf.RoundToInt(Rate * 0.35f);
            var d = new float[n];
            var rng = new System.Random(4);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.08f;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.7f + lp * 1.5f) * Mathf.Exp(-t * 14f);
            }
            Normalize(d, 0.7f);
            return Make("Thud", d);
        }

        public static AudioClip Respawn()
        {
            int n = Mathf.RoundToInt(Rate * 0.3f);
            var d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(300f, 1200f, t);
                ph += 2f * Mathf.PI * f / Rate;
                d[i] = Mathf.Sin(ph) * Mathf.Sin(t * Mathf.PI) * 0.35f;
            }
            return Make("Respawn", d);
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
    }
}
