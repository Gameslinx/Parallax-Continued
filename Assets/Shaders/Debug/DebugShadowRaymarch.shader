Shader "Unlit/DebugShadowRaymarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}

        _Displacement("Displacement", Range(0, 2)) = 0.1
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
                float3 normal : NORMAl;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 worldTangent : TANGENT;
                float3 worldBinormal : BINORMAL;
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            sampler2D _CameraDepthTexture;
            float4 _MainTex_ST;
            float _Displacement;

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

                o.vertex = UnityWorldToClipPos(displacedWorldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            struct PS_Output
            {
                float4 shadowAttenuation : SV_TARGET0;
                float4 shadowDistance : SV_TARGET1;
            };

            PS_Output frag (v2f i) : SV_Target
            {

                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);

                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float2 sphericalUV = DirectionToEquirectangularUV(normalize(i.worldPos - origin));

                fixed4 col = tex2Dlod(_MainTex, float4(sphericalUV, 0, 0));

                float initialHeight = tex2Dlod(_MainTex, float4(sphericalUV, 0, 0)).r;

                // Shadow experiments

                float shadowAttenuation = 1;

                int stepCount = 128;
                float worldMeshRadius = 0.1 + _Displacement * 1;

                float ETA = 0.001f;

                // Start slightly above to prevent self shadow
                // Start at terrain surface
                float3 initialRayPos = origin + normalize(i.worldNormal)*0.5 + i.worldNormal * initialHeight * (_Displacement + 0.005f);
                float3 rayPos = initialRayPos;

                float3 rayDir = normalize(_WorldSpaceLightPos0);
                float stepSize = (float)worldMeshRadius / (float)stepCount;

                float worldMinRadius = 0.5;
                float worldMaxRadius = 0.5 + _Displacement;

                for (int b = 0; b < stepCount; b++)
                {
                    
                    float3 dirFromCenter = normalize(rayPos - origin);
                    float2 uv = DirectionToEquirectangularUV(dirFromCenter);

                    float heightValueAtRay = tex2Dlod(_MainTex, float4(uv, 0, 0)).r;

                    float worldTerrainAltitude = lerp(worldMinRadius, worldMaxRadius, heightValueAtRay);
                    float worldRayAltitude = saturate(length(rayPos - origin));

                    if (worldTerrainAltitude > worldRayAltitude)
                    {
                        shadowAttenuation = 0;
                        break;
                    }

                    // Advance ray
                    rayPos += rayDir * stepSize;
                }

                // Tangent space normal mapping
                float3x3 TBN = transpose(float3x3(i.worldTangent, i.worldBinormal, i.worldNormal));
                float3 normal = UnpackNormal(tex2D(_BumpMap, sphericalUV));
                i.worldNormal = normalize(mul(TBN, normal));

                // Lighting
                float NdotL = dot(i.worldNormal, _WorldSpaceLightPos0);
                NdotL = saturate(NdotL);

                PS_Output output;

                // Max possible distance a ray could travel (planet radius world space)

                output.shadowAttenuation = shadowAttenuation;
                output.shadowDistance = (distance(rayPos, initialRayPos));

                return output;
            }
            ENDCG
        }
    }
}
