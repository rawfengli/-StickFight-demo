Shader "Custom/shader_menu_button"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"     
            "RenderType" = "Transparent"
        }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        /*
        pass//edge_detection
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "edge_detection_alpha.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Threshold;
            float2 _TexelSize;
            float4 _EdgeColor;

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag(Output o) : SV_Target
            {

                float res = EdgeDetection(_Threshold, o.uv, _TexelSize, _MainTex, sampler_MainTex);

                return res * _EdgeColor;
            }

            ENDHLSL
        }
        */
        pass//gaussian_blur_horizontal
        {   
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "gaussian_blur.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag(Output o) : SV_Target
            {
                float4 color = GaussianBlurHorizontal(o.uv, _BlurSize, _MainTex_TexelSize.xy, _MainTex, sampler_MainTex);
                return color;
            }

            ENDHLSL
        }
        pass//gaussian_blur_vertical
        {   
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "gaussian_blur.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag(Output o) : SV_Target
            {
                float4 color = GaussianBlurVertical(o.uv, _BlurSize, _MainTex_TexelSize.xy, _MainTex, sampler_MainTex);
                return color;
            }

            ENDHLSL
        }
        /*
        pass//blit
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag(Output o) : SV_Target
            {

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, o.uv);

                return color;
            }

            ENDHLSL
         }
         */
    }
    FallBack "Diffuse"
}
