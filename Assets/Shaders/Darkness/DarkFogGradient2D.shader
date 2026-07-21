Shader "WaterAndFire/DarkFogGradient2D"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.002,0.008,0.045,1)
        _MiddleColor ("Middle Color", Color) = (0.012,0.07,0.21,1)
        _BottomColor ("Bottom Color", Color) = (0.02,0.35,0.56,1)
        _Brightness ("Brightness", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0.25,2)) = 1.08
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "DarkFogGradient"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _MiddleColor;
                half4 _BottomColor;
                float _Brightness;
                float _Contrast;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float lowerBlend = smoothstep(0.0, 0.52, input.uv.y);
                float upperBlend = smoothstep(0.46, 1.0, input.uv.y);
                half3 color = lerp(_BottomColor.rgb, _MiddleColor.rgb, lowerBlend);
                color = lerp(color, _TopColor.rgb, upperBlend);
                color = saturate((color - 0.5h) * _Contrast + 0.5h) * _Brightness;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
