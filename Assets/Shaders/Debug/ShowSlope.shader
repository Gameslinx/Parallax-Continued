Shader "Unlit/ShowSlope"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PlanetNormal("Planet Normal", VECTOR) = (0,0,0)
        _SteepPower("Steep Power", Range(0.001, 10)) = 1
        _SteepContrast("Steep Contrast", Range(0.001, 10)) = 1
        _SteepMidpoint("Steep Midpoint", Range(0.001, 10)) = 1
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
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float3 _PlanetNormal;

            float _SteepPower;
            float _SteepMidpoint;
            float _SteepContrast;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = mul(unity_ObjectToWorld, v.normal);
                return o;
            }
            
            float CalculateSlope(float3 normal)
            {
                // Get gradient of slope
                float slope = abs(dot(normalize(-_PlanetNormal), normal));
                
                slope = pow(slope, _SteepPower);
                slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
                
                return slope;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float slope = CalculateSlope(normalize(i.worldNormal));
                return slope;
            }
            ENDCG
        }
    }
}