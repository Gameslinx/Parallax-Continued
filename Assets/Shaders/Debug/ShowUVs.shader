Shader "Custom/ShowUVs"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
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
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldBinormal: BINORMAL;
                float3 worldTangent : TANGENT;
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float4 _MainTex_ST;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.worldNormal = mul(unity_ObjectToWorld, v.normal);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, (cross(v.normal, v.tangent.xyz)) * v.tangent.w).xyz);
                o.worldTangent = mul(unity_ObjectToWorld, v.tangent.xyz);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                return float4(i.uv, 0, 1);
            }
            ENDCG
        }
    }
}