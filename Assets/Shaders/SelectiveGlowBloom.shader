Shader "Hidden/WaterAndFire/SelectiveGlowBloom"
{
    Properties
    {
        _Threshold ("Threshold", Float) = 0
        _Intensity ("Intensity", Float) = 1
        _BlurRadius ("Blur Radius", Float) = 1
        _CompositeIntensity ("Composite Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _Threshold;
        float _Intensity;
        float _BlurRadius;
        float _CompositeIntensity;

        float4 Prefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0);
            float brightness = max(max(color.r, color.g), color.b);
            float contribution = saturate((brightness - _Threshold) / max(0.0001, 1.0 - _Threshold));
            return float4(color.rgb * contribution * _Intensity, color.a * contribution);
        }

        float4 Blur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 offset = _BlitTexture_TexelSize.xy * _BlurRadius;
            float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0) * 0.227027;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(offset.x, 0), 0) * 0.1945946;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord - float2(offset.x, 0), 0) * 0.1945946;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0, offset.y), 0) * 0.1945946;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord - float2(0, offset.y), 0) * 0.1945946;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + offset, 0) * 0.0478125;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord - offset, 0) * 0.0478125;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(offset.x, -offset.y), 0) * 0.0478125;
            color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-offset.x, offset.y), 0) * 0.0478125;
            return color;
        }

        float4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 glow = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0);
            return float4(glow.rgb * _CompositeIntensity, 0);
        }
        ENDHLSL

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Prefilter
            ENDHLSL
        }

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
