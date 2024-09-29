Shader "Custom/TriplanarRotationTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tiling("Tiling", Range(0.001, 10)) = 1
        _PlanetOrigin("PlanetOrigin", vector) = (0,0,0)
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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD1;
                float3 planetDir : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Tiling;
            float3 _PlanetOrigin;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                o.planetDir = o.worldPos - _PlanetOrigin;
                return o;
            }

            float3 GetTriplanarWeights(float3 worldNormal)
            {
	            float3 triW = abs(worldNormal);
	            return triW / (triW.x + triW.y + triW.z);
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                i.worldNormal = normalize(i.worldNormal);
                i.planetDir = normalize(i.planetDir);

                float3 weight = GetTriplanarWeights(i.worldNormal);

                float2 zy = i.worldPos.zy * _Tiling;
                float2 xz = i.worldPos.xz * _Tiling;
                float2 xy = i.worldPos.xy * _Tiling;

                float3 normal = (0, 0, 1);
                float3 tangent = (0, 1, 0);
                float3 binormal = (1, 0, 0);

                float2 uv = float2(dot())

                float cosAngleZY = dot(i.planetDir.zy, float3(0, 1, 0));
                return cosAngleZY;

                float4 zyTex = tex2D(_MainTex, zy);
                float4 xzTex = tex2D(_MainTex, xz);
                float4 xyTex = tex2D(_MainTex, xy);

                float4 result = zyTex * weight.x + xzTex * weight.y + xyTex * weight.z;

                return result;
            }
            ENDCG
        }
    }
}