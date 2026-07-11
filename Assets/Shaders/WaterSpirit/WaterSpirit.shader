Shader "WaterAndFire/WaterSpirit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _LargeNoiseTex ("Large Flow Noise", 2D) = "gray" {}
        _DetailNoiseTex ("Detail Flow Noise", 2D) = "gray" {}

        [HDR] _ShallowColor ("Shallow Color", Color) = (0.12, 0.5, 0.88, 1)
        [HDR] _DeepColor ("Deep Color", Color) = (0.015, 0.1, 0.38, 1)
        [HDR] _HighlightColor ("Highlight Color", Color) = (0.25, 0.9, 1.15, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.45, 0.95, 1.15, 1)
        _OverallAlpha ("Overall Alpha", Range(0, 1)) = 0.88

        _LargeFlowScale ("Large Flow Scale", Range(0.1, 8)) = 0.92
        _LargeFlowSpeed ("Large Flow Speed", Range(0, 2)) = 0.12
        _LargeFlowDirection ("Large Flow Direction", Vector) = (0.37, 0.18, 0, 0)
        _LargeDistortionStrength ("Large Distortion Strength", Range(0, 0.5)) = 0.17

        _DetailFlowScale ("Detail Flow Scale", Range(0.25, 16)) = 1.35
        _DetailFlowSpeed ("Detail Flow Speed", Range(0, 3)) = 0.19
        _DetailFlowDirection ("Detail Flow Direction", Vector) = (-0.16, 0.31, 0, 0)
        _DetailDistortionStrength ("Detail Distortion Strength", Range(0, 0.3)) = 0.045

        _PatternContrast ("Pattern Contrast", Range(0.1, 4)) = 1.3
        _PatternSoftness ("Pattern Softness", Range(0.005, 0.5)) = 0.17
        _PatternThreshold ("Pattern Threshold", Range(0, 1)) = 0.53
        _PixelSize ("Pixel Size", Range(2, 128)) = 36
        _PixelationStrength ("Pixelation Strength", Range(0, 1)) = 0.58

        _InternalWobbleAmount ("Internal Wobble Amount", Range(0, 0.5)) = 0.08
        _InternalWobbleSpeed ("Internal Wobble Speed", Range(0, 3)) = 0.18
        _SpriteDistortionStrength ("Sprite Distortion Strength", Range(0, 0.08)) = 0.018
        _VerticalStretchVisual ("Vertical Stretch Visual", Range(-0.4, 0.4)) = 0
        _HorizontalStretchVisual ("Horizontal Stretch Visual", Range(-0.4, 0.4)) = 0

        _EdgeWidth ("Edge Width", Range(0.25, 6)) = 1.3
        _EdgeStrength ("Edge Strength", Range(0, 3)) = 0.7
        _EdgeFlowInfluence ("Edge Flow Influence", Range(0, 1)) = 0.55

        _EmissionStrength ("Emission Strength", Range(0, 3)) = 0.18
        _HighlightEmissionStrength ("Highlight Emission Strength", Range(0, 4)) = 0.68
        _EdgeEmissionStrength ("Edge Emission Strength", Range(0, 4)) = 0.45

        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _FlowSpeedMultiplier ("Flow Speed Multiplier", Float) = 1
        [HideInInspector] _DistortionMultiplier ("Distortion Multiplier", Float) = 1
        [HideInInspector] _VisualStretch ("Visual Stretch", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FlowPull ("Flow Pull", Vector) = (0, 0, 0, 0)
        [HideInInspector] _LandingPulse ("Landing Pulse", Float) = 0
        [HideInInspector] _LandingPulseProgress ("Landing Pulse Progress", Float) = 0
        [HideInInspector] _TimeOffset ("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_SKINNED_VERTEX_INPUTS
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            half2 lightingUV : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_LargeNoiseTex);
        SAMPLER(sampler_LargeNoiseTex);
        TEXTURE2D(_DetailNoiseTex);
        SAMPLER(sampler_DetailNoiseTex);

        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _HighlightColor;
            half4 _EdgeColor;
            float4 _LargeFlowDirection;
            float4 _DetailFlowDirection;
            float4 _SpriteUVRect;
            float4 _VisualStretch;
            float4 _FlowPull;
            float _OverallAlpha;
            float _LargeFlowScale;
            float _LargeFlowSpeed;
            float _LargeDistortionStrength;
            float _DetailFlowScale;
            float _DetailFlowSpeed;
            float _DetailDistortionStrength;
            float _PatternContrast;
            float _PatternSoftness;
            float _PatternThreshold;
            float _PixelSize;
            float _PixelationStrength;
            float _InternalWobbleAmount;
            float _InternalWobbleSpeed;
            float _SpriteDistortionStrength;
            float _VerticalStretchVisual;
            float _HorizontalStretchVisual;
            float _EdgeWidth;
            float _EdgeStrength;
            float _EdgeFlowInfluence;
            float _EmissionStrength;
            float _HighlightEmissionStrength;
            float _EdgeEmissionStrength;
            float _FlowSpeedMultiplier;
            float _DistortionMultiplier;
            float _LandingPulse;
            float _LandingPulseProgress;
            float _TimeOffset;
        CBUFFER_END

        float2 SafeDirection(float2 value, float2 fallbackValue)
        {
            float lengthSquared = dot(value, value);
            return lengthSquared > 0.00001 ? value * rsqrt(lengthSquared) : fallbackValue;
        }

        float2 Rotate90(float2 value)
        {
            return float2(-value.y, value.x);
        }

        float2 ToLocalSpriteUV(float2 atlasUV)
        {
            float2 size = max(_SpriteUVRect.zw, float2(0.00001, 0.00001));
            return saturate((atlasUV - _SpriteUVRect.xy) / size);
        }

        float2 ToAtlasUV(float2 localUV)
        {
            return _SpriteUVRect.xy + saturate(localUV) * _SpriteUVRect.zw;
        }

        float2 ClampAtlasUV(float2 atlasUV, float2 padding)
        {
            float2 rectMin = _SpriteUVRect.xy + padding;
            float2 rectMax = _SpriteUVRect.xy + _SpriteUVRect.zw - padding;
            return clamp(atlasUV, rectMin, max(rectMin, rectMax));
        }

        float2 PixelateUV(float2 uv)
        {
            float cells = max(_PixelSize, 1.0);
            float2 snapped = (floor(uv * cells) + 0.5) / cells;
            return lerp(uv, snapped, saturate(_PixelationStrength) * 0.42);
        }

        Varyings WaterVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            UNITY_SKINNED_VERTEX_COMPUTE(input);

            SetUpSpriteInstanceProperties();
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = input.color * _Color * unity_SpriteColor;
            output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
            return output;
        }

        half3 EvaluateWater(Varyings input, out half alpha, out half3 emission)
        {
            half4 originalSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            alpha = saturate(originalSample.a * _OverallAlpha);

            float2 localUV = ToLocalSpriteUV(input.uv);
            float2 centered = localUV - 0.5;
            float flowSpeed = max(_FlowSpeedMultiplier, 0.01);
            float distortionMultiplier = max(_DistortionMultiplier, 0.0);
            float timeValue = (_Time.y + _TimeOffset) * flowSpeed;

            float2 stretch = float2(
                1.0 + _HorizontalStretchVisual + _VisualStretch.x,
                1.0 + _VerticalStretchVisual + _VisualStretch.y);
            stretch = max(stretch, float2(0.55, 0.55));

            float2 flowUV = centered / stretch + 0.5;
            float wobbleTime = timeValue * _InternalWobbleSpeed;
            flowUV.x += sin((flowUV.y * 2.17 + wobbleTime * 0.19) * TWO_PI) * _InternalWobbleAmount * 0.075;
            flowUV.y += cos((flowUV.x * 1.83 - wobbleTime * 0.13) * TWO_PI) * _InternalWobbleAmount * 0.055;
            flowUV += _FlowPull.xy * 0.065;

            float2 largeDirection = SafeDirection(_LargeFlowDirection.xy, float2(0.9, 0.35));
            float largeTime = timeValue * _LargeFlowSpeed;
            float largeScale = max(_LargeFlowScale, 0.05);
            float2 largeUvA = flowUV * largeScale + largeDirection * largeTime;
            float2 largeUvB = Rotate90(flowUV - 0.5) * (largeScale * 0.73) + 0.5;
            largeUvB += float2(-largeDirection.y, largeDirection.x) * (-largeTime * 0.71) + float2(0.37, 0.19);

            float3 largeA = SAMPLE_TEXTURE2D(_LargeNoiseTex, sampler_LargeNoiseTex, largeUvA).rgb;
            float3 largeB = SAMPLE_TEXTURE2D(_LargeNoiseTex, sampler_LargeNoiseTex, largeUvB).rgb;
            float2 largeVector = (largeA.rg * 2.0 - 1.0) * 0.64 + (largeB.gr * 2.0 - 1.0) * 0.36;
            float largePattern = saturate(largeA.b * 0.58 + largeB.r * 0.42);

            float2 radialDirection = centered * rsqrt(max(dot(centered, centered), 0.0001));
            float pulseRadius = lerp(0.04, 0.72, saturate(_LandingPulseProgress));
            float pulseRing = 1.0 - smoothstep(0.035, 0.13, abs(length(centered) - pulseRadius));
            float2 pulseWarp = radialDirection * pulseRing * _LandingPulse * 0.045;

            float2 domainWarp = largeVector * _LargeDistortionStrength * distortionMultiplier;
            domainWarp += pulseWarp;

            float2 detailDirection = SafeDirection(_DetailFlowDirection.xy, float2(-0.35, 0.94));
            float detailTime = timeValue * _DetailFlowSpeed;
            float detailScale = max(_DetailFlowScale, 0.05);
            float2 detailUvA = flowUV * detailScale + detailDirection * detailTime + domainWarp;
            float2 detailUvB = Rotate90(flowUV - 0.5) * (detailScale * 0.81) + 0.5;
            detailUvB += float2(detailDirection.y, -detailDirection.x) * (detailTime * 0.79) - domainWarp * 0.63 + float2(0.11, 0.43);

            detailUvA = PixelateUV(detailUvA);
            detailUvB = PixelateUV(detailUvB);

            float3 detailA = SAMPLE_TEXTURE2D(_DetailNoiseTex, sampler_DetailNoiseTex, detailUvA).rgb;
            float3 detailB = SAMPLE_TEXTURE2D(_DetailNoiseTex, sampler_DetailNoiseTex, detailUvB).rgb;
            float2 detailVector = (detailA.rg * 2.0 - 1.0) * 0.61 + (detailB.gr * 2.0 - 1.0) * 0.39;

            float2 finalDetailUV = detailUvA + detailVector * _DetailDistortionStrength * distortionMultiplier;
            finalDetailUV -= domainWarp * 0.21;
            finalDetailUV = PixelateUV(finalDetailUV);
            float3 finalDetail = SAMPLE_TEXTURE2D(_DetailNoiseTex, sampler_DetailNoiseTex, finalDetailUV).rgb;

            float broadDetailScale = max(detailScale * 0.38, 0.45);
            float2 broadDetailUV = flowUV * broadDetailScale;
            broadDetailUV += detailDirection * (detailTime * 0.72) + domainWarp * 1.35 + float2(0.27, 0.09);
            broadDetailUV = PixelateUV(broadDetailUV);
            float3 broadDetail = SAMPLE_TEXTURE2D(_DetailNoiseTex, sampler_DetailNoiseTex, broadDetailUV).rgb;

            float warpedRidgeA = 1.0 - abs(broadDetail.r * 2.0 - 1.0);
            float warpedRidgeB = 1.0 - abs(broadDetail.g * 2.0 - 1.0);
            float broadRidge = 1.0 - abs(largeA.r * 2.0 - 1.0);
            float detailRidge = 1.0 - abs(finalDetail.r * 2.0 - 1.0);
            float causticRaw = saturate(
                pow(warpedRidgeA, 4.0) * 0.5
                + pow(warpedRidgeB, 5.0) * 0.25
                + pow(broadRidge, 5.0) * 0.22
                + pow(detailRidge, 5.0) * 0.03);
            causticRaw *= lerp(0.82, 1.12, finalDetail.g);

            float depthRaw = saturate(largePattern * 0.72 + finalDetail.r * 0.18 + detailB.g * 0.1);
            float contrastedDepth = saturate((depthRaw - 0.5) * _PatternContrast + 0.5);
            float softness = max(_PatternSoftness, 0.005);
            float waterPattern = smoothstep(_PatternThreshold - softness, _PatternThreshold + softness, contrastedDepth);

            float highlightThreshold = saturate(_PatternThreshold - 0.18);
            float highlight = smoothstep(highlightThreshold, 0.84, causticRaw);
            highlight = saturate(highlight + pulseRing * _LandingPulse * 0.72);

            float patternSteps = max(2.0, floor(_PixelSize * 0.18));
            float pixelatedPattern = floor(waterPattern * patternSteps + 0.5) / patternSteps;
            waterPattern = lerp(waterPattern, pixelatedPattern, saturate(_PixelationStrength) * 0.52);

            float2 spriteColorOffset = (domainWarp * 0.43 + detailVector * _DetailDistortionStrength) * _SpriteDistortionStrength;
            float2 warpedLocalUV = saturate(localUV + spriteColorOffset);
            float2 warpedAtlasUV = ClampAtlasUV(ToAtlasUV(warpedLocalUV), _MainTex_TexelSize.xy * 0.5);
            half3 spriteColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedAtlasUV).rgb * input.color.rgb;
            half sourceLuma = dot(spriteColor, half3(0.2126, 0.7152, 0.0722));

            float2 edgeStep = _MainTex_TexelSize.xy * max(_EdgeWidth, 0.25);
            float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
            half alphaLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ClampAtlasUV(input.uv - float2(edgeStep.x, 0), halfTexel)).a;
            half alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ClampAtlasUV(input.uv + float2(edgeStep.x, 0), halfTexel)).a;
            half alphaDown = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ClampAtlasUV(input.uv - float2(0, edgeStep.y), halfTexel)).a;
            half alphaUp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ClampAtlasUV(input.uv + float2(0, edgeStep.y), halfTexel)).a;
            half neighborAlpha = min(min(alphaLeft, alphaRight), min(alphaDown, alphaUp)) * input.color.a;
            float edge = saturate((originalSample.a - neighborAlpha) * 4.0) * originalSample.a;
            float edgeFlow = lerp(1.0, 0.58 + waterPattern * 0.42 + highlight * 0.3, _EdgeFlowInfluence);
            edge = saturate(edge * _EdgeStrength * edgeFlow);

            half3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, waterPattern);
            waterColor = lerp(waterColor, _HighlightColor.rgb, highlight * 0.72);
            waterColor *= lerp(0.9, 1.08, sourceLuma);
            waterColor = lerp(waterColor, _EdgeColor.rgb, edge * 0.78);
            waterColor *= input.color.rgb;

            emission = _HighlightColor.rgb * highlight * _HighlightEmissionStrength;
            emission += _EdgeColor.rgb * edge * _EdgeEmissionStrength;
            emission *= _EmissionStrength * originalSample.a;
            return waterColor;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterLitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 WaterLitFragment(Varyings input) : SV_Target
            {
                half alpha;
                half3 emission;
                half3 waterColor = EvaluateWater(input, alpha, emission);

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(waterColor, alpha, half4(1, 1, 1, 1), surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

                half4 output = CombinedShapeLightShared(surfaceData, inputData);
                output.rgb += emission;
                output.a = alpha;
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterUnlitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            half4 WaterUnlitFragment(Varyings input) : SV_Target
            {
                half alpha;
                half3 emission;
                half3 waterColor = EvaluateWater(input, alpha, emission);
                return half4(waterColor + emission, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
