#ifndef GAUSSIAN_BLUR_INCLUDED
#define EDGE_DETECTION_COLORINCLUDED

static float GaussianWeights[5] = {0.070270, 0.316216, 0.227027, 0.316216, 0.070270};

float4 GaussianBlurHorizontal(float2 uv, float blurSize, float2 texelSize, Texture2D tex, SamplerState samplerTex)
{
    float2 offset = float2(texelSize.x * blurSize, 0);
    float4 color = 0;

    [unroll]
    for(int i = -2; i <= 2; i++)
    {
        color += SAMPLE_TEXTURE2D(tex, samplerTex, uv + offset * i) * GaussianWeights[i + 2];
    }
    return color;
}

float4 GaussianBlurVertical(float2 uv, float blurSize, float2 texelSize, Texture2D tex, SamplerState samplerTex)
{
    float2 offset = float2(0, texelSize.y * blurSize);
    float4 color = 0;
    
    [unroll]
    for(int i = -2; i <= 2; i++)
    {
        color += SAMPLE_TEXTURE2D(tex, samplerTex, uv + offset * i) * GaussianWeights[i + 2];
    }
    return color;
}
#endif 