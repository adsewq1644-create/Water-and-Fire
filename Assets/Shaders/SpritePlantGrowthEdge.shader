Shader "WaterAndFire/SpritePlantGrowthEdge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _CoreColor ("Core Color", Color) = (0.12, 1.25, 0.2, 1)
        [HDR] _GrowthColor ("Growth Color", Color) = (0.025, 0.72, 0.08, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.008, 0.22, 0.025, 1)
        _GrowthProgress ("Growth Progress", Range(0, 1)) = 0
        _FragmentScale ("Fragment Scale", Range(2, 40)) = 14
        _FragmentEdgeWidth ("Fragment Edge Width", Range(0.01, 0.2)) = 0.035
        _RimWidth ("Rim Width", Range(0.5, 8)) = 1.2
        _EdgeNoiseAmount ("Edge Noise Amount", Range(0, 1)) = 0.45
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 0.75
        _OuterGlowWidth ("Outer Glow Width", Range(0.5, 10)) = 1.5
        _GeometryExpansion ("Geometry Expansion", Range(1, 1.4)) = 1.14
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1.4
        _Opacity ("Opacity", Range(0, 1)) = 0.64
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
            float4 _CoreColor;
            float4 _GrowthColor;
            float4 _EdgeColor;
            float _GrowthProgress;
            float _FragmentScale;
            float _FragmentEdgeWidth;
            float _RimWidth;
            float _EdgeNoiseAmount;
            float _FlowSpeed;
            float _OuterGlowWidth;
            float _GeometryExpansion;
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
                float time = _Time.y * _FlowSpeed;

                float coarse = ValueNoise(i.uv * _FragmentScale + float2(time * 0.13, -time * 0.28));
                float fine = ValueNoise(i.uv * (_FragmentScale * 2.15) + float2(-time * 0.21, -time * 0.47));
                float cells = saturate(coarse * 0.7 + fine * 0.3);
                float threshold = 0.5 + sin(time * 1.4 + coarse * 5.0) * 0.025;
                float cellEdge = 1.0 - smoothstep(
                    _FragmentEdgeWidth,
                    _FragmentEdgeWidth * 2.15,
                    abs(cells - threshold));

                float innerAlpha = ErodeAlpha(i.uv, _RimWidth);
                float spriteRim = saturate(alpha - innerAlpha);
                float dilated = DilateAlpha(i.uv, _OuterGlowWidth);
                float outerBand = saturate(dilated - alpha);

                float edgeVariation = saturate(
                    1.0 - _EdgeNoiseAmount
                    + coarse * _EdgeNoiseAmount * 0.8
                    + cellEdge * _EdgeNoiseAmount * 0.65);
                float rim = spriteRim * edgeVariation;
                float fragments = spriteRim * cellEdge * saturate(0.55 + fine * 0.65);
                float outer = outerBand * saturate(edgeVariation * 0.55 + cellEdge * 0.5);

                float startFade = smoothstep(0.0, 0.08, _GrowthProgress);
                float endFade = 1.0 - smoothstep(0.82, 1.0, _GrowthProgress);
                float life = startFade * endFade;
                float pulse = 0.82 + sin(time * 2.4 + coarse * 4.0) * 0.18;
                float hotCore = saturate(fragments * 1.2 + rim * 0.35);
                float outputAlpha = saturate(fragments + rim * 0.78 + outer * 0.72) * life * pulse * _Opacity;
                clip(outputAlpha - 0.002);

                float3 color = lerp(_EdgeColor.rgb, _GrowthColor.rgb, saturate(rim + fragments));
                color = lerp(color, _CoreColor.rgb, hotCore);
                color *= 1.0 + _EmissionIntensity * saturate(hotCore + rim * 0.5 + outer * 0.35);
                return half4(color, outputAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
