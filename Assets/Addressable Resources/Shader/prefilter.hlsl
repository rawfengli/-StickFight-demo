#pragma once    
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HDR.hlsl"

float4 FragPrefilter(float2 uv, Texture2D tex, SamplerState _sampler, float _Threshold, float _SoftKnee)
{
    float4 color = SAMPLE_TEXTURE2D(tex, _sampler, uv);
                
    float brightness = max(color.r, max(color.g, color.b));
    float softness = clamp(brightness - _Threshold + _SoftKnee, 0.0, 2.0 * _SoftKnee);
    softness = softness / (2 * _SoftKnee + 0.001);
    softness = softness * softness;
                
    float multiplier = max(brightness - _Threshold, softness) / max(brightness, 1e-4);

    color *= multiplier;
    color = max(color, 0);
    return EncodeHDR(color);
}