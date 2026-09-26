Shader "Custom/shader common particle"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white"{}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }


        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        pass
        {


            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Output vert (Input input)
            {
                Output output;
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;

                return output;
            }

            float4 frag (Output output) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, output.uv);
                color = output.color * color;
                return color;
            }

            ENDHLSL
        }
    }
}
