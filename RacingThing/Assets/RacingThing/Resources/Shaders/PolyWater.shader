// Animated low-poly water: vertex waves, faceted normals from screen-space derivatives,
// fresnel blend between deep and shallow colours, sun glints and fog.
Shader "RacingThing/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.25,0.78,0.82,1)
        _DeepColor ("Deep", Color) = (0.05,0.35,0.55,1)
        _SunColor ("Sun", Color) = (1,0.95,0.85,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.6,0.4,0)
        _WaveHeight ("Wave Height", Float) = 0.35
        _WaveScale ("Wave Scale", Float) = 0.05
        _Alpha ("Alpha", Range(0,1)) = 0.86
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _ShallowColor, _DeepColor, _SunColor;
            float4 _SunDir;
            float _WaveHeight, _WaveScale, _Alpha;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float h = sin(w.x * _WaveScale + t * 1.1) * 0.5
                        + sin(w.z * _WaveScale * 1.3 + t * 0.8) * 0.35
                        + sin((w.x + w.z) * _WaveScale * 2.1 + t * 1.7) * 0.15;
                w.y += h * _WaveHeight;
                o.wpos = w;
                o.pos = UnityWorldToClipPos(w);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.wpos), ddx(i.wpos)));
                if (n.y < 0) n = -n;
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float fres = pow(1 - saturate(dot(n, v)), 3);
                float3 l = normalize(_SunDir.xyz);
                float diff = saturate(dot(n, l)) * 0.35 + 0.65;
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(fres * 1.2 + 0.15)) * diff;
                float3 hlf = normalize(l + v);
                float spec = pow(saturate(dot(n, hlf)), 180) * 1.4;
                col += _SunColor.rgb * spec;
                col += fres * 0.18;
                fixed4 c = fixed4(col, _Alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
