Shader "Custom/HDR Default"
{
    Properties
    {
        
        [HDR] _HDRColor ("HDR Tint Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
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
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha


            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _HDRColor;

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
                float4 col = _HDRColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, o.uv);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
