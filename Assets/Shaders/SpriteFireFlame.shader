Shader "WaterAndFire/SpriteFireFlame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlameTex ("Flame Atlas", 2D) = "white" {}
        _SmokeTex ("Smoke Atlas", 2D) = "gray" {}

        [HDR] _CoreColor ("Core Color", Color) = (1, 0.92, 0.22, 1)
        [HDR] _MidColor ("Mid Flame", Color) = (1, 0.45, 0.06, 1)
        [HDR] _EdgeColor ("Edge Flame", Color) = (0.95, 0.17, 0.03, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.34, 0.06, 1)
        [HDR] _OutlineColor ("Outline Color", Color) = (0.38, 0.04, 0.015, 1)
        _SmokeColor ("Smoke Color", Color) = (0.07, 0.055, 0.045, 1)

        _UseRectPlaceholderMask ("Use Rect Placeholder Mask", Range(0, 1)) = 0
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.98
        _PixelGrid ("Pixel Grid", Range(8, 128)) = 44
        _FlameTiling ("Flame Scale", Range(0.5, 6)) = 2.8
        _FlameSpeed ("Flame Speed", Range(0, 8)) = 2.6
        _ShapeShift ("Shape Shift", Range(0, 1)) = 0.5
        _EdgeBurn ("Edge Burn", Range(0, 2)) = 1.2
        _CoreStrength ("Core Strength", Range(0, 2)) = 1.25
        _SmokeAmount ("Smoke Amount", Range(0, 1)) = 0.14
        _EmberAmount ("Ember Amount", Range(0, 1)) = 0.32
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 1.45
        _OutlineWidth ("Outline Width", Range(0, 24)) = 0
        _OutlineStrength ("Outline Strength", Range(0, 2)) = 0
        _OuterFlameWidth ("Outer Flame Width", Range(0, 32)) = 6
        _OuterFlameStrength ("Outer Flame Strength", Range(0, 3)) = 0.55
        _OuterFlameWobble ("Outer Flame Wobble", Range(0, 1)) = 0.85
        _SilhouetteMelt ("Silhouette Melt", Range(0, 1)) = 0.58
        _WindBend ("Wind Bend", Range(-1, 1)) = 0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.55
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
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _FlameTex;
            sampler2D _SmokeTex;
            float4 _Color;
            float4 _CoreColor;
            float4 _MidColor;
            float4 _EdgeColor;
            float4 _GlowColor;
            float4 _OutlineColor;
            float4 _SmokeColor;
            float _UseRectPlaceholderMask;
            float _BodyAlpha;
            float _PixelGrid;
            float _FlameTiling;
            float _FlameSpeed;
            float _ShapeShift;
            float _EdgeBurn;
            float _CoreStrength;
            float _SmokeAmount;
            float _EmberAmount;
            float _EmissionIntensity;
            float _OutlineWidth;
            float _OutlineStrength;
            float _OuterFlameWidth;
            float _OuterFlameStrength;
            float _OuterFlameWobble;
            float _SilhouetteMelt;
            float _WindBend;
            float _WindStrength;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float RectPlaceholderDistance(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                return 0.92 - max(abs(centered.x), abs(centered.y));
            }

            float RectPlaceholderMask(float2 uv, float alpha)
            {
                float2 centered = uv * 2.0 - 1.0;
                float distance = RectPlaceholderDistance(uv);
                float edgeBand = 1.0 - smoothstep(0.02, 0.32, abs(distance));
                float topBias = 0.45 + saturate(uv.y) * 0.75;
                float noise = ValueNoise(float2(uv.x * 7.2 + _Time.y * 0.85, uv.y * 9.4 - _Time.y * 1.15)) - 0.5;
                float wave = sin(uv.y * 23.0 + _Time.y * 4.2 + uv.x * 8.0) * 0.5 + 0.5;
                float cornerRound = smoothstep(0.48, 0.9, abs(centered.x)) * smoothstep(0.48, 0.9, abs(centered.y));
                distance += (noise * 0.105 + (wave - 0.5) * 0.045) * edgeBand * topBias * _SilhouetteMelt;
                distance -= cornerRound * 0.09 * _SilhouetteMelt;
                return smoothstep(0.0, 0.13, distance) * alpha;
            }

            float UvInBounds(float2 uv)
            {
                return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
            }

            float SpriteMask(float2 uv, float alpha)
            {
                float2 clampedUv = saturate(uv);
                float inBounds = UvInBounds(uv);
                float spriteAlpha = tex2D(_MainTex, clampedUv).a * alpha * inBounds;
                float rectAlpha = RectPlaceholderMask(clampedUv, alpha) * inBounds;
                return lerp(spriteAlpha, rectAlpha, saturate(_UseRectPlaceholderMask));
            }

            float DilatedSpriteMask(float2 uv, float radius, float alpha)
            {
                float2 stepUv = _MainTex_TexelSize.xy * max(1.0, radius);
                float2 halfStepUv = stepUv * 0.5;
                float nearAlpha = SpriteMask(uv, alpha);

                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(stepUv.x, 0), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(-stepUv.x, 0), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(0, stepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(0, -stepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + stepUv, alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv - stepUv, alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(stepUv.x, -stepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(-stepUv.x, stepUv.y), alpha));

                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(halfStepUv.x, 0), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(-halfStepUv.x, 0), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(0, halfStepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(0, -halfStepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + halfStepUv, alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv - halfStepUv, alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(halfStepUv.x, -halfStepUv.y), alpha));
                nearAlpha = max(nearAlpha, SpriteMask(uv + float2(-halfStepUv.x, halfStepUv.y), alpha));

                return saturate(nearAlpha);
            }

            float SampleAtlas3x3(sampler2D atlas, float2 localUv, float frame)
            {
                float id = fmod(frame, 9.0);
                float col = fmod(id, 3.0);
                float row = floor(id / 3.0);
                float2 atlasUv = (float2(col, 2.0 - row) + saturate(localUv)) / 3.0;
                return tex2D(atlas, atlasUv).r;
            }

            float FlameParticle(float2 uv, float2 center, float2 size, float frame, float bend, float stretch)
            {
                float2 local = (uv - center) / size + 0.5;
                float height = saturate(local.y);
                local.x -= bend * height * height;
                local.y = (local.y - 0.5) / max(0.2, stretch) + 0.5;

                float inside = step(0.0, local.x) * step(local.x, 1.0) * step(0.0, local.y) * step(local.y, 1.0);
                float mask = SampleAtlas3x3(_FlameTex, local, frame) * inside;
                return smoothstep(0.18, 0.78, mask);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _FlameSpeed;
                float grid = max(1.0, _PixelGrid);
                float2 pixelUv = (floor(i.uv * grid) + 0.5) / grid;
                float height01 = saturate(pixelUv.y);
                float centerWeight = saturate(1.0 - abs(pixelUv.x * 2.0 - 1.0));

                float wind = _WindBend * _WindStrength;
                float topWind = wind * height01 * height01;
                float flutter = (ValueNoise(float2(pixelUv.x * 6.0, pixelUv.y * 7.0 - time * 1.25)) - 0.5);
                float sideFlicker = sin(pixelUv.y * 21.0 + time * 3.4 + pixelUv.x * 7.0) * 0.018;

                float2 warpedUv = i.uv;
                warpedUv.x += topWind * (0.32 + height01 * 0.55);
                warpedUv.x += flutter * _ShapeShift * 0.08 * (0.2 + height01);
                warpedUv.x += sideFlicker * _ShapeShift * height01;
                warpedUv.y += (ValueNoise(pixelUv * 5.5 + time * 0.22) - 0.5) * 0.035 * _ShapeShift;

                float stableMask = SpriteMask(i.uv, i.color.a);
                float movingMask = SpriteMask(warpedUv, i.color.a);
                float bodyMask = saturate(lerp(stableMask, movingMask, _ShapeShift));

                float2 edgeStep = _MainTex_TexelSize.xy * 1.75;
                float alphaLeft = SpriteMask(i.uv + float2(-edgeStep.x, 0), i.color.a);
                float alphaRight = SpriteMask(i.uv + float2(edgeStep.x, 0), i.color.a);
                float alphaDown = SpriteMask(i.uv + float2(0, -edgeStep.y), i.color.a);
                float alphaUp = SpriteMask(i.uv + float2(0, edgeStep.y), i.color.a);
                float2 edgeGradient = float2(alphaRight - alphaLeft, alphaUp - alphaDown);
                float2 centerDirection = normalize(pixelUv - 0.5 + float2(0.0001, 0.0001));
                float2 outward = normalize(lerp(centerDirection, -edgeGradient, step(0.0001, dot(edgeGradient, edgeGradient))) + float2(0.0001, 0.0001));
                float edgeMask = saturate((stableMask - min(min(alphaLeft, alphaRight), min(alphaDown, alphaUp))) * 5.0);
                float upperEdge = edgeMask * smoothstep(0.22, 0.95, height01);

                float useRectMask = saturate(_UseRectPlaceholderMask);
                float rectDistance = RectPlaceholderDistance(i.uv);
                float outlineNear = DilatedSpriteMask(i.uv, _OutlineWidth, i.color.a);
                float spriteOutlineMask = saturate((outlineNear - bodyMask) * 3.0) * _OutlineStrength;
                float rectOutlineWidth = max(0.002, _OutlineWidth * 0.006);
                float rectOutlineMask = saturate((1.0 - smoothstep(0.0, rectOutlineWidth, abs(rectDistance))) * (1.0 - bodyMask * 0.65)) * _OutlineStrength;
                float outlineMask = lerp(spriteOutlineMask, rectOutlineMask, useRectMask);

                float outerNoise = ValueNoise(float2(pixelUv.x * 8.0 + time * 0.55, pixelUv.y * 10.0 - time * 1.1));
                float outerWave = sin(pixelUv.y * 22.0 + time * 4.1 + pixelUv.x * 8.0) * 0.5 + 0.5;
                float2 outerUv = i.uv - outward * (outerNoise * 0.045 + outerWave * 0.028) * _OuterFlameWobble;
                outerUv.x += wind * height01 * 0.14;
                float outerNear = DilatedSpriteMask(outerUv, _OuterFlameWidth, i.color.a);
                float spriteOuterBand = saturate((outerNear - bodyMask) * 2.0) * _OuterFlameStrength;
                float rectOuterDistance = RectPlaceholderDistance(outerUv);
                float rectOuterWidth = max(0.004, _OuterFlameWidth * 0.006);
                float rectOutsideDistance = max(0.0, -rectOuterDistance);
                float rectOuterBand = (1.0 - smoothstep(0.0, rectOuterWidth, rectOutsideDistance)) * step(rectOuterDistance, 0.0) * _OuterFlameStrength;
                float outerBand = lerp(spriteOuterBand, rectOuterBand, useRectMask);

                float anim = frac(time * 0.28);
                float frameA = floor(frac(time * 0.42) * 9.0);
                float frameB = floor(frac(time * 0.35 + 0.33) * 9.0);
                float frameC = floor(frac(time * 0.49 + 0.62) * 9.0);
                float frameD = floor(frac(time * 0.56 + 0.18) * 9.0);
                float scale = lerp(1.18, 0.72, saturate((_FlameTiling - 0.5) / 5.5));

                float2 center0 = float2(0.50 + wind * 0.10, 0.26 + anim * 0.14);
                float2 center1 = float2(0.36 + wind * 0.28 + sin(time * 1.2) * 0.025, 0.50 + frac(anim + 0.28) * 0.18);
                float2 center2 = float2(0.64 + wind * 0.28 + sin(time * 1.35 + 2.1) * 0.025, 0.52 + frac(anim + 0.60) * 0.17);
                float2 center3 = float2(0.50 + wind * 0.42 + sin(time * 1.65 + 1.3) * 0.018, 0.72 + frac(anim + 0.12) * 0.13);

                float p0 = FlameParticle(pixelUv, center0, float2(0.76, 0.62) * scale, frameA, wind * 0.35, 1.05);
                float p1 = FlameParticle(pixelUv, center1, float2(0.48, 0.55) * scale, frameB, wind * 0.52, 1.12);
                float p2 = FlameParticle(pixelUv, center2, float2(0.48, 0.55) * scale, frameC, wind * 0.52, 1.12);
                float p3 = FlameParticle(pixelUv, center3, float2(0.36, 0.42) * scale, frameD, wind * 0.75, 1.22);
                float flameField = saturate(max(max(p0, p1), max(p2, p3)));

                float edgeLeft = FlameParticle(pixelUv, float2(0.15 + wind * 0.34, 0.55 + frac(anim + 0.47) * 0.20), float2(0.32, 0.42) * scale, frameC + 2.0, wind * 0.7, 1.25);
                float edgeRight = FlameParticle(pixelUv, float2(0.85 + wind * 0.34, 0.55 + frac(anim + 0.13) * 0.20), float2(0.32, 0.42) * scale, frameB + 4.0, wind * 0.7, 1.25);
                float edgeTop = FlameParticle(pixelUv, float2(0.50 + wind * 0.52, 0.92), float2(0.44, 0.36) * scale, frameD + 1.0, wind * 0.9, 1.3);
                float edgeParticle = max(max(edgeLeft, edgeRight), edgeTop);
                float edgeFlame = saturate(edgeParticle * upperEdge * _EdgeBurn);
                float outerFlame = saturate(outerBand * max(edgeParticle, flameField * 0.45 + outerNoise * 0.35) * (0.5 + outerWave * 0.7));

                float lowerHeat = 1.0 - smoothstep(0.20, 0.88, height01);
                float fillHeat = saturate(flameField * 0.95 + lowerHeat * 0.75 + centerWeight * 0.18);
                float hotCore = smoothstep(0.62, 1.15, p0 * 0.95 + centerWeight * lowerHeat * 1.1 + flameField * 0.3) * _CoreStrength;

                float smokeSample = tex2D(_SmokeTex, frac(float2(pixelUv.x * 1.15 + wind * 0.2, pixelUv.y * 1.05 - time * 0.1))).r;
                float smokeMask = smoothstep(0.56, 0.92, smokeSample) * smoothstep(0.55, 0.98, height01) * _SmokeAmount * bodyMask;
                float emberNoise = ValueNoise(float2(pixelUv.x * 13.0 - time * 0.5, pixelUv.y * 16.0 + time * 1.4));
                float ember = step(0.82, emberNoise) * flameField * smoothstep(0.28, 1.0, height01) * _EmberAmount * bodyMask;

                float alpha = bodyMask * _BodyAlpha;
                alpha *= saturate(0.52 + fillHeat * 0.72 + edgeFlame * 0.24);
                alpha = saturate(alpha + outlineMask * 0.55 + outerFlame * 0.7 + edgeFlame * 0.3 + ember * 0.15);

                float3 color = lerp(_EdgeColor.rgb, _MidColor.rgb, fillHeat);
                color = lerp(color, _CoreColor.rgb, saturate(hotCore));
                color = lerp(color, _SmokeColor.rgb, smokeMask);
                color = lerp(color, _OutlineColor.rgb, saturate(outlineMask * 0.85));
                color = lerp(color, _MidColor.rgb, saturate(outerFlame * 0.9));
                color = lerp(color, _CoreColor.rgb, saturate(outerFlame * edgeParticle * 0.45));
                color += _GlowColor.rgb * saturate(hotCore * 0.55 + edgeFlame * 0.65 + outlineMask * 0.45 + outerFlame + ember) * _EmissionIntensity;
                color += _MidColor.rgb * ember * _EmissionIntensity;

                float3 spriteTint = lerp(float3(1.0, 1.0, 1.0), saturate(i.color.rgb * 1.05), 0.16);
                color *= spriteTint;

                return half4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
