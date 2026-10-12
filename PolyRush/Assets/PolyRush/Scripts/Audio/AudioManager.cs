using UnityEngine;

namespace PolyRush
{
    public enum Sfx { Click, CountBeep, GoBeep, Checkpoint, Finish, Boost, Respawn, Impact, Wrong }

    /// <summary>2D one-shot sound effects. Volumes follow GameSettings.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        AudioSource ui, sfx;
        AudioClip click, countBeep, goBeep, chime, fanfare, whoosh, respawn, thud, wrong;

        void Awake()
        {
            Instance = this;
            ui = gameObject.AddComponent<AudioSource>();
            sfx = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = sfx.playOnAwake = false;
            ui.spatialBlend = sfx.spatialBlend = 0f;
            ui.ignoreListenerPause = true;

            click = AudioSynth.Click();
            countBeep = AudioSynth.Beep(587.3f, 0.22f);
            goBeep = AudioSynth.Beep(1174.7f, 0.5f);
            chime = AudioSynth.Chime();
            fanfare = AudioSynth.Fanfare();
            whoosh = AudioSynth.Whoosh();
            respawn = AudioSynth.Respawn();
            thud = AudioSynth.Thud();
            wrong = AudioSynth.Beep(220f, 0.3f);
        }

        public static void Play(Sfx s, float volume = 1f)
        {
            if (Instance != null) Instance.PlayInternal(s, volume);
        }

        void PlayInternal(Sfx s, float volume)
        {
            float v = GameSettings.EffectsVolume * volume;
            switch (s)
            {
                case Sfx.Click: ui.PlayOneShot(click, v * 0.8f); break;
                case Sfx.CountBeep: sfx.PlayOneShot(countBeep, v); break;
                case Sfx.GoBeep: sfx.PlayOneShot(goBeep, v); break;
                case Sfx.Checkpoint: sfx.PlayOneShot(chime, v); break;
                case Sfx.Finish: sfx.PlayOneShot(fanfare, v); break;
                case Sfx.Boost: sfx.PlayOneShot(whoosh, v * 0.8f); break;
                case Sfx.Respawn: sfx.PlayOneShot(respawn, v); break;
                case Sfx.Impact: sfx.PlayOneShot(thud, v); break;
                case Sfx.Wrong: sfx.PlayOneShot(wrong, v * 0.7f); break;
            }
        }
    }
}
