Shader "Custom/hourglass"
{
    Properties
    {
        _Vertex10("Vertex1", Vector) = (1, 1, 1, 0)
        _Vertex11("Vertex2", Vector) = (1, 1, 1, 0)
        _Vertex12("Vertex3", Vector) = (1, 1, 1, 0)
        [Space(40)]
        _Vertex20("Vertex1", Vector) = (1, 1, 1, 0)
        _Vertex21("Vertex2", Vector) = (1, 1, 1, 0)
        _Vertex22("Vertex3", Vector) = (1, 1, 1, 0)
        
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

            float4 _Vertex10;
            float4 _Vertex11;
            float4 _Vertex12;

            float _TopLine1;
            float _BottomLine1;

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

            float cross(float2 a, float2 b, float2 c)
            {
                float2 vec0 = b - a;//vec_ab
                float2 vec1 = c - a;//vec_ab
                return vec0.x * vec1.y - vec0.y * vec1.x; 
            }
            float InTriangle(float2 p, float2 v0, float2 v1, float2 v2)
            {
                float res = 0; 
                res += step(cross(p, v0, v1), 0);
                res += step(cross(p, v1, v2), 0);
                res += step(cross(p, v2, v0), 0);
                if(res == 3 || res == 0)
                    return 1;
                else
                    return 0;
            }
            float OutOffRange(float p, float y0, float y1)
            {
                return step((p - y0) * (p - y1), 0);
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
                float v ;

                v = InTriangle(o.uv, _Vertex10.xy, _Vertex11.xy, _Vertex12.xy) * OutOffRange(o.uv.y, _TopLine1, _BottomLine1);

                col = float4(v, v, v, v);
                return col;
            }
            ENDHLSL
        }
        Pass//hourglass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha


            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Vertex20;
            float4 _Vertex21;
            float4 _Vertex22;

            float _TopLine2;
            float _BottomLine2;

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

            float cross(float2 a, float2 b, float2 c)
            {
                float2 vec0 = b - a;//vec_ab
                float2 vec1 = c - a;//vec_ab
                return vec0.x * vec1.y - vec0.y * vec1.x; 
            }
            float InTriangle(float2 p, float2 v0, float2 v1, float2 v2)
            {
                float res = 0; 
                res += step(cross(p, v0, v1), 0);
                res += step(cross(p, v1, v2), 0);
                res += step(cross(p, v2, v0), 0);
                if(res == 3 || res == 0)
                    return 1;
                else
                    return 0;
            }
            float OutOffRange(float p, float y0, float y1)
            {
                return step((p - y0) * (p - y1), 0);
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
                float v ;

                v = InTriangle(o.uv, _Vertex20.xy, _Vertex21.xy, _Vertex22.xy) * OutOffRange(o.uv.y, _TopLine2, _BottomLine2);

                col = float4(v, v, v, v);
                return col;
            }
            ENDHLSL
        }
    }
}
