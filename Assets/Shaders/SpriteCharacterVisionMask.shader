Shader "WaterAndFire/SpriteCharacterVisionMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HiddenAlpha ("Hidden Alpha", Range(0, 1)) = 0
        _VisibleAlpha ("Visible Alpha", Range(0, 1)) = 1
        _VisionSoftness ("Vision Softness", Range(0.01, 3)) = 0.8
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

            #define MAX_VISION_SOURCES 8

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
            float _VisibleAlpha;
            float _VisionSoftness;
            int _VisionSourceCount;
            float4 _VisionSources[MAX_VISION_SOURCES];

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
                float visibility = _HiddenAlpha;
                float softness = max(0.001, _VisionSoftness);

                [unroll]
                for (int index = 0; index < MAX_VISION_SOURCES; index++)
                {
                    if (index >= _VisionSourceCount)
                    {
                        break;
                    }

                    float radius = max(0.001, _VisionSources[index].z);
                    float distanceToSource = distance(i.worldPos, _VisionSources[index].xy);
                    float sourceVisibility = 1.0 - smoothstep(radius - softness, radius, distanceToSource);
                    visibility = max(visibility, _VisibleAlpha * sourceVisibility * saturate(_VisionSources[index].w));
                }

                return fixed4(tex.rgb, tex.a * visibility);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
