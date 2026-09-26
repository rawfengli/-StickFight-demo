Shader "Custom/shader_ring"
{
    Properties
    {
        //Shader
        _RingSpacing ("Ring Spacing", Float) = 0.1
        _TargetRadius ("Target Radius", Float) = 0.4
        _FadeStartRadius ("Fade Start Radius", Float) = 0.3
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
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _RingSpacing;
            float _TargetRadius;
            float _FadeStartRadius;
            //C#
            float _TotalCD;
            float _NowCD;
            float _FadeTime;

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

            float4 InRingPass1(float2 uv)
            {
                float t = _NowCD / _TotalCD;
                float2 center = float2(0.5, 0.5);
                float dis = length(float2(uv - center));
                float dis0 = lerp(0.5f, _TargetRadius, t);
                float dis1 = dis0 - _RingSpacing;

                float4 color;

                if(dis1 <= dis && dis <= dis0)
                    color = float4(1, 1, 1, t * 1.2f);
                else
                    color = float4(1, 1, 1, 0);
                return color;
            }
            float4 InRingPass2(float2 uv)
            {
                float dt = _NowCD - _TotalCD;
                float t = dt / _FadeTime;
                float2 center = float2(0.5, 0.5);

                float dis = length(float2(uv - center));
                float dis0 = lerp(_TargetRadius, 0.5f, t);
                float dis1 = lerp(_FadeStartRadius, 0.5f, t);

                float4 color;
                if(dis1 <= dis && dis <= dis0)
                    color = float4(1, 1, 1, 1 - t);
                else
                    color = float4(1, 1, 1, 0);

                return color;
            }

            Output vert(Input i)
            {
                Output o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.uv = i.uv;
                return o;
            }
            float4 frag(Output o) : SV_Target
            {
                float4 col;
                if(_NowCD < _TotalCD)
                    col = InRingPass1(o.uv);
                else
                    col = InRingPass2(o.uv);
                
                return col;
            }
            ENDHLSL
        }
    }
}
