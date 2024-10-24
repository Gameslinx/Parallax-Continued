﻿Shader "Unlit/BlendTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainTex2("Texture 2", 2D) = "white" {}
        _DisplacementTex1("Displacement 1", 2D) = "white" {}
        _DisplacementTex2("Displacement 2", 2D) = "white" {}
        _BlendStart("Blend start", Range(0, 10)) = 1
        _BlendEnd("Blend end", Range(0, 10)) = 2

        _Smoothness("Smoothness", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _MainTex2;
            sampler2D _DisplacementTex1;
            sampler2D _DisplacementTex2;
            float _BlendStart;
            float _BlendEnd;
            float _BlendPower;
            float _Smoothness;
            float4 _MainTex_ST;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;

                return o;
            }
            
            float GetPercentageBetween(float lower, float upper, float x)
            {
                return saturate((x - lower) / (upper - lower));
            }

            float GetDisplacementLerpFactor(float heightLerp, float displacement1, float displacement2)
            {
                displacement2 += (heightLerp);
                displacement1 *= (1 - heightLerp);

                displacement2 = saturate(displacement2);
                displacement1 = saturate(displacement1);

                float diff = (displacement2 - displacement1) * heightLerp;

                diff /= _Smoothness;
                diff = saturate(diff);

                return diff;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the base textures
                float4 tex1 = tex2D(_MainTex, i.uv);
                float4 tex2 = tex2D(_MainTex2, i.uv);

                // Sample the displacement maps
                float displacement1 = tex2D(_DisplacementTex1, i.uv).r;
                float displacement2 = tex2D(_DisplacementTex1, i.uv).b;

                // Compute the height-based blend factor
                float heightLerp = GetPercentageBetween(_BlendStart, _BlendEnd, length(i.worldPos));

                float diff = GetDisplacementLerpFactor(heightLerp, displacement1, displacement2);

                float4 result = lerp(tex1, tex2, diff);

                return result;

            }
            ENDCG
        }
    }
}