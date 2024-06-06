
Shader "Custom/Billboard" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "white" {}
        _Color("Color", COLOR) = (0,0,0)
        _Cutoff("_Cutoff", Range(0, 1)) = 0.5
        _Metallic("_Metallic", Range(0.001, 100)) = 1
        _MetallicTint("_MetallicTint", COLOR) = (1,1,1)
        _Gloss("_Gloss", Range(0.001, 200)) = 1
    }
    SubShader
    {
            //Tags{ "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        ZWrite On
        //Cull Off
            Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout"}
            //Tags { "RenderType" = "Opaque"}

        Pass 
        {

            Tags{ "LightMode" = "ForwardBase" }
            //Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "AutoLight.cginc"
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            float _Cutoff;


            float3 _FresnelColor;
            float _FresnelPower;
            sampler2D _MainTex;
            float2 _MainTex_ST;
            float4 _Color;

            sampler2D _BumpMap;

            struct appdata_t 
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f 
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float3 worldNormal : TEXCOORD1;
                float3 world_vertex : TEXCOORD2;

                float3 tangentWorld: TEXCOORD6;
                float3 binormalWorld: TEXCOORD7;

                float3 viewDir : TEXCOORD8;
                float3 lightDir : TEXCOORD9;

                LIGHTING_COORDS(3, 4)
            };
            
            void Billboard(inout float4 vertex, float4x4 mat)
            {
                float3 local = vertex.xyz;
                            
                float3 upVector = float3(0, 1, 0);
                float3 forwardVector = mul(UNITY_MATRIX_IT_MV[2].xyz, mat);
                float3 rightVector = normalize(cross(forwardVector, upVector));
                         
                float3 position = local.x * rightVector + local.y * upVector + local.z * forwardVector;
                 
                float3x3 rotMat = float3x3(rightVector, upVector, forwardVector);

                vertex = float4(position, 1);
                
                // Output local space position
                //vertex = mul(mat, vertex);
            }


            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                Billboard(i.vertex, unity_ObjectToWorld);
                float4 pos = mul(unity_ObjectToWorld, i.vertex);
                
                float3 world_vertex = mul(unity_ObjectToWorld, pos.xyz);

                o.pos = UnityObjectToClipPos(i.vertex);
                o.color = i.color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.world_vertex = world_vertex;

                o.worldNormal = normalize(mul(unity_ObjectToWorld, i.normal));
                o.tangentWorld = normalize(mul(unity_ObjectToWorld, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir =  normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0);

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                
                return o;
            }

            fixed4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                return 1;

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

                clip(col.a - _Cutoff);

                return col;

                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);

                float3 worldNormal = mul(TBN, normalMap);

                return col;
            }

            ENDCG
        }
    }
    Fallback "Cutout"
}