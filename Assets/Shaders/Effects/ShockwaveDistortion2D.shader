Shader "Hidden/WaterAndFire/ShockwaveDistortion2D"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ShockwaveDistortion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_SHOCKWAVES 4
            #define MAX_DARK_ZONE_RECTS 8

            int _ShockwaveCount;
            int _DarkZoneRectCount;
            float _DarkZoneEdgeSoftness;
            float _ShockwaveGlobalStrength;
            float4 _ShockwaveCenterRadiusWidth[MAX_SHOCKWAVES];
            float4 _ShockwaveParameters0[MAX_SHOCKWAVES];
            float4 _ShockwaveParameters1[MAX_SHOCKWAVES];
            float4 _ShockwaveParameters2[MAX_SHOCKWAVES];
            float4 _ShockwaveParameters3[MAX_SHOCKWAVES];
            float4 _ShockwaveArcDirections[MAX_SHOCKWAVES];
            float4 _ShockwaveTints[MAX_SHOCKWAVES];
            float4 _DarkZoneRects[MAX_DARK_ZONE_RECTS];

            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(
                    samplePosition * float2(123.34, 456.21));
                samplePosition += dot(
                    samplePosition,
                    samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 fraction = frac(samplePosition);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);

                float bottomLeft = Hash21(cell);
                float bottomRight = Hash21(cell + float2(1.0, 0.0));
                float topLeft = Hash21(cell + float2(0.0, 1.0));
                float topRight = Hash21(cell + float2(1.0, 1.0));
                return lerp(
                    lerp(bottomLeft, bottomRight, fraction.x),
                    lerp(topLeft, topRight, fraction.x),
                    fraction.y);
            }

            float BandMask(
                float signedDistance,
                float bandCenter,
                float halfWidth,
                float softness)
            {
                return 1.0 - smoothstep(
                    halfWidth,
                    halfWidth + softness,
                    abs(signedDistance - bandCenter));
            }

            float DarkZoneMask(float2 uv)
            {
                float mask = 0.0;
                float softness = max(0.00001, _DarkZoneEdgeSoftness);

                [unroll]
                for (int index = 0; index < MAX_DARK_ZONE_RECTS; index++)
                {
                    if (index >= _DarkZoneRectCount)
                    {
                        break;
                    }

                    float4 rect = _DarkZoneRects[index];
                    float2 enter = smoothstep(
                        rect.xy,
                        rect.xy + softness,
                        uv);
                    float2 leave = 1.0 - smoothstep(
                        rect.zw - softness,
                        rect.zw,
                        uv);
                    mask = max(mask, enter.x * enter.y * leave.x * leave.y);
                }

                return saturate(mask);
            }

            float ArcMask(
                float2 radialDirection,
                float shapeMode,
                float arcCosine,
                float2 arcDirection,
                float edgeFade)
            {
                if (shapeMode < 0.5)
                {
                    return 1.0;
                }

                float alignment = dot(radialDirection, normalize(arcDirection));
                return smoothstep(
                    arcCosine - edgeFade,
                    arcCosine + edgeFade,
                    alignment);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float aspect = max(
                    0.0001,
                    _BlitTexture_TexelSize.z / _BlitTexture_TexelSize.w);
                float zoneMask = DarkZoneMask(uv);
                float2 accumulatedOffset = float2(0.0, 0.0);
                float3 accumulatedHighlight = float3(0.0, 0.0, 0.0);

                [unroll]
                for (int index = 0; index < MAX_SHOCKWAVES; index++)
                {
                    if (index >= _ShockwaveCount)
                    {
                        break;
                    }

                    float4 centerRadiusWidth = _ShockwaveCenterRadiusWidth[index];
                    float4 parameters0 = _ShockwaveParameters0[index];
                    float4 parameters1 = _ShockwaveParameters1[index];
                    float4 parameters2 = _ShockwaveParameters2[index];
                    float4 parameters3 = _ShockwaveParameters3[index];

                    float2 delta = uv - centerRadiusWidth.xy;
                    float2 metricDelta = delta * float2(aspect, 1.0);
                    float distanceFromCenter = length(metricDelta);
                    float2 radialDirection = distanceFromCenter > 0.00001
                        ? metricDelta / distanceFromCenter
                        : float2(0.0, 1.0);

                    float radius = max(0.00001, centerRadiusWidth.z);
                    float width = max(0.0002, centerRadiusWidth.w);
                    float noise = ValueNoise(
                        radialDirection * parameters1.x * 0.75
                        + radius * parameters1.x
                        + _Time.y * parameters1.z);
                    noise = noise * 2.0 - 1.0;
                    // Keep the silhouette stable as the wave expands. Noise only
                    // offsets a small fraction of the pressure-band width.
                    float radiusJitterReference = min(radius, width * 3.0);
                    float noisyRadius =
                        radius + noise * radiusJitterReference * parameters1.y;

                    // Creature snore waves advance a deformation phase along
                    // the arc while the radius expands. The lobes therefore
                    // flow sideways instead of scaling as a frozen silhouette.
                    float2 arcDirection = normalize(
                        _ShockwaveArcDirections[index].xy);
                    float2 arcTangent =
                        float2(-arcDirection.y, arcDirection.x);
                    float arcCoordinate = atan2(
                        dot(radialDirection, arcTangent),
                        dot(radialDirection, arcDirection));
                    float undulationFrequency =
                        _ShockwaveArcDirections[index].z;
                    float undulationSpeed =
                        _ShockwaveArcDirections[index].w;
                    float undulationPhase =
                        arcCoordinate * undulationFrequency -
                        parameters3.y * undulationSpeed * 6.2831853;
                    float primaryUndulation = sin(undulationPhase);
                    float secondaryUndulation = sin(
                        arcCoordinate * undulationFrequency * 0.57 +
                        parameters3.y *
                        undulationSpeed *
                        6.2831853 *
                        0.73 +
                        1.7);
                    float travelingUndulation =
                        (primaryUndulation + secondaryUndulation * 0.28) /
                        1.28;
                    noisyRadius +=
                        travelingUndulation *
                        width *
                        parameters3.w;
                    float signedDistance = distanceFromCenter - noisyRadius;

                    float arcMask = ArcMask(
                        radialDirection,
                        parameters0.y,
                        parameters0.z,
                        _ShockwaveArcDirections[index].xy,
                        parameters1.w);
                    float softness = width * lerp(
                        0.08,
                        0.72,
                        parameters3.x);
                    float leading = BandMask(
                        signedDistance,
                        width * 0.32,
                        width * 0.16,
                        softness);
                    float mainBand = BandMask(
                        signedDistance,
                        0.0,
                        width * 0.48,
                        softness);
                    float trailing = BandMask(
                        signedDistance,
                        -width * 0.64,
                        width * 0.3,
                        softness);

                    float normalizedBandDistance = signedDistance / width;
                    float phase =
                        normalizedBandDistance * parameters2.x
                        - _Time.y * parameters2.y * 0.35
                        + noise * 0.35;
                    float gentleRipple = sin(phase) * 0.1;

                    // A pressure wave reads as one broad displacement:
                    // compression at the front, a wide moving air mass, then a
                    // weaker opposite pull as the image returns to rest.
                    float broadPressure =
                        leading * 0.55
                        + mainBand * (0.42 + gentleRipple)
                        - trailing * parameters2.z * 0.45;
                    float waveMask =
                        arcMask *
                        zoneMask *
                        parameters0.w *
                        _ShockwaveGlobalStrength;
                    float2 screenRadialDirection =
                        radialDirection / float2(aspect, 1.0);
                    float2 screenTangentDirection =
                        float2(-radialDirection.y, radialDirection.x) /
                        float2(aspect, 1.0);
                    float organicShear =
                        noise * 0.7 +
                        sin(phase * 0.65) * 0.3;

                    accumulatedOffset +=
                        screenRadialDirection *
                        broadPressure *
                        parameters0.x *
                        waveMask;
                    accumulatedOffset +=
                        screenTangentDirection *
                        organicShear *
                        parameters0.x *
                        parameters3.z *
                        mainBand *
                        waveMask;
                    accumulatedHighlight +=
                        _ShockwaveTints[index].rgb *
                        leading *
                        parameters2.w *
                        waveMask;
                }

                float2 distortedUv = saturate(uv + accumulatedOffset);
                half4 color = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    distortedUv,
                    0);
                color.rgb += accumulatedHighlight;
                return color;
            }
            ENDHLSL
        }
    }
}
