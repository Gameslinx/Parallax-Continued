Shader "Custom/ShowNormals"
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.worldNormal = mul(unity_ObjectToWorld, v.normal);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, (cross(v.normal, v.tangent.xyz)) * v.tangent.w).xyz);
                o.worldTangent = mul(unity_ObjectToWorld, v.tangent.xyz);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float3 normal = UnpackNormal(tex2D(_BumpMap, i.uv.xy * 40 * float2(2, 1)));
                normal.xy *= 1;
                normal.z = sqrt(max(0, 1 - normal.x * normal.x - normal.y * normal.y));

                float3x3 TBN = float3x3(i.worldTangent, i.worldBinormal, i.worldNormal);
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normal);

                //return float4(worldNormal, 1);
                return dot(worldNormal, _WorldSpaceLightPos0);
                return float4(worldNormal, 1);
            }
            ENDCG
        }
    }
}