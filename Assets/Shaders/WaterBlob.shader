Shader "WaterAndFire/WaterBlob"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.36, 0.92, 1, 0.76)
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.85
        _RimPower ("Rim Power", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed _BodyAlpha;
            fixed _RimPower;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, input.texcoord);
                float2 centered = input.texcoord - 0.5;
                fixed rim = saturate(length(centered) * 2.0);
                fixed highlight = pow(rim, 3.0) * _RimPower;

                fixed4 color = sprite * input.color;
                color.rgb += fixed3(0.55, 0.9, 1.0) * highlight * sprite.a;
                color.a *= _BodyAlpha;
                return color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
