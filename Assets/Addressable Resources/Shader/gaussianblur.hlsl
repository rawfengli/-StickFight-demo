#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HDR.hlsl"

static const float weight9Sample[9] = {0.0162, 0.0540, 0.1216, 0.1945, 0.2270, 0.1945, 0.1216, 0.0540, 0.0162};
static const float weight[5] = {0.0545, 0.2442, 0.4026, 0.2442, 0.0545};

float4 GaussianBlurHorizontal(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 5; i++)
    {
        float4 col = SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(stepSize.x, 0) * (i - 2));
        res += col * weight[i];
    }
    return res;
}
float4 GaussianBlurVertical(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 5; i++)
    {
        float4 col = SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(0, stepSize.y) * (i - 2));
        res += col * weight[i];
    }
    return res;
}

//------------------------------------------------------------------------------------------------------
float4 GaussianBlurHorizontalHDR(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 5; i++)
    {
        float4 col = DecodeHDR(SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(stepSize.x, 0) * (i - 2)));
        res += col * weight[i];
    }
    return EncodeHDR(res);

}
float4 GaussianBlurVerticalHDR(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 5; i++)
    {
        float4 col = DecodeHDR(SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(0, stepSize.y) * (i - 2)));
        res += col * weight[i];
    }
    return EncodeHDR(res);
}
//------------------------------------------------------------------------------------------------------


float4 GaussianBlurHorizontal9Sample(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 9; i++)
    {
        float4 col = SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(stepSize.x, 0) * (i - 4));
        res += col * weight9Sample[i];
    }
    return res;
}
float4 GaussianBlurVertical9Sample(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 9; i++)
    {
        float4 col = SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(0, stepSize.y) * (i - 4));
        res += col * weight9Sample[i];
    }
    return res;
}
//------------------------------------------------------------------------------------------------------
float4 GaussianBlurHorizontal9SampleHDR(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 9; i++)
    {
        float4 col = DecodeHDR(SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(stepSize.x, 0) * (i - 4)));
        res += col * weight9Sample[i];
    }
    return EncodeHDR(res);
}
float4 GaussianBlurVertical9SampleHDR(float2 uv, float2 stepSize, Texture2D tex, SamplerState _sampler)
{
    float4 res = 0;
    for(int i = 0; i < 9; i++)
    {
        float4 col = DecodeHDR(SAMPLE_TEXTURE2D(tex, _sampler, uv + float2(0, stepSize.y) * (i - 4)));
        res += col * weight9Sample[i];
    }
    return EncodeHDR(res);
}