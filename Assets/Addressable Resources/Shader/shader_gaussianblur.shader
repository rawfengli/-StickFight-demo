Shader "Custom/shader_gaussianblur"
{
    Properties
    {
        _MainTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"     
            "RenderType" = "Transparent"
        }

        Pass
        {
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "gaussianblur.hlsl"

            float4 _BlitTexture_TexelSize;

            float4 frag(Varyings input) : SV_TARGET
            {
                float value = GaussianBlurHorizontal(input.texcoord, _BlitTexture_TexelSize.xy, _BlitTexture, sampler_LinearClamp);
                float4 color = float4(value, value, value, value);

                return color;
            }
            ENDHLSL
        }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "gaussianblur.hlsl"

            float4 _BlitTexture_TexelSize;

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = GaussianBlurVertical(input.texcoord, _BlitTexture_TexelSize.xy, _BlitTexture, sampler_LinearClamp);
                return color;
            }
            ENDHLSL
        }
    }
}
