Shader "Unlit/DistanceTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaxDist("Max dist", Range(1.0001, 100)) = 1
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
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _MaxDist;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float dist = distance(i.worldPos, _WorldSpaceCameraPos);
                float logDist = log2(pow(dist, 2.0f));
                float maxDist = log2(pow(_MaxDist, 2.0f));

                int level = lerp(1, 7, logDist / maxDist);

                int roundedDist = logDist;



                return (float)level / 7;
            }
            ENDCG
        }
    }
}