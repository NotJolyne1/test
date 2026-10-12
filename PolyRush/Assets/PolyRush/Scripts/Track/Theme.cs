using UnityEngine;

namespace PolyRush
{
    public enum TreeKind { Round, Pine, Palm, Bush, Cactus, Birch }

    // Colours and scenery for one environment.
    public class Theme
    {
        public string Id;
        public string DisplayName;

        // Sky / light
        public Color SkyTop, SkyHorizon, SkyBottom;
        public Color SunColor = new Color(1f, 0.96f, 0.86f);
        public float SunIntensity = 1.15f;
        public Vector3 SunEuler = new Vector3(48f, -35f, 0f);
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public Color FogColor;
        public float FogDensity = 0.0012f;

        // Ground
        public Color GrassA, GrassB, Sand, Rock, Dirt, Shoulder;
        public float HillAmp = 10f;
        public float MountainHeight = 90f;
        public float LakeAmount = 0f;   // 0..1 how much of the land becomes lakes
        public bool HasWater = true;
        public float WaterLevel = -1.6f;
        public Color WaterShallow = new Color(0.25f, 0.78f, 0.82f);
        public Color WaterDeep = new Color(0.05f, 0.35f, 0.55f);
        public float OceanAngle = -1f;  // degrees; < 0 means no ocean side
        public float OceanDistance = 140f;
        public bool Island;             // ocean all around the track
        public bool Mesas;              // big layered rock formations (canyon)

        // Scenery
        public TreeKind[] Trees = { TreeKind.Round };
        public float TreeDensity = 1f;
        public float RockDensity = 0.3f;
        public int Clouds = 22;
        public Color AccentColor = new Color(1f, 0.6f, 0.15f);

