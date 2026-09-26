// edge_detection.hlsl
#ifndef EDGE_DETECTION_INCLUDED
#define EDGE_DETECTION_INCLUDED


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


float4 EdgeDetection(float2 uv, float texelSize, TEXTURE2D_PARAM(tex, sampler_tex))
{
    int count = 0;
    float4 rawCol = SAMPLE_TEXTURE2D(tex, sampler_tex, uv);
    if(rawCol.a > 0.01)
        return rawCol;
    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            float2 offsetUV = uv + float2(x, y) * texelSize;
            
            float4 color = SAMPLE_TEXTURE2D(tex, sampler_tex, offsetUV);
            count += step(0.05, color.a);
        }
    }
    if(count > 0)
        return float4(0, 0, 0, 1);
    else    
        return float4(0, 0, 0, 0);
}

#endif 