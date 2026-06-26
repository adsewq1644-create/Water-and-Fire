Shader "WaterAndFire/SpritePlantEdgeBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _HotColor ("Hot Color", Color) = (1, 0.92, 0.2, 1)
        [HDR] _FlameColor ("Flame Color", Color) = (1, 0.25, 0.015, 1)
        _CharColor ("Char Color", Color) = (0.055, 0.018, 0.008, 1)
        _BurnProgress ("Burn Progress", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.5, 8)) = 2
        _FrontWidth ("Burn Front Width", Range(0.02, 0.35)) = 0.12
        _OuterFlameWidth ("Outer Flame Width", Range(0.5, 10)) = 3
        _NoiseScale ("Noise Scale", Range(1, 40)) = 13
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 3.5
        _NoiseAmount ("Noise Amount", Range(0, 0.35)) = 0.14
        _MeltAmount ("Melt Amount", Range(0, 0.5)) = 0.24
        _DripLength ("Drip Length", Range(0, 0.35)) = 0.16
        _DripScale ("Drip Columns", Range(3, 40)) = 18
        _GeometryExpansion ("Geometry Expansion", Range(1, 1.4)) = 1.18
        _CharOpacity ("Char Opacity", Range(0, 1)) = 0.72
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 3
        _Opacity ("Opacity", Range(0, 1)) = 1
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
            float4 _Color;
            float4 _HotColor;
            float4 _FlameColor;
            float4 _CharColor;
            float _BurnProgress;
            float _EdgeWidth;
            float _FrontWidth;
            float _OuterFlameWidth;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseAmount;
            float _MeltAmount;
            float _DripLength;
            float _DripScale;
            float _GeometryExpansion;
            float _CharOpacity;
            float _EmissionIntensity;
            float _Opacity;

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

            float SampleAlpha(float2 uv)
            {
                float inBounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, saturate(uv)).a * inBounds;
            }

            float ErodeAlpha(float2 uv, float radius)
            {
                float2 offset = _MainTex_TexelSize.xy * radius;
                float alpha = SampleAlpha(uv);
                alpha = min(alpha, SampleAlpha(uv + float2(offset.x, 0)));
                alpha = min(alpha, SampleAlpha(uv + float2(-offset.x, 0)));
                alpha = min(alpha, SampleAlpha(uv + float2(0, offset.y)));
                alpha = min(alpha, SampleAlpha(uv + float2(0, -offset.y)));
                alpha = min(alpha, SampleAlpha(uv + offset));
                alpha = min(alpha, SampleAlpha(uv - offset));
                alpha = min(alpha, SampleAlpha(uv + float2(offset.x, -offset.y)));
                alpha = min(alpha, SampleAlpha(uv + float2(-offset.x, offset.y)));
                return alpha;
            }

            float DilateAlpha(float2 uv, float radius)
            {
                float2 offset = _MainTex_TexelSize.xy * radius;
                float alpha = SampleAlpha(uv);
                alpha = max(alpha, SampleAlpha(uv + float2(offset.x, 0)));
                alpha = max(alpha, SampleAlpha(uv + float2(-offset.x, 0)));
                alpha = max(alpha, SampleAlpha(uv + float2(0, offset.y)));
                alpha = max(alpha, SampleAlpha(uv + float2(0, -offset.y)));
                alpha = max(alpha, SampleAlpha(uv + offset));
                alpha = max(alpha, SampleAlpha(uv - offset));
                alpha = max(alpha, SampleAlpha(uv + float2(offset.x, -offset.y)));
                alpha = max(alpha, SampleAlpha(uv + float2(-offset.x, offset.y)));
                return alpha;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float expansion = max(1.0, _GeometryExpansion);
                float4 expandedVertex = v.vertex;
                expandedVertex.xy *= expansion;
                o.vertex = UnityObjectToClipPos(expandedVertex);
                o.uv = (v.texcoord - 0.5) * expansion + 0.5;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float alpha = SampleAlpha(i.uv) * i.color.a;
                float width = max(0.5, _EdgeWidth);
                float erode1 = ErodeAlpha(i.uv, width);
                float erode2 = ErodeAlpha(i.uv, width * 2.0);
                float erode3 = ErodeAlpha(i.uv, width * 4.0);
                float erode4 = ErodeAlpha(i.uv, width * 8.0);

                float ring0 = saturate(alpha - erode1);
                float ring1 = saturate(erode1 - erode2);
                float ring2 = saturate(erode2 - erode3);
                float ring3 = saturate(erode3 - erode4);
                float inner = saturate(erode4);
                float ringSum = max(0.0001, ring0 + ring1 + ring2 + ring3 + inner);
                float depth = (ring0 * 0.03 + ring1 * 0.22 + ring2 * 0.46 + ring3 * 0.72 + inner) / ringSum;

                float time = _Time.y * _NoiseSpeed;
                float coarseNoise = ValueNoise(i.uv * _NoiseScale + float2(time * 0.18, -time * 0.42));
                float fineNoise = ValueNoise(i.uv * (_NoiseScale * 2.1) + float2(-time * 0.31, -time * 0.8));
                float flameWave = sin(i.uv.x * 31.0 + time * 3.2 + coarseNoise * 5.0) * 0.5 + 0.5;
                float column = floor(i.uv.x * _DripScale) / max(1.0, _DripScale);
                float channelNoise = ValueNoise(float2(column * _DripScale * 0.73, i.uv.y * 2.2 - time * 0.28));
                float meltChannel = pow(saturate(channelNoise), 3.5) * _MeltAmount;
                float progressWindow = smoothstep(0.05, 0.42, _BurnProgress);
                float noisyProgress = saturate(
                    _BurnProgress
                    + (coarseNoise - 0.5) * _NoiseAmount
                    + meltChannel * progressWindow);

                float burned = alpha * smoothstep(depth - 0.08, depth + 0.08, noisyProgress);
                float frontDistance = abs(depth - noisyProgress);
                float front = alpha * (1.0 - smoothstep(_FrontWidth, _FrontWidth * 2.2, frontDistance));
                front *= saturate(0.55 + coarseNoise * 0.7 + fineNoise * 0.45);

                float dilated = DilateAlpha(i.uv, _OuterFlameWidth);
                float outerBand = saturate(dilated - alpha);
                float outerLife = 1.0 - smoothstep(0.05, 0.42, _BurnProgress);
                float outerFlame = outerBand * outerLife;
                outerFlame *= smoothstep(0.28, 0.76, coarseNoise * 0.68 + flameWave * 0.48);

                float dripVariation = lerp(0.3, 1.0, ValueNoise(float2(column * 29.0 + 4.7, time * 0.12)));
                float dripDistance = _DripLength * dripVariation;
                float aboveNear = SampleAlpha(i.uv + float2(0, dripDistance * 0.28));
                float aboveMid = SampleAlpha(i.uv + float2(0, dripDistance * 0.58));
                float aboveFar = SampleAlpha(i.uv + float2(0, dripDistance));
                float sourceAbove = max(aboveNear, max(aboveMid, aboveFar));
                float verticalTrail = saturate(sourceAbove - alpha);
                float dripStripe = pow(saturate(ValueNoise(float2(column * 53.0 + 8.1, time * 0.18))), 4.5);
                float dripLife = smoothstep(0.08, 0.3, _BurnProgress)
                    * (1.0 - smoothstep(0.78, 1.0, _BurnProgress));
                float drip = verticalTrail * dripStripe * dripLife;

                float upwardFlicker = smoothstep(0.45, 0.92, fineNoise + flameWave * 0.35);
                float flame = saturate(front * (0.9 + upwardFlicker * 0.9) + outerFlame + drip);
                float hotCore = saturate(
                    front * smoothstep(0.48, 0.88, fineNoise + 0.25)
                    + drip * smoothstep(0.45, 0.85, dripStripe));

                float charFade = 1.0 - smoothstep(0.72, 1.0, _BurnProgress);
                float charAlpha = burned * _CharOpacity * charFade;
                float outputAlpha = saturate(charAlpha + flame) * _Opacity;
                clip(outputAlpha - 0.002);

                float3 fireColor = lerp(_FlameColor.rgb, _HotColor.rgb, hotCore);
                fireColor *= 1.0 + _EmissionIntensity * saturate(flame * 0.85 + hotCore);
                float3 color = lerp(_CharColor.rgb, fireColor, saturate(flame * 1.3));

                return half4(color, outputAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
