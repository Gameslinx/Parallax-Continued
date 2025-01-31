Shader "Custom/RaymarchedShadows"
{
    Properties
    {
        [Header(Tessellation Settings)]
        [Space(10)]
        _MaxTessellation("Max Tessellation", Range(1, 64)) = 1
        _TessellationEdgeLength("Tessellation Edge Length (Pixels)", Range(0.01, 100)) = 1
        _MaxTessellationRange("Max Tessellation Range", Range(1, 100)) = 5

        [Space(10)]
        [Header(Planet Textures)]
        [Space(10)]
        _HeightMap("Planet Height Map", 2D) = "black" {}
        _DepthTex("Depth Tex", 2D) = "white" {}

        _PlanetRadius("Planet Radius", Range(0, 100000)) = 0
        _WorldPlanetRadius("World Planet Radius", Range(0, 2000)) = 0
        _PlanetOrigin("Planet Origin", VECTOR) = (0,0,0)

        // Saves a multicompile
        _DisableDisplacement("Disable Displacement", int) = 0
        _LightWidth("Light Width", Range(0.0001, 0.3)) = 0.05
        //_ScaledPlanetOpacity("Scaled planet opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite On
        ZTest On
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #define STEP_COUNT 48
            #define SHADOW_BIAS 0.001
            #define SHADOW_NORMAL_BIAS 0.01

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            // Subset of the parallax variables
            float _WorldPlanetRadius;
            float _MinRadialAltitude;
            float _MaxRadialAltitude;
        
            // Mesh to real planet scale factor
            float _ScaleFactor;
            float _LightWidth;
            float _ScaledPlanetOpacity;

            // Parallax includes
            #include "../../Includes/ParallaxGlobalFunctions.cginc" 
            #include "../ParallaxScaledStructs.cginc"
            #include "../../Terrain/ParallaxVariables.cginc"
            #include "../../Terrain/ParallaxUtils.cginc"
            #include "../ScaledDisplacementUtils.cginc"
        
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
                float4 screenPos : TEXCOORD2;
                SHADOW_COORDS(3)
            };
        
            sampler2D _HeightMap;
            bool _DisableDisplacement;
        
            v2f vert (appdata v)
            {
                v2f o;
        
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
        
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz, 0)).xyz);
        
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
        
                float displacement = tex2Dlod(_HeightMap, float4(v.uv, 0, 0)).r;
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement);
        
                o.pos = UnityWorldToClipPos(o.worldPos);
                o.screenPos = ComputeScreenPos(o.pos);

                o.uv = v.uv;
                TRANSFER_SHADOW(o)
                return o;
            }
        
            struct PS_Output
            {
                float4 shadowAttenuation : SV_TARGET0;
                float4 shadowDistance : SV_TARGET1;
                float depth : DEPTH;
            };
        
            float GetTerrainHeightAt(float3 pos, float3 origin)
            {
                // Also transform by the planet rotation
                float3 dirFromCenter = normalize(pos - origin);
                float2 uv = DirectionToEquirectangularUV(dirFromCenter);
                float heightValue = tex2Dlod(_HeightMap, float4(uv, 0, 0)).r;
        
                return lerp(_MinRadialAltitude, _MaxRadialAltitude, heightValue);
            }
        
            float GetAltitudeAt(float3 pos, float3 origin)
            {
                float altitude = length(pos - origin) / _ScaleFactor;
                float altAboveRadius = altitude - (_WorldPlanetRadius / _ScaleFactor);

                return altAboveRadius * _ScaleFactor;
            }

            // Shadow umbra and penumbra calculation reference: https://iquilezles.org/articles/rmshadows/
            PS_Output frag (v2f i)
            {
                i.worldNormal = normalize(i.worldNormal);

                // Planet origin
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                // Initial height position, add a small amount to prevent immediate self shadow
                // Set it to 1/100 of the total altitude range
                float initialHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, tex2Dlod(_HeightMap, float4(i.uv, 0, 0)) + SHADOW_BIAS);

                // Setup ray params
                // Shadow bias
                float3 initialRayPos = origin + normalize(i.worldPos - origin) * _WorldPlanetRadius + normalize(i.worldPos - origin) * initialHeight;

                // Shadow normal bias
                float3 dhdx = ddx(i.worldPos);
                float3 dhdy = ddy(i.worldPos);
                float3 dhdz = -normalize(cross(dhdx, dhdy)) * (_MaxRadialAltitude - _MinRadialAltitude) * SHADOW_NORMAL_BIAS;

                float3 rayPos = initialRayPos + dhdz;
                float3 rayDir = normalize(_WorldSpaceLightPos0);

                // TODO: Precompute the ray distance
                float rayDistance = _WorldPlanetRadius * 0.2f;
                float stepSize = rayDistance / float(STEP_COUNT);

                float attenuation = 1.0f;
                float t = 0;

                // Raymarch towards the light
                for (int b = 0; b < 48; b++)
                {
                    rayPos += rayDir * stepSize;

                    // Get ray altitude and terrain height
                    float worldTerrainAltitude = GetTerrainHeightAt(rayPos, origin);
                    float worldRayAltitude = GetAltitudeAt(rayPos, origin);

                    // Compute height difference between ray and terrain
                    float h = (worldRayAltitude - worldTerrainAltitude);
                    attenuation = min(attenuation, h / (_LightWidth * t));
                    t += stepSize;

                    // Stop when shadowed
                    if (attenuation < -1.0f)
                    {
                        break;
                    }
                }

                // Snap to -1 contribution and use a smoothstep function
                attenuation = max(attenuation, -1.0f);
                attenuation = 0.25f * (1.0f + attenuation) * (1.0f + attenuation) * (2.0f - attenuation);

                PS_Output output;

                output.shadowAttenuation = attenuation;
                float shadowBlurMaxDistance = 0.05f;
                output.shadowDistance = saturate(distance(rayPos, initialRayPos) / shadowBlurMaxDistance);
                output.depth = i.pos.z + 0.001f;

                output.shadowAttenuation = saturate(output.shadowAttenuation + (1 - _ScaledPlanetOpacity));

                return output;
            }
            ENDCG
        }

        // Depth pass
        Pass
        {
            Tags {"LightMode"="ShadowCaster"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            // Subset of the parallax variables
            float _WorldPlanetRadius;
            float _MinRadialAltitude;
            float _MaxRadialAltitude;

            // Parallax includes
            #include "../../Includes/ParallaxGlobalFunctions.cginc" 
            #include "../ParallaxScaledStructs.cginc"
            #include "../../Terrain/ParallaxVariables.cginc"
            #include "../../Terrain/ParallaxUtils.cginc"
            #include "../ScaledDisplacementUtils.cginc"
        
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
                float4 screenPos : TEXCOORD2;
            };
        
            sampler2D _HeightMap;
            bool _DisableDisplacement;
        
            v2f vert (appdata v)
            {
                v2f o;
        
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
        
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz, 0)).xyz);
        
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
        
                float displacement = tex2Dlod(_HeightMap, float4(v.uv, 0, 0)).r;
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement);
        
                o.pos = UnityWorldToClipPos(o.worldPos);
                o.screenPos = ComputeScreenPos(o.pos);

                o.uv = v.uv;
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
