#ifndef EDGE_DETECTION_ALPHA_INCLUDED
#define EDGE_DETECTION_ALPHA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

static float kernelX[3][3] = {
    {-1, 0, 1},
    {-2, 0, 2},
    {-1, 0, 1},
};
static float kernelY[3][3] = {
    {-1, -2, -1},
    { 0,  0,  0},
    { 1,  2,  1},
};
float EdgeDetection(float threshold, float2 uv, float2 texelSize, Texture2D tex, SamplerState samplerTex)
{
    float3 gx = 0;
    float3 gy = 0;
    float totalWeight = 0;
    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            float2 offsetUV = uv + float2(x, y) * texelSize;
            offsetUV = clamp(offsetUV, 0.0, 1.0);            

            float4 _color = SAMPLE_TEXTURE2D(tex, samplerTex, offsetUV);
            float weight = _color.a;
            
            gx += kernelX[x + 1][y + 1] * weight;
            gy += kernelY[x + 1][y + 1] * weight;
            totalWeight += weight;
        }
    }
    float edgeStrength = length(gx) + length(gy);

    if (totalWeight > 0.01) 
        edgeStrength = edgeStrength / (totalWeight / 9);

    edgeStrength = edgeStrength / 4.0;
    

    float edge = max(0.0, edgeStrength - threshold);
    edge = edge / (1.0 - threshold); // 保持对比度

    return saturate(edge);
}

#endif 