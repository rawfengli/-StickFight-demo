Shader "Custom/shader bullet fire"
{
    Properties
    {
        
        [HDR] _HDRColor ("HDR Tint Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _Edge("Edge", Float) = 0.1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"     
            "RenderType" = "Transparent"
        }
        Pass//hourglass
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
            float _Edge;

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

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                o.color = i.color;

                return o;
            }
            float4 frag(Output o) : SV_Target
            {
                float4 col = _HDRColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, o.uv);
                col = col * step(_Edge, o.uv.x) * o.color;
                return col;
            }
            ENDHLSL
        }
    }
}
