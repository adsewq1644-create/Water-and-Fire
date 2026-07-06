Shader "WaterAndFire/SpriteFireRevealMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HiddenAlpha ("Hidden Alpha", Range(0, 1)) = 0
        _RevealedAlpha ("Revealed Alpha", Range(0, 1)) = 1
        _RevealSoftness ("Reveal Softness", Range(0.01, 3)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define MAX_REVEAL_SOURCES 8

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _HiddenAlpha;
            float _RevealedAlpha;
            float _RevealSoftness;
            int _RevealSourceCount;
            float4 _RevealSources[MAX_REVEAL_SOURCES];

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                float reveal = 0.0;
                float softness = max(0.001, _RevealSoftness);

                [unroll]
                for (int index = 0; index < MAX_REVEAL_SOURCES; index++)
                {
                    if (index >= _RevealSourceCount)
                    {
                        break;
                    }

                    float radius = max(0.001, _RevealSources[index].z);
                    float distanceToSource = distance(i.worldPos, _RevealSources[index].xy);
                    float sourceReveal = 1.0 - smoothstep(radius - softness, radius, distanceToSource);
                    float sourceAlpha = lerp(_HiddenAlpha, _RevealedAlpha * saturate(_RevealSources[index].w), sourceReveal);
                    reveal = max(reveal, sourceAlpha);
                }

                float alpha = tex.a * max(_HiddenAlpha, reveal);
                return fixed4(tex.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
