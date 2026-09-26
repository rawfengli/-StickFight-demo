#pragma once    
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float4 EncodeHDR(float4 color)
{
#if _USE_RGBM
    float4 outColor = EncodeRGBM(color.rgb);
#else
    float4 outColor = float4(color.rgb, 1.0);
#endif

#if UNITY_COLORSPACE_GAMMA
    return float4(sqrt(outColor.rgb), outColor.w); // linear to γ
#else
    return outColor;
#endif
}



float4 DecodeHDR(half4 color)
{
#if UNITY_COLORSPACE_GAMMA
    color.xyz *= color.xyz; // γ to linear
#endif

#if _USE_RGBM
    return DecodeRGBM(color);
#else
    return float4(color.xyz, 1.0);
#endif
}