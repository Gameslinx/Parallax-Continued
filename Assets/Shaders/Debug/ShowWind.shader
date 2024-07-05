Shader "Unlit/ShowWind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WindMap ("Wind Map", 2D) = "grey" {}
        _PlanetOrigin("Planet Origin", vector) = (0,0,0) 
        _WindSpeed("Wind Speed", vector) = (0,0,0) 
        _WaveAmp("Wave Amp", Range(0, 10)) = 10
        _HeightCutoff("Height Cutoff", Range(0, 1)) = 1
        _HeightFactor("Height Factor", Range(0, 10)) = 1
        _WaveSpeed("Wave Speed", Range(0, 10)) = 1
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
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
            };

            sampler2D _MainTex;
            sampler2D _WindMap;
            float4 _MainTex_ST;
            float3 _PlanetOrigin;

            float3 _WindSpeed;
            float _WaveAmp;
            float _HeightCutoff;
            float _HeightFactor;
            float _WaveSpeed;
            
            float3 Wind(float3 worldPos, float localVertexHeight)
            {
                float3 bf = abs(normalize(worldPos - _PlanetOrigin));
                bf /= dot(bf, (float3) 1);
                float2 xz = worldPos.zx + frac(_Time.x * 10);
                float2 xy = worldPos.xy + frac(_Time.x * 10);
                float2 zy = worldPos.yz + frac(_Time.x * 10);

                float3 texXZ = tex2Dlod(_WindMap, float4(xz, 0, 0)).rgb * bf.y;
                float3 texXY = tex2Dlod(_WindMap, float4(xy, 0, 0)).rgb * bf.z;
                float3 texZY = tex2Dlod(_WindMap, float4(zy, 0, 0)).rgb * bf.x;

                return texXZ + texXY + texZY;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float3 worldNormal = normalize(i.worldNormal);

                float3 windCol = Wind(i.worldPos, worldNormal);
                //return float4(worldNormal, 1);
                return float4(windCol, 1);
            }
            ENDCG
        }
    }
}