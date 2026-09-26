Shader "Custom/shader_stickman"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        
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

            float4 _Color;

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
                return _Color;
            }
            ENDHLSL
        }
    }
}
