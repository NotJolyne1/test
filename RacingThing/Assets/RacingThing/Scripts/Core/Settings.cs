using System.Collections.Generic;
using UnityEngine;

namespace RacingThing
{
    // Player options, saved in PlayerPrefs. Call Apply() after changing anything graphics/audio related.
    public static class Settings
    {
        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };
        public static readonly string[] CameraNames = { "Chase", "Far", "Bumper" };
        public static readonly string[] CarColorNames = { "Red", "Blue", "Lime", "Yellow", "Orange", "Purple", "White", "Graphite" };
        public static readonly Color[] CarColors =
        {
            new Color(0.88f, 0.16f, 0.14f),
            new Color(0.12f, 0.45f, 0.92f),
            new Color(0.45f, 0.82f, 0.18f),
            new Color(0.98f, 0.80f, 0.12f),
            new Color(1.00f, 0.50f, 0.10f),
            new Color(0.55f, 0.25f, 0.85f),
            new Color(0.93f, 0.93f, 0.95f),
            new Color(0.20f, 0.21f, 0.24f),
        };

        static float Get(string key, float def) { return PlayerPrefs.GetFloat("rt_" + key, def); }
        static int Get(string key, int def) { return PlayerPrefs.GetInt("rt_" + key, def); }
        static void Set(string key, float v) { PlayerPrefs.SetFloat("rt_" + key, v); }
        static void Set(string key, int v) { PlayerPrefs.SetInt("rt_" + key, v); }

        public static float MasterVolume { get { return Get("vol_master", 0.8f); } set { Set("vol_master", Mathf.Clamp01(value)); } }
        public static float EffectsVolume { get { return Get("vol_sfx", 0.8f); } set { Set("vol_sfx", Mathf.Clamp01(value)); } }
        public static float EngineVolume { get { return Get("vol_engine", 0.7f); } set { Set("vol_engine", Mathf.Clamp01(value)); } }
        public static int Quality { get { return Mathf.Clamp(Get("quality", DefaultQuality()), 0, 3); } set { Set("quality", Mathf.Clamp(value, 0, 3)); } }
        public static bool Fullscreen { get { return Get("fullscreen", 1) == 1; } set { Set("fullscreen", value ? 1 : 0); } }
        public static bool VSync { get { return Get("vsync", 1) == 1; } set { Set("vsync", value ? 1 : 0); } }
        public static bool ShowGhost { get { return Get("ghost", 1) == 1; } set { Set("ghost", value ? 1 : 0); } }
        public static bool UseMph { get { return Get("mph", 0) == 1; } set { Set("mph", value ? 1 : 0); } }
        public static bool ShowFps { get { return Get("fps", 0) == 1; } set { Set("fps", value ? 1 : 0); } }
        public static int CameraMode { get { return Mathf.Clamp(Get("camera", 0), 0, CameraNames.Length - 1); } set { Set("camera", value); } }
        public static int CarColor { get { return Mathf.Clamp(Get("carcolor", 0), 0, CarColors.Length - 1); } set { Set("carcolor", value); } }
        public static int ResolutionIndex { get { return Get("res", -1); } set { Set("res", value); } }

        // how much scenery to spawn at each quality level
        public static float DetailScale
        {
            get
            {
                switch (Quality)
                {
                    case 0: return 0.45f;
                    case 1: return 0.75f;
                    case 2: return 1.0f;
                    default: return 1.25f;
                }
            }
        }

        static int DefaultQuality()
        {
            // low-vram machines (most school laptops) start on medium
            int vram = SystemInfo.graphicsMemorySize;
            if (vram > 0 && vram < 1500) return 1;
            return 2;
        }

        public static List<Resolution> AvailableResolutions()
        {
            var list = new List<Resolution>();
            var seen = new HashSet<long>();
            var all = Screen.resolutions;
            for (int i = all.Length - 1; i >= 0; i--)
            {
                long key = all[i].width * 100000L + all[i].height;
                if (all[i].width < 960 || seen.Contains(key)) continue;
                seen.Add(key);
                list.Add(all[i]);
            }
            if (list.Count == 0)
            {
                var r = new Resolution();
                r.width = Screen.currentResolution.width;
                r.height = Screen.currentResolution.height;
                list.Add(r);
            }
            return list;
        }

        public static string ResolutionLabel()
        {
            var list = AvailableResolutions();
            int idx = ResolutionIndex;
            if (idx < 0 || idx >= list.Count) return Screen.width + " x " + Screen.height;
            return list[idx].width + " x " + list[idx].height;
        }

        public static void Apply(bool applyScreen)
        {
            int q = Quality;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 144;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.anisotropicFiltering = q >= 1 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = true; // used once per track for sky reflections
            QualitySettings.lodBias = 1f + q * 0.5f;
            switch (q)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 70f;
                    QualitySettings.shadowCascades = 0;
                    QualitySettings.antiAliasing = 0;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 130f;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.antiAliasing = 2;
                    break;
                case 2:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowDistance = 200f;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.antiAliasing = 4;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 320f;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.antiAliasing = 8;
                    break;
            }
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            AudioListener.volume = MasterVolume;

            if (applyScreen && !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                var list = AvailableResolutions();
                int idx = ResolutionIndex;
                int w = Screen.currentResolution.width, h = Screen.currentResolution.height;
                if (idx >= 0 && idx < list.Count) { w = list[idx].width; h = list[idx].height; }
                if (Fullscreen) Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
                else
                {
                    if (idx < 0 || idx >= list.Count) { w = 1600; h = 900; }
                    w = Mathf.Min(w, Screen.currentResolution.width);
                    h = Mathf.Min(h, Screen.currentResolution.height);
                    Screen.SetResolution(w, h, FullScreenMode.Windowed);
                }
            }
            PlayerPrefs.Save();
        }
    }
}
