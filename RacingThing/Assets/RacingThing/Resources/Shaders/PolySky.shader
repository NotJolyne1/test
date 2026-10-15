// Gradient summer sky with a soft sun disc and glow.
Shader "RacingThing/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25,0.55,0.9,1)
        _HorizonColor ("Horizon", Color) = (0.75,0.9,1,1)
        _BottomColor ("Bottom", Color) = (0.85,0.93,0.97,1)
        _SunColor ("Sun", Color) = (1,0.95,0.85,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.6,0.4,0)
        _SunSize ("Sun Size", Range(0.001,0.1)) = 0.012
        _SunGlow ("Sun Glow", Range(1,256)) = 24
        _HorizonSharpness ("Horizon Sharpness", Range(0.1,4)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float4 _SunDir;
            float _SunSize, _SunGlow, _HorizonSharpness;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col;
                if (h >= 0)
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(h), _HorizonSharpness));
                else
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-h * 4), 0.5));

                float3 sd = normalize(_SunDir.xyz);
                float s = dot(d, sd);
                float disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.7, s);
                float glow = pow(saturate(s), _SunGlow) * 0.45 + pow(saturate(s), 4) * 0.12;
                col += _SunColor.rgb * (disc * 1.6 + glow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
