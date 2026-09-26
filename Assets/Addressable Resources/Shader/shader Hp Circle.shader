Shader "Custom/shader Hp Circle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _CircleTex("Main Texture", 2D) = "white" {}
        _Height ("Hp", Float) = 1
        _Speed ("Speed", Float) = 1
        _Scale ("Scale", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "LightMode" = "UniversalForward"
            "Queue" = "Transparent"     
            "RenderType" = "Transparent"
        }


        Pass
        {

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "gradientnoise2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CircleTex);
            SAMPLER(sampler_CircleTex);
            float4 _Color;
            float _Height;
            float _Speed;
            float _Scale;

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
                float4 col = _Color * SAMPLE_TEXTURE2D(_CircleTex, sampler_CircleTex, o.uv);

                float4 col1 = col;
                col1.rgb *= 0.7;
                float noise1 = GradientNoise2D((o.uv - float2(_Speed * _Time.x, 0)) * _Scale);
                noise1 = sin(noise1 * 3.14) * 0.02f;
                col1 *= step(o.uv.y, _Height + noise1 + 0.02f);

                float4 col2 = col;
                float noise2 = GradientNoise2D((o.uv + float2(_Speed * _Time.x, 0)) * _Scale);
                noise2 = sin(noise2 * 3.14) * 0.02f;
                col2 *= step(o.uv.y, _Height + noise2);
                
                return saturate(col1 + col2);
            }
            ENDHLSL
        }
    }
}
