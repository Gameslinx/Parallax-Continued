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
        _BlueNoise("Blue Noise", 2D) = "grey" {}

        //_FrameCount("Frame Count", int) = 0
        _PlanetRadius("Planet Radius", Range(0, 100000)) = 0
        _WorldPlanetRadius("World Planet Radius", Range(0, 2000)) = 0
        _PlanetOrigin("Planet Origin", VECTOR) = (0,0,0)

        // Saves a multicompile
        _DisableDisplacement("Disable Displacement", int) = 0
        _MaxRayDistance("Max Ray Distace", Range(0.0001, 100000)) = 1

        // Comment out when compiling
        //_ScaledPlanetOpacity("Scaled planet opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
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
            #pragma multi_compile_local _           OCEAN OCEAN_FROM_COLORMAP
            #pragma multi_compile_local _           BLUE_NOISE

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
            float _ScaledPlanetOpacity;
            float _OceanAltitude;
            float _MaxRayDistance;

            #define MAX_SHADOW_LIGHTS 4
            float4 _ShadowLights[MAX_SHADOW_LIGHTS];
            int _ShadowLightCount;

            // Global integer - Set at runtime from settings
            int _ParallaxScaledShadowStepSize;

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
                float3 normal : NORMAL;
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
        
            Texture2D _HeightMap;

            // Use a custom sampler state to disable anisotropic filtering for the raymarched shadows - 16x less samples
            SamplerState linear_repeat_sampler;

            bool _DisableDisplacement;

            // Blue noise tex and sampler
            Texture2D _BlueNoise;
            SamplerState sampler_BlueNoise_point_repeat;
            float4 _BlueNoise_TexelSize;
            int _FrameCount;
        
            v2f vert (appdata v)
            {
                v2f o;
        
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
        
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz, 0)).xyz);
        
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
        
                float displacement = _HeightMap.SampleLevel(linear_repeat_sampler, v.uv, 0, 0).r;
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
                float heightValue = _HeightMap.SampleLevel(linear_repeat_sampler, uv, 0, 0).r;
        
                float altitude = lerp(_MinRadialAltitude, _MaxRadialAltitude, heightValue);

                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)
                altitude = max(altitude, _OceanAltitude);
                #endif

                return altitude;
            }
        
            float GetAltitudeAt(float3 pos, float3 origin)
            {
                float altitude = length(pos - origin) / _ScaleFactor;
                float altAboveRadius = altitude - (_WorldPlanetRadius / _ScaleFactor);

                float realAltitude = altAboveRadius * _ScaleFactor;

                return realAltitude;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // Shadow umbra and penumbra calculation reference: https://iquilezles.org/articles/rmshadows/
            float RaymarchShadow(float3 rayPos, float3 rayDir, float lightWidth, float stepSize, float noise, float3 origin, out float3 rayEndPos)
            {
                float attenuation = 1.0f;
                float t = 0.0f;

                #if defined (BLUE_NOISE)
                    rayPos -= rayDir * noise * stepSize;
                    t -= noise * stepSize;
                #endif

                // Raymarch towards the light
                for (int b = 0; b < _ParallaxScaledShadowStepSize; b++)
                {
                    rayPos += rayDir * stepSize;
                    t += stepSize;

                    // Get ray altitude and terrain height
                    float worldTerrainAltitude = GetTerrainHeightAt(rayPos, origin);
                    float worldRayAltitude = GetAltitudeAt(rayPos, origin);

                    // Angle between the ray and the terrain point below the sample, as seen from the receiver
                    // Steep rays put that point back near the receiver, so it can't occlude
                    float clearance = worldRayAltitude - worldTerrainAltitude;
                    float upDotRay = dot(normalize(rayPos - origin), rayDir);
                    float perpendicular = clearance * sqrt(saturate(1.0f - upDotRay * upDotRay));
                    float along = t - clearance * upDotRay;
                    float occluderAngle = atan2(perpendicular, along);
                    attenuation = min(attenuation, occluderAngle / lightWidth);

                    // Stop when shadowed
                    if (attenuation < -1.0f)
                    {
                        break;
                    }
                }

                rayEndPos = rayPos;

                // Snap to -1 contribution and use a smoothstep function
                attenuation = max(attenuation, -1.0f);
                return 0.25f * (1.0f + attenuation) * (1.0f + attenuation) * (2.0f - attenuation);
            }

            PS_Output frag (v2f i)
            {
                i.worldNormal = normalize(i.worldNormal);

                // Planet origin
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                // Initial height position, add a small amount to prevent immediate self shadow
                float initialHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, _HeightMap.SampleLevel(linear_repeat_sampler, i.uv, 0, 0));
                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)
                initialHeight = max(initialHeight, _OceanAltitude);
                #endif

                initialHeight += SHADOW_BIAS;

                // Setup ray params
                // Shadow bias
                float3 initialRayPos = origin + normalize(i.worldPos - origin) * _WorldPlanetRadius + normalize(i.worldPos - origin) * initialHeight;

                // Shadow normal bias
                float3 dhdx = ddx(i.worldPos);
                float3 dhdy = ddy(i.worldPos);
                float3 geometricNormal = normalize(cross(dhdx, dhdy));

                // The sign of ddy depends on the API's screen space convention (D3D vs OpenGL)
                // Orient the normal away from the planet
                geometricNormal = dot(geometricNormal, i.worldPos - origin) < 0 ? -geometricNormal : geometricNormal;
                float3 dhdz = geometricNormal * (_MaxRadialAltitude - _MinRadialAltitude) * SHADOW_NORMAL_BIAS;

                float3 rayStartPos = initialRayPos + dhdz;

                float rayDistance = _MaxRayDistance;
                float stepSize = rayDistance / float(_ParallaxScaledShadowStepSize);

                float noise = 0.0f;

                // Random noise offset, could use blue noise but this looks good enough, it's only visible at shadow edges and helps remove stepping
                #if defined (BLUE_NOISE)

                    float2 screenUV = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy + 0.5f;
                    noise = _BlueNoise.Sample(sampler_BlueNoise_point_repeat, (screenUV % 64) / 64);
                    noise = (noise + 1.618f * _FrameCount) % 1;

                #endif

                float4 attenuations = 1.0f;
                float3 primaryRayEndPos = rayStartPos;

                int lightCount = min(_ShadowLightCount, MAX_SHADOW_LIGHTS);
                [loop]
                for (int l = 0; l < lightCount; l++)
                {
                    float4 shadowLight = _ShadowLights[l];
                    if (shadowLight.w <= 0.0f)
                    {
                        continue;
                    }

                    float3 rayEndPos;
                    float attenuation = RaymarchShadow(rayStartPos, normalize(shadowLight.xyz), shadowLight.w, stepSize, noise, origin, rayEndPos);
                    attenuations = lerp(attenuations, attenuation, float4(l == int4(0, 1, 2, 3)));

                    if (l == 0)
                    {
                        primaryRayEndPos = rayEndPos;
                    }
                }

                PS_Output output;

                output.shadowAttenuation = attenuations;
                float shadowBlurMaxDistance = 0.05f;
                output.shadowDistance = saturate(distance(primaryRayEndPos, initialRayPos) / shadowBlurMaxDistance);

                #if defined (SHADER_API_GLCORE) || defined (SHADER_API_OPENGL)
                output.depth = i.pos.z - 0.001f;
                #else
                output.depth = i.pos.z + 0.001f;
                #endif

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
            #pragma multi_compile_local _          OCEAN OCEAN_FROM_COLORMAP

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            // Subset of the parallax variables
            float _WorldPlanetRadius;
            float _MinRadialAltitude;
            float _MaxRadialAltitude;

            float _OceanAltitude;

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
                float3 normal : NORMAL;
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
