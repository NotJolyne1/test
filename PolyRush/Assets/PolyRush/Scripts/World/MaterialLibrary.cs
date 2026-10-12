using UnityEngine;

namespace PolyRush
{
    /// <summary>Creates and caches every material used by the game (shaders live in Resources/Shaders).</summary>
    public static class MaterialLibrary
    {
        static Material vertexLit, road, barrier, concrete, checker, boost, cpStripe, ground, carPaint, carMatte,
                        ghost, particles, glow, cloud;

        public static Shader LitShader()
        {
            var s = Shader.Find("PolyRush/Lit");
            if (s == null)
            {
                Debug.LogWarning("PolyRush: PolyRush/Lit shader missing, falling back to Standard");
                s = Shader.Find("Standard");
            }
            return s;
        }

        static Material Lit(string name, Texture tex, float gloss, Vector2 tiling)
        {
            var m = new Material(LitShader());
            m.name = name;
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling;
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        public static Material VertexLit { get { return vertexLit ?? (vertexLit = Lit("VertexLit", null, 0.1f, Vector2.one)); } }
        public static Material Road { get { return road ?? (road = Lit("Road", ProceduralTextures.Asphalt(), 0.22f, Vector2.one)); } }
        public static Material Barrier { get { return barrier ?? (barrier = Lit("Barrier", ProceduralTextures.BarrierStripes(), 0.35f, Vector2.one)); } }
        public static Material Concrete { get { return concrete ?? (concrete = Lit("Concrete", ProceduralTextures.Concrete(), 0.08f, Vector2.one)); } }
        public static Material Ground { get { return ground ?? (ground = Lit("Ground", ProceduralTextures.GroundDetail(), 0.04f, Vector2.one)); } }
        public static Material CarPaint { get { return carPaint ?? (carPaint = Lit("CarPaint", null, 0.72f, Vector2.one)); } }
        public static Material CarMatte { get { return carMatte ?? (carMatte = Lit("CarMatte", null, 0.25f, Vector2.one)); } }

        static Material Decal(string name, Texture tex, float gloss)
        {
            var m = Lit(name, tex, gloss, Vector2.one);
            if (m.HasProperty("_OffsetFactor"))
            {
                m.SetFloat("_OffsetFactor", -1f);
                m.SetFloat("_OffsetUnits", -2f);
            }
            return m;
        }

        public static Material Checker { get { return checker ?? (checker = Decal("Checker", ProceduralTextures.Checker(8, 2), 0.2f)); } }
        public static Material BoostPad { get { return boost ?? (boost = Decal("BoostPad", ProceduralTextures.BoostPad(), 0.5f)); } }
        public static Material CheckpointStripe { get { return cpStripe ?? (cpStripe = Decal("CheckpointStripe", ProceduralTextures.CheckpointStripe(), 0.3f)); } }

        public static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    glow = Lit("Glow", null, 0.3f, Vector2.one);
                    if (glow.HasProperty("_GlowStrength")) glow.SetFloat("_GlowStrength", 1.6f);
                }
                return glow;
            }
        }

        public static Material Cloud
        {
            get
            {
                if (cloud == null)
                {
                    cloud = Lit("Cloud", null, 0.0f, Vector2.one);
                    if (cloud.HasProperty("_GlowStrength")) cloud.SetFloat("_GlowStrength", 0.55f);
                }
                return cloud;
            }
        }

        public static Material Ghost
        {
            get
            {
                if (ghost == null)
                {
                    var s = Shader.Find("PolyRush/Ghost");
                    ghost = new Material(s != null ? s : LitShader());
                    ghost.name = "Ghost";
                }
                return ghost;
            }
        }

        public static Material Particles
        {
            get
            {
                if (particles == null)
                {
                    var s = Shader.Find("PolyRush/Unlit");
                    particles = new Material(s != null ? s : Shader.Find("Sprites/Default"));
                    particles.name = "Particles";
                    particles.mainTexture = ProceduralTextures.SoftDot();
                }
                return particles;
            }
        }

        public static Material NewSky(Theme theme, Vector3 sunDir)
        {
            var s = Shader.Find("PolyRush/Sky");
            if (s == null) return null;
            var m = new Material(s);
            m.name = "Sky";
            m.SetColor("_TopColor", theme.SkyTop);
            m.SetColor("_HorizonColor", theme.SkyHorizon);
            m.SetColor("_BottomColor", theme.SkyBottom);
            m.SetColor("_SunColor", theme.SunColor);
            m.SetVector("_SunDir", sunDir);
            return m;
        }

        public static Material NewWater(Theme theme, Vector3 sunDir)
        {
            var s = Shader.Find("PolyRush/Water");
            if (s == null) s = LitShader();
            var m = new Material(s);
            m.name = "Water";
            if (m.HasProperty("_ShallowColor"))
            {
                m.SetColor("_ShallowColor", theme.WaterShallow);
                m.SetColor("_DeepColor", theme.WaterDeep);
                m.SetColor("_SunColor", theme.SunColor);
                m.SetVector("_SunDir", sunDir);
            }
            else m.color = theme.WaterShallow;
            return m;
        }
    }
}
