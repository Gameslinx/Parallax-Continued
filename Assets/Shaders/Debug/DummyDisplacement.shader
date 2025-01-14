Shader "Unlit/DummyDisplacement"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _BumpScale("Bump Scale", Range(0, 4)) = 1

        _Displacement("Displacement", Range(0, 2)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAl;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 worldTangent : TANGENT;
                float3 worldBinormal : BINORMAL;
                SHADOW_COORDS(2)
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float4 _MainTex_ST;
            float _Displacement;
            float _BumpScale;

            // Light shadow casters

            float2 DirectionToEquirectangularUV(float3 direction)
            {
                // Normalize the direction to ensure it's a unit vector
                direction = normalize(direction);
            
                // Compute the azimuthal angle (longitude) in the range [-PI, PI]
                float phi = atan2(direction.z, direction.x);
            
                // Compute the polar angle (latitude) in the range [0, PI]
                float theta = acos(direction.y);
            
                // Map phi and theta to UV coordinates
                float u = (phi / (2.0 * 3.14159265359)) + 0.5; // [0, 1] range
                float v = theta / 3.14159265359; // [0, 1] range
            
                return float2(u + 0.75, 1 - v);
            }

            v2f vert (appdata v)
            {
                v2f o;

                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz, 0)).xyz);
                float3 worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                float3 worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal.xyz, v.tangent.xyz), 0)).xyz) * v.tangent.w;

                float heightValue = tex2Dlod(_MainTex, float4(DirectionToEquirectangularUV(normalize(worldPos - origin)), 0, 0));

                float3 displacedWorldPos = worldPos + worldNormal * heightValue * _Displacement;
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
                o.worldTangent = worldTangent;
                o.worldBinormal = worldBinormal;

                o.pos = UnityWorldToClipPos(displacedWorldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);

                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float2 sphericalUV = DirectionToEquirectangularUV(normalize(i.worldPos - origin));

                fixed4 col = tex2Dlod(_MainTex, float4(sphericalUV, 0, 0));

                // Tangent space normal mapping
                float3x3 TBN = transpose(float3x3(i.worldTangent, i.worldBinormal, i.worldNormal));
                float3 normal = UnpackScaleNormal(tex2D(_BumpMap, sphericalUV), _BumpScale);
                i.worldNormal = normalize(mul(TBN, normal));

                // Lighting
                float NdotL = dot(i.worldNormal, _WorldSpaceLightPos0);
                NdotL = saturate(NdotL);

                float shadowAttenuation = SHADOW_ATTENUATION(i);

                return shadowAttenuation * 1;
                float4 diffuse = col * NdotL * _LightColor0 * shadowAttenuation;
                return diffuse + UNITY_LIGHTMODEL_AMBIENT;
            }
            ENDCG
        }
        Pass
        {
            Tags {"LightMode"="ShadowCaster"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            #include "DebugShadowFuncs.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Displacement;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAl;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 worldTangent : TANGENT;
                float3 worldBinormal : BINORMAL;
            };

            float2 DirectionToEquirectangularUV(float3 direction)
            {
                // Normalize the direction to ensure it's a unit vector
                direction = normalize(direction);
            
                // Compute the azimuthal angle (longitude) in the range [-PI, PI]
                float phi = atan2(direction.z, direction.x);
            
                // Compute the polar angle (latitude) in the range [0, PI]
                float theta = acos(direction.y);
            
                // Map phi and theta to UV coordinates
                float u = (phi / (2.0 * 3.14159265359)) + 0.5; // [0, 1] range
                float v = theta / 3.14159265359; // [0, 1] range
            
                return float2(u + 0.75, 1 - v);
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz, 0)).xyz);
                float3 worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                float3 worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal.xyz, v.tangent.xyz), 0)).xyz) * v.tangent.w;

                float heightValue = tex2Dlod(_MainTex, float4(DirectionToEquirectangularUV(normalize(worldPos - origin)), 0, 0));

                float3 displacedWorldPos = worldPos + worldNormal * heightValue * _Displacement;
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
                o.worldTangent = worldTangent;
                o.worldBinormal = worldBinormal;

                o.pos = UnityWorldToClipPos(displacedWorldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                //v.vertex.xyz = mul(unity_WorldToObject, float4(displacedWorldPos, 1)).xyz;
                //v.vertex

                o.pos = ParallaxClipSpaceShadowCasterPos(displacedWorldPos, worldNormal);
                o.pos = UnityApplyLinearShadowBias(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
