// Translucent ghost car with a bright rim. Depth pre-pass so the car does not see through itself.
Shader "RacingThing/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.6,0.85,1,0.35)
        _RimColor ("Rim", Color) = (0.8,0.95,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            ZWrite On
            ColorMask 0
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _RimColor;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };

            v2f vert (appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = WorldSpaceViewDir(i.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float rim = 1 - saturate(dot(normalize(i.n), normalize(i.v)));
                rim = pow(rim, 2.5);
                fixed4 c = _Color;
                c.rgb = lerp(c.rgb, _RimColor.rgb, rim);
                c.a = saturate(_Color.a + rim * 0.6);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
