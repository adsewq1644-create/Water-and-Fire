Shader "WaterAndFire/WaterBlob"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _WaterColorA ("Deep Water", Color) = (0.08, 0.58, 0.8, 0.75)
        [HDR] _WaterColorB ("Soft Water", Color) = (0.55, 0.98, 1.0, 0.88)
        [HDR] _FoamColor ("Foam", Color) = (0.92, 1.0, 1.0, 1)
        [HDR] _GlowColor ("Glow", Color) = (0.3, 0.85, 1.0, 1)
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.78
        _FlowSpeed ("Flow Speed", Range(-8, 8)) = 2.1
        _PatternScale ("Pattern Scale", Range(0.5, 18)) = 5.2
        _NoiseScale ("Noise Scale", Range(0.5, 25)) = 8.5
        _FoamAmount ("Foam Amount", Range(0, 3)) = 0.7
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColorA;
                half4 _WaterColorB;
                half4 _FoamColor;
                half4 _GlowColor;
                half _BodyAlpha;
                half _FlowSpeed;
                half _PatternScale;
                half _NoiseScale;
                half _FoamAmount;
                half _EmissionIntensity;
            CBUFFER_END

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float alphaMask = sprite.a;
                float time = _Time.y;
                float2 centered = input.uv - 0.5;
                float radial = saturate(1.0 - length(centered) * 2.0);
                float flow = Noise(float2(input.uv.x * _PatternScale + time * 0.25, input.uv.y * _NoiseScale - time * _FlowSpeed));
                float foam = smoothstep(0.9, 0.985, Noise(float2(input.uv.x * 12.0 - time * 1.7, input.uv.y * 11.0 + time * 0.8)));
                float rim = smoothstep(0.05, 0.65, alphaMask) * (1.0 - smoothstep(0.55, 1.0, radial));

                half3 color = lerp(_WaterColorA.rgb, _WaterColorB.rgb, saturate(flow * 0.65 + radial * 0.25));
                color *= input.color.rgb;
                color += _FoamColor.rgb * foam * _FoamAmount;
                color += _GlowColor.rgb * (rim * 0.75 + foam * 0.3) * _EmissionIntensity;

                half alpha = alphaMask * _BodyAlpha * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
