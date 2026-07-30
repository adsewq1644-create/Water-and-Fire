Shader "WaterAndFire/Effects/Shockwave2D"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Softness ("Softness", Range(0.01, 1)) = 0.34
        _Progress ("Progress", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Float) = 0.055
        _ShapeMode ("Shape Mode", Float) = 0
        _ArcDirection ("Arc Direction", Vector) = (0, 1, 0, 0)
        _ArcAngle ("Arc Angle", Range(1, 360)) = 360
        _EdgeFade ("Edge Fade", Range(0, 0.45)) = 0.14
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Shockwave2D"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _ArcDirection;
                float _Alpha;
                float _Softness;
                float _Progress;
                float _RingWidth;
                float _ShapeMode;
                float _ArcAngle;
                float _EdgeFade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = abs(input.uv.y * 2.0 - 1.0);
                float profile = 1.0 - smoothstep(
                    1.0 - max(0.01, _Softness),
                    1.0,
                    across);

                half4 color = input.color * _Tint;
                color.a *= profile * _Alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
