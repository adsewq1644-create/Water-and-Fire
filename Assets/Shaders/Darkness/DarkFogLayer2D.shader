Shader "WaterAndFire/DarkFogLayer2D"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.1,0.4,0.6,1)
        _FogAlpha ("Fog Alpha", Range(0,1)) = 0.25
        _VerticalBias ("Vertical Bias", Range(0,1)) = 0.9
        _ScrollSpeed ("Scroll Speed", Vector) = (0.012,0.004,0,0)
        _NoiseScale ("Noise Scale", Float) = 3.2
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.55
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
            Name "DarkFogLayer"
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
                half4 _FogColor;
                float _FogAlpha;
                float _VerticalBias;
                float4 _ScrollSpeed;
                float _NoiseScale;
                float _NoiseStrength;
                float _Brightness;
                float _Contrast;
            CBUFFER_END

            float Hash(float2 samplePosition)
            {
                return frac(sin(dot(samplePosition, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 fraction = frac(samplePosition);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float a = Hash(cell);
                float b = Hash(cell + float2(1.0, 0.0));
                float c = Hash(cell + float2(0.0, 1.0));
                float d = Hash(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, fraction.x), lerp(c, d, fraction.x), fraction.y);
            }

            float FogNoise(float2 samplePosition)
            {
                float value = 0.0;
                float amplitude = 0.58;
                for (int octave = 0; octave < 3; octave++)
                {
                    value += ValueNoise(samplePosition) * amplitude;
                    samplePosition = samplePosition * 2.03 + 7.13;
                    amplitude *= 0.47;
                }
                return saturate(value);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 scroll = _ScrollSpeed.xy * _Time.y;
                float2 noiseUv = input.uv * _NoiseScale + scroll;
                float broadNoise = FogNoise(noiseUv);
                float wisps = FogNoise(noiseUv * float2(0.52, 1.65) + 13.7);
                float noise = lerp(0.72, broadNoise * 0.62 + wisps * 0.58, _NoiseStrength);

                float bottomDensity = 0.04 + 0.96 * pow(saturate(1.0 - input.uv.y), 1.60);
                float verticalDensity = lerp(1.0, bottomDensity, _VerticalBias);
                float sideFade = smoothstep(0.0, 0.08, input.uv.x) * smoothstep(0.0, 0.08, 1.0 - input.uv.x);
                float topFade = smoothstep(0.0, 0.14, 1.0 - input.uv.y);
                float alpha = saturate(_FogAlpha * verticalDensity * noise * sideFade * topFade);

                half3 color = saturate((_FogColor.rgb - 0.5h) * _Contrast + 0.5h) * _Brightness;
                return half4(color, alpha * _FogColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