        public static Theme Get(string id)
        {
            var t = new Theme();
            t.Id = id;
            // default: plain sunny day
            t.SkyTop = Hex(0x3E8EE8);
            t.SkyHorizon = Hex(0xBFE6FF);
            t.SkyBottom = Hex(0xD9EEF7);
            t.AmbientSky = Hex(0x9CC8F0);
            t.AmbientEquator = Hex(0xB9D3C6);
            t.AmbientGround = Hex(0x6C7A52);
            t.FogColor = Hex(0xC4E3F5);
            t.GrassA = Hex(0x6DBB3F);
            t.GrassB = Hex(0x8BCB4A);
            t.Sand = Hex(0xE8D49A);
            t.Rock = Hex(0x9A9488);
            t.Dirt = Hex(0xA7865A);
            t.Shoulder = Hex(0x8C9A6A);

            switch (id)
            {
                case "coast":
                    t.DisplayName = "Coast";
                    t.Trees = new[] { TreeKind.Palm, TreeKind.Palm, TreeKind.Bush, TreeKind.Round };
                    t.TreeDensity = 0.7f;
                    t.OceanAngle = 115f;
                    t.OceanDistance = 55f;
                    t.HillAmp = 8f;
                    t.GrassA = Hex(0x7CC046);
                    t.GrassB = Hex(0x9CD15A);
                    t.Sand = Hex(0xF0DCA0);
                    t.AccentColor = Hex(0x29B6F6);
                    break;
                case "forest":
                    t.DisplayName = "Forest";
                    t.Trees = new[] { TreeKind.Pine, TreeKind.Pine, TreeKind.Pine, TreeKind.Birch, TreeKind.Bush };
                    t.TreeDensity = 1.5f;
                    t.HillAmp = 18f;
                    t.MountainHeight = 140f;
                    t.LakeAmount = 0.15f;
                    t.GrassA = Hex(0x4E9A3A);
                    t.GrassB = Hex(0x6AAE45);
                    t.FogColor = Hex(0xBBD9E8);
                    t.SkyHorizon = Hex(0xC8E4F4);
                    t.AccentColor = Hex(0x66BB6A);
                    t.RockDensity = 0.6f;
                    break;
                case "lake":
                    t.DisplayName = "Lakes";
                    t.Trees = new[] { TreeKind.Round, TreeKind.Birch, TreeKind.Pine, TreeKind.Bush };
                    t.TreeDensity = 1.1f;
                    t.LakeAmount = 0.55f;
                    t.HillAmp = 12f;
                    t.WaterLevel = -1.4f;
                    t.AccentColor = Hex(0x26C6DA);
                    break;
                case "canyon":
                    t.DisplayName = "Canyon";
                    t.Trees = new[] { TreeKind.Cactus, TreeKind.Cactus, TreeKind.Bush };
                    t.TreeDensity = 0.35f;
                    t.RockDensity = 1.6f;
                    t.HillAmp = 22f;
                    t.MountainHeight = 120f;
                    t.HasWater = false;
                    t.Mesas = true;
                    t.GrassA = Hex(0xD9A066);
                    t.GrassB = Hex(0xE3B47A);
                    t.Sand = Hex(0xEBC98F);
                    t.Rock = Hex(0xC0673E);
                    t.Dirt = Hex(0xB8784A);
                    t.Shoulder = Hex(0xC79763);
                    t.SkyTop = Hex(0x3A86D9);
                    t.SkyHorizon = Hex(0xF4DDB8);
                    t.SkyBottom = Hex(0xF2D9B5);
                    t.FogColor = Hex(0xF0D9BC);
                    t.AmbientGround = Hex(0x9A6A45);
                    t.AmbientEquator = Hex(0xE0C2A0);
                    t.SunColor = Hex(0xFFF0D6);
                    t.SunIntensity = 1.25f;
                    t.AccentColor = Hex(0xFF7043);
                    break;
                case "sunset":
                    t.DisplayName = "Sunset";
                    t.Trees = new[] { TreeKind.Round, TreeKind.Palm, TreeKind.Bush };
                    t.TreeDensity = 0.8f;
                    t.HillAmp = 10f;
                    t.LakeAmount = 0.25f;
                    t.SkyTop = Hex(0x3B4D9B);
                    t.SkyHorizon = Hex(0xFFB06A);
                    t.SkyBottom = Hex(0xF59E78);
                    t.SunColor = Hex(0xFFC28A);
                    t.SunIntensity = 1.05f;
                    t.SunEuler = new Vector3(14f, -60f, 0f);
                    t.AmbientSky = Hex(0x8C8FC7);
                    t.AmbientEquator = Hex(0xE8A27E);
                    t.AmbientGround = Hex(0x6B5A45);
                    t.FogColor = Hex(0xF2B48A);
                    t.FogDensity = 0.0016f;
                    t.GrassA = Hex(0x7BAF3E);
                    t.GrassB = Hex(0x96BD49);
                    t.WaterShallow = Hex(0x5E9AA8);
                    t.WaterDeep = Hex(0x34437A);
                    t.AccentColor = Hex(0xFF8A65);
                    break;
                case "tropic":
                    t.DisplayName = "Island";
                    t.Trees = new[] { TreeKind.Palm, TreeKind.Palm, TreeKind.Palm, TreeKind.Bush, TreeKind.Round };
                    t.TreeDensity = 1.0f;
                    t.Island = true;
                    t.OceanDistance = 70f;
                    t.HillAmp = 14f;
                    t.MountainHeight = 70f;
                    t.WaterShallow = Hex(0x3FE0D0);
                    t.WaterDeep = Hex(0x0A6FA8);
                    t.GrassA = Hex(0x5DB84A);
                    t.GrassB = Hex(0x7CCB52);
                    t.Sand = Hex(0xF6E3AE);
                    t.SkyTop = Hex(0x2F8BEA);
                    t.AccentColor = Hex(0xFFCA28);
                    break;
                default: // meadow
                    t.Id = "meadow";
                    t.DisplayName = "Meadow";
                    t.Trees = new[] { TreeKind.Round, TreeKind.Round, TreeKind.Birch, TreeKind.Bush, TreeKind.Pine };
                    t.TreeDensity = 0.9f;
                    t.LakeAmount = 0.12f;
                    t.AccentColor = Hex(0xFFB300);
                    break;
            }
            return t;
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        }
    }
}
