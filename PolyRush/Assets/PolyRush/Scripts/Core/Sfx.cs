using System.Collections.Generic;
using UnityEngine;

namespace PolyRush
{
    public enum Sound { Click, Hover, Checkpoint, Finish, Boost, Respawn, Land, Hit, Wrong }

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
            }
            if (clip != null) src.PlayOneShot(clip, v);
        }
    }
}
