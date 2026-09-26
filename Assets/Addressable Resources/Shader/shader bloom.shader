Shader "Custom/preGraphicsFormat."
{
    Properties{}
    SubShader
    {

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "prefilter.hlsl"

            
            float _Threshold;
            float _SoftKnee;

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = FragPrefilter(input.texcoord,  _BlitTexture, sampler_LinearClamp, _Threshold, _SoftKnee);
                return color;
            }

            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "gaussianblur.hlsl"
            
            float4 _BlitTexture_TexelSize;

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = GaussianBlurHorizontal9SampleHDR(
                    input.texcoord, 
                    _BlitTexture_TexelSize.xy, 
                    _BlitTexture, 
                    sampler_LinearClamp);

                return color;
            }

            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "gaussianblur.hlsl"
            
            float4 _BlitTexture_TexelSize;

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = GaussianBlurVerticalHDR(
                    input.texcoord, 
                    _BlitTexture_TexelSize.xy, 
                    _BlitTexture, 
                    sampler_LinearClamp);

                return color;
            }

            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "HDR.hlsl"
            
            TEXTURE2D(_LowTexture);
            SAMPLER(sampler_LowTexture);

            float _Scatter;

            float3 UpSample(float2 uv)
            {
                float3 highMipColor = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv));
                float3 lowMipColor  = DecodeHDR(SAMPLE_TEXTURE2D(_LowTexture, sampler_LowTexture, uv));
                
                return lerp(highMipColor, lowMipColor, _Scatter);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 color = UpSample(input.texcoord);
                return float4(color, 1.0);
            }

            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE2D(_BloomTexture);
            SAMPLER(sampler_BloomTexture);
            float4 _BloomColor;
            float _Intensity;

            float4 frag(Varyings input) : SV_Target
            {
                float3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                float4 bloom = SAMPLE_TEXTURE2D(_BloomTexture, sampler_BloomTexture, input.texcoord);
            
#if UNITY_COLORSPACE_GAMMA
                bloom.xyz *= bloom.xyz; // γ to linear
#endif
                color += bloom.xyz * _BloomColor.rgb * _Intensity;
                return float4(color, 1.0);
            }

            ENDHLSL

        }
    }
}
