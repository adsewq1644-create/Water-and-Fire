Shader "WaterAndFire/SpriteWaterWobble"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PatternTex ("Water Pattern", 2D) = "gray" {}
        _NoiseTex ("Fine Noise", 2D) = "gray" {}
        _RefractionTex ("Distortion Texture", 2D) = "gray" {}

        _WaterColorA ("Deep Water", Color) = (0.0745, 0.3294, 0.6706, 1)
        _WaterColorB ("Soft Water", Color) = (0.5843, 0.8039, 0.9922, 1)
        _StreamColor ("Inner Stream", Color) = (0.451, 1.0, 0.8784, 1)
        _FoamColor ("Rim Highlight", Color) = (0.8588, 0.9255, 0.9255, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.2235, 0.6235, 0.902, 1)

        _UseRectPlaceholderMask ("Use Rect Placeholder Mask", Range(0, 1)) = 0
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.72
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.18)) = 0.045
        _RimStrength ("Rim Strength", Range(0, 2)) = 1.05
        _InnerFlowStrength ("Inner Flow Strength", Range(0, 2)) = 0.55
        _ColorIntensity ("Color Intensity", Range(0, 2)) = 0.92
        _BackgroundVisibility ("Background Visibility", Range(0, 1)) = 0.258
        _BackgroundRefractionStrength ("Background Refraction", Range(0, 1)) = 0.12
        _PixelRefractionSize ("Pixel Refraction Size", Range(1, 16)) = 3
        _PixelPatternSize ("Pixel Pattern Size", Range(8, 256)) = 43.5
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 8
        _RimEmission ("Rim Emission", Range(0, 8)) = 0.1
        _StreamEmission ("Stream Emission", Range(0, 6)) = 1.1
        _OuterGlowStrength ("Outer Glow Strength", Range(0, 4)) = 0
        _OuterGlowSize ("Outer Glow Size", Range(1, 16)) = 1

        _PatternTiling ("Pattern Tiling", Range(0.25, 8)) = 1.35
        _NoiseTiling ("Noise Tiling", Range(0.5, 20)) = 3.4
        _RefractionTiling ("Distortion Tiling", Range(0.25, 12)) = 2.2
        _WobbleStrength ("Wobble Strength", Range(0, 1)) = 0.12
        _WobbleSpeed ("Wobble Speed", Range(0, 8)) = 0.9
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.15
        _SparkleStrength ("Sparkle Strength", Range(0, 2)) = 1.7
        _SilhouetteWobble ("Silhouette Wobble", Range(0, 1)) = 0.7
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
            #pragma target 2.0
            #include "UnityCG.cginc"

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
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraOpaqueTexture;
            sampler2D _PatternTex;
            sampler2D _NoiseTex;
            sampler2D _RefractionTex;
            fixed4 _Color;
            fixed4 _WaterColorA;
            fixed4 _WaterColorB;
            fixed4 _StreamColor;
            fixed4 _FoamColor;
            fixed4 _GlowColor;
            float _UseRectPlaceholderMask;
            float _BodyAlpha;
            float _EdgeSoftness;
            float _RimStrength;
            float _InnerFlowStrength;
            float _ColorIntensity;
            float _BackgroundVisibility;
            float _BackgroundRefractionStrength;
            float _PixelRefractionSize;
            float _PixelPatternSize;
            float _EmissionIntensity;
            float _RimEmission;
            float _StreamEmission;
            float _OuterGlowStrength;
            float _OuterGlowSize;
            float _PatternTiling;
            float _NoiseTiling;
            float _RefractionTiling;
            float _WobbleStrength;
            float _WobbleSpeed;
            float _RefractionStrength;
            float _SparkleStrength;
            float _SilhouetteWobble;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _WobbleSpeed;
                float2 centered = i.uv * 2.0 - 1.0;

                float2 flowA = float2(0.055, 0.115) * time;
                float2 flowB = float2(-0.095, 0.04) * time;
                float2 flowC = float2(0.035, -0.085) * time;
                float2 flowD = float2(0.13, -0.03) * time;

                float2 refractionUv = i.uv * _RefractionTiling + flowB;
                float2 refraction = tex2D(_RefractionTex, refractionUv).rg * 2.0 - 1.0;
                float shapeNoise = tex2D(_RefractionTex, i.uv * (_RefractionTiling * 0.72) + flowC).r - 0.5;
                float fineNoise = tex2D(_NoiseTex, i.uv * _NoiseTiling + flowA).r;
                float lowNoise = tex2D(_NoiseTex, i.uv * (_NoiseTiling * 0.32) + flowD).r;

                float2 distortion = (refraction + (fineNoise - 0.5)) * (_WobbleStrength * _RefractionStrength * 0.075);
                float edgeWaveX = sin(i.uv.y * 18.0 + time * 3.1 + shapeNoise * 2.0);
                float edgeWaveY = sin(i.uv.x * 15.0 - time * 2.4 + lowNoise * 2.6);
                float2 alphaWobble = distortion * (_SilhouetteWobble * 0.65);
                alphaWobble += float2(edgeWaveX, edgeWaveY) * (_MainTex_TexelSize.xy * 2.4 * _SilhouetteWobble);

                fixed spriteAlpha = tex2D(_MainTex, i.uv).a * i.color.a;
                fixed warpedSpriteAlpha = tex2D(_MainTex, i.uv + alphaWobble).a * i.color.a;

                float rectLeft = i.uv.x + edgeWaveX * 0.018 * _SilhouetteWobble;
                float rectRight = 1.0 - i.uv.x - edgeWaveX * 0.018 * _SilhouetteWobble;
                float rectBottom = i.uv.y + edgeWaveY * 0.018 * _SilhouetteWobble;
                float rectTop = 1.0 - i.uv.y - edgeWaveY * 0.018 * _SilhouetteWobble;
                float rectDistance = min(min(rectLeft, rectRight), min(rectBottom, rectTop));
                float rectMask = smoothstep(0.002, _EdgeSoftness, rectDistance) * i.color.a;

                float useRectMask = saturate(_UseRectPlaceholderMask);
                float alphaSource = lerp(lerp(spriteAlpha, warpedSpriteAlpha, saturate(_SilhouetteWobble * 0.45)), rectMask, useRectMask);
                float bodyMask = alphaSource;

                float2 edgeStep = _MainTex_TexelSize.xy * 1.5;
                float alphaLeft = tex2D(_MainTex, i.uv + float2(-edgeStep.x, 0)).a;
                float alphaRight = tex2D(_MainTex, i.uv + float2(edgeStep.x, 0)).a;
                float alphaDown = tex2D(_MainTex, i.uv + float2(0, -edgeStep.y)).a;
                float alphaUp = tex2D(_MainTex, i.uv + float2(0, edgeStep.y)).a;
                float alphaMin = min(min(alphaLeft, alphaRight), min(alphaDown, alphaUp));
                float spriteEdge = saturate((spriteAlpha - alphaMin) * 4.0);
                float rectEdge = 1.0 - smoothstep(_EdgeSoftness * 0.8, _EdgeSoftness * 3.2, rectDistance);
                spriteEdge = lerp(spriteEdge, rectEdge, useRectMask) * bodyMask;

                float2 glowStep = _MainTex_TexelSize.xy * max(1.0, _OuterGlowSize);
                float nearAlpha = 0.0;
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(glowStep.x, 0)).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(-glowStep.x, 0)).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(0, glowStep.y)).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(0, -glowStep.y)).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + glowStep).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv - glowStep).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(glowStep.x, -glowStep.y)).a);
                nearAlpha = max(nearAlpha, tex2D(_MainTex, i.uv + float2(-glowStep.x, glowStep.y)).a);
                float spriteOuterGlow = saturate((nearAlpha * i.color.a - bodyMask) * 1.8);
                float rectOuterGlow = (1.0 - smoothstep(0.0, _EdgeSoftness * max(1.0, _OuterGlowSize), abs(rectDistance))) * (1.0 - bodyMask * 0.65);
                float outerGlow = lerp(spriteOuterGlow, rectOuterGlow, useRectMask) * _OuterGlowStrength;

                float edgePulse = 0.82 + 0.18 * sin(time * 4.1 + fineNoise * 5.0 + centered.x * 3.0);
                float rim = saturate(spriteEdge * _RimStrength * edgePulse) * bodyMask;

                float2 warpA = (tex2D(_RefractionTex, i.uv * 1.2 + flowB).rg * 2.0 - 1.0) * 0.16;
                float2 warpB = (tex2D(_NoiseTex, i.uv * 2.1 + flowC).rg * 2.0 - 1.0) * 0.08;
                float pixelPattern = max(1.0, _PixelPatternSize);
                float2 patternUvA = floor((i.uv * _PatternTiling + flowA + distortion + warpA) * pixelPattern) / pixelPattern;
                float2 patternUvB = floor((i.uv * (_PatternTiling * 1.65) + flowB - distortion * 0.7 + warpB) * pixelPattern) / pixelPattern;
                float2 patternUvC = floor((i.uv * (_PatternTiling * 0.78) + flowD + warpA * 0.45 - warpB) * pixelPattern) / pixelPattern;
                fixed4 patternA = tex2D(_PatternTex, patternUvA);
                fixed4 patternB = tex2D(_PatternTex, patternUvB);
                fixed4 patternC = tex2D(_RefractionTex, patternUvC);
                float flowPattern = saturate(patternA.r * 0.42 + patternB.g * 0.3 + patternC.b * 0.24 + fineNoise * 0.16 - lowNoise * 0.1);
                float veinPattern = saturate(abs(patternA.r - patternB.g) + patternC.r * 0.45 + lowNoise * 0.18);
                float stream = smoothstep(0.58, 0.92, flowPattern) * smoothstep(0.35, 0.86, veinPattern) * bodyMask;

                float softBands = smoothstep(0.28, 0.86, patternA.b * 0.42 + patternB.r * 0.34 + patternC.g * 0.32 + lowNoise * 0.18);
                float3 water = lerp(_WaterColorA.rgb, _WaterColorB.rgb, saturate(softBands * 0.72 + lowNoise * 0.28));
                water = lerp(water, _StreamColor.rgb, saturate(stream * _InnerFlowStrength));

                float highlight = pow(saturate(patternA.r * patternB.g + patternC.b * 0.35 + fineNoise * 0.22), 6.0) * _SparkleStrength * bodyMask;
                float3 insideColor = lerp(water, _FoamColor.rgb, saturate(rim * _RimStrength + highlight));

                float2 screenUv = i.screenPos.xy / i.screenPos.w;
                float2 screenPixel = max(float2(1.0, 1.0), _ScreenParams.xy / max(1.0, _PixelRefractionSize));
                float2 pixelScreenUv = floor(screenUv * screenPixel) / screenPixel;
                float2 backgroundOffset = (refraction + warpA + warpB + (fineNoise - 0.5)) * (_BackgroundRefractionStrength * 0.035) * bodyMask;
                fixed3 backgroundColor = tex2D(_CameraOpaqueTexture, pixelScreenUv + backgroundOffset).rgb;

                float alpha = saturate(bodyMask * _BodyAlpha + rim * 0.12 + highlight * 0.12);
                alpha = saturate(alpha * saturate(bodyMask) + outerGlow * 0.38);
                float3 finalColor = lerp(insideColor * _ColorIntensity, backgroundColor, _BackgroundVisibility * bodyMask);
                finalColor = lerp(finalColor, insideColor * _ColorIntensity, saturate(rim + highlight));
                float emissionMask = saturate(rim * _RimEmission + stream * _StreamEmission + highlight * 2.0 + outerGlow);
                finalColor += _GlowColor.rgb * emissionMask * _EmissionIntensity;
                fixed3 tint = lerp(fixed3(1, 1, 1), saturate(i.color.rgb * 1.15), 0.38);
                finalColor *= tint;

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
