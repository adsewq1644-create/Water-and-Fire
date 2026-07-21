Shader "WaterAndFire/DarkFogSilhouette2D"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.01,0.04,0.12,0.8)
        _Brightness ("Brightness", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0.25,2)) = 1.08
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DarkFogSilhouette"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Brightness;
                float _Contrast;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = saturate((_Tint.rgb - 0.5h) * _Contrast + 0.5h) * _Brightness;
                return half4(color, _Tint.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
