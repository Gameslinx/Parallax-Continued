Shader "Unlit/RaymarchedShadows"
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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite On
        ZTest On
        //Cull Back
        //
        //  Forward Base Pass
        //
        //Pass
        //{
        //    Blend SrcAlpha OneMinusSrcAlpha
        //    Tags { "LightMode" = "ForwardBase" }
        //    CGPROGRAM
        //
        //    #define SCALED
        //
        //    // Single:  One surface texture
        //    // Double:  Blend between two altitude based textures
        //    // Full:    Blend between three altitude based textures
        //    // All have slope based textures
        //
        //    // For anyone wondering, the _ after multi_compile tells unity the keyword is a toggle, and avoids creating variants "_ON" and "_OFF"
        //    // I would move this to ParallaxStructs.cginc but as we're on unity 2019 you can't have preprocessor directives in cgincludes. Sigh
        //    #pragma multi_compile_local            PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
        //    #pragma multi_compile_local _          INFLUENCE_MAPPING
        //    #pragma multi_compile_local _          ADVANCED_BLENDING
        //    #pragma multi_compile_local _          EMISSION
        //    //#pragma skip_variants POINT_COOKIE LIGHTMAP_ON DIRLIGHTMAP_COMBINED DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING VERTEXLIGHT_ON
        //
        //    #include "UnityCG.cginc"
        //    #include "Lighting.cginc"
        //    #include "AutoLight.cginc"
        //    
        //    #include "../Includes/ParallaxGlobalFunctions.cginc" 
        //    #include "../Terrain/ParallaxVariables.cginc"
        //    #include "ParallaxScaledVariables.cginc"
        //    #include "../Terrain/ParallaxUtils.cginc"
        //    #include "ParallaxScaledUtils.cginc"
        //
        //    #pragma multi_compile_fwdbase
        //
        //    #pragma vertex Vertex_Shader
        //    #pragma hull Hull_Shader
        //    #pragma domain Domain_Shader
        //    #pragma fragment Frag_Shader
        //    
        //
        //    // Input
        //
        //    struct appdata
        //    {
        //        float4 vertex : POSITION;
        //        float3 normal : NORMAL;
        //        float2 uv : TEXCOORD0;
        //    };
        //
        //    struct TessellationControlPoint
        //    {                                            
        //        float4 pos : SV_POSITION;                
        //        float3 worldPos : INTERNALTESSPOS;       
        //        float3 worldNormal : NORMAL;             
        //        float2 uv : TEXCOORD0;                   
        //    };
        //
        //    struct TessellationFactors
        //    {
        //        float edge[3] : SV_TessFactor;
        //        float inside : SV_InsideTessFactor;
        //    };
        //
        //    struct Interpolators                            
        //    {                                               
        //        float4 pos : SV_POSITION;                   
        //        float3 worldPos : TEXCOORD2;                
        //        float3 worldNormal : NORMAL;                
        //        float2 uv : TEXCOORD0;                                            
        //    };
        //    
        //    // Do expensive shit here!!
        //    // Before tessellation!!
        //
        //    // Return type is TessellationControlPoint if Tessellation is enabled, or Interpolators if not
        //    TessellationControlPoint Vertex_Shader (appdata v)
        //    {
        //        TessellationControlPoint o;
        //
        //        o.pos = UnityObjectToClipPos(v.vertex);
        //        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        //        o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
        //        o.uv = v.uv;
        //        return o;
        //    }
        //    TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
        //    {
        //        TessellationFactors f;
        //
        //        if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
        //        {
        //            // Cull the patch - This should be set to 1 in the shadow caster
        //            // Also set to 1 in the pixel shader, because smooth normals can mess with this
        //            f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 1;
        //        } 
        //        else
        //        {
        //            float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
        //            float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
        //            float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);
        //
        //            f.edge[0] = min(tessFactor0, _MaxTessellation);
        //            f.edge[1] = min(tessFactor1, _MaxTessellation);
        //            f.edge[2] = min(tessFactor2, _MaxTessellation);
        //            f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
        //        }
        //        return f;
        //    }
        //
        //    [domain("tri")]
        //    [outputcontrolpoints(3)]
        //    [outputtopology("triangle_cw")]
        //    [patchconstantfunc("PatchConstantFunction")]
        //    [partitioning("fractional_odd")]
        //    TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
        //    {
        //        return patch[id];
        //    }
        //
        //    // Domain shader
        //    [domain("tri")]
        //    Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
        //    {
        //        Interpolators o;
        //
        //        o.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
        //        o.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
        //        o.uv = BARYCENTRIC_INTERPOLATE(uv);
        //
        //        float displacement = tex2Dlod(_HeightMap, float4(o.uv, 0, 0));
        //        CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement);
        //
        //        o.pos = UnityWorldToClipPos(o.worldPos);
        //
        //        return o;
        //    }
        //    fixed4 Frag_Shader (Interpolators i) : SV_Target
        //    {   
        //        //
        //        //  Block - Prerequisites
        //        //
        //
        //        // Necessary normalizations
        //        i.worldNormal = normalize(i.worldNormal);
        //
        //        return tex2D(_HeightMap, i.uv);
        //    }
        //    ENDCG
        //}

        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            // Subset of the parallax variables
            float _WorldPlanetRadius;
            float _MinRadialAltitude;
            float _MaxRadialAltitude;
        
            // Mesh to real planet scale factor
            float _ScaleFactor;
        
            // Parallax includes
            #include "../../Includes/ParallaxGlobalFunctions.cginc" 
            #include "../ParallaxScaledStructs.cginc"
            #include "../../Terrain/ParallaxVariables.cginc"
            #include "../../Terrain/ParallaxUtils.cginc"
            #include "../ScaledDisplacementUtils.cginc"
            
            sampler2D _CameraDepthTexture;
            sampler2D _DepthTex;
        
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
            };
        
            float GetTerrainHeightAt(float3 pos, float3 origin)
            {
                // Also transform by the planet rotation
                float3 dirFromCenter = normalize(pos - origin);
                float2 uv = DirectionToEquirectangularUV(dirFromCenter);
                float heightValue = tex2Dlod(_HeightMap, float4(uv, 0, 0));
        
                return lerp(_MinRadialAltitude, _MaxRadialAltitude, heightValue);
            }
        
            float GetAltitudeAt(float3 pos, float3 origin)
            {
                float altitude = length(pos - origin) / _ScaleFactor;
                float altAboveRadius = altitude - (_WorldPlanetRadius / _ScaleFactor);

                return altAboveRadius * _ScaleFactor;
            }
        
            PS_Output frag (v2f i)
            {
                i.worldNormal = normalize(i.worldNormal);
        
                float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float2 sphericalUV = DirectionToEquirectangularUV(normalize(i.worldPos - origin));
        
                fixed4 col = tex2Dlod(_HeightMap, float4(i.uv, 0, 0));
        
                // Shadow experiments
        
                float shadowAttenuation = 1;
        
                int stepCount = 128;
                float worldMeshRadius = 0.5;
        
                float ETA = 0.0001;
        
                // Start slightly above to prevent self shadow
                // Start at terrain surface

                float initialHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, tex2Dlod(_HeightMap, float4(i.uv,0,0)));
                initialHeight += ETA;

                float3 initialRayPos = origin + normalize(i.worldNormal) * 0.5 + i.worldNormal * initialHeight; //i.worldPos + i.worldNormal * 0.001f;//GetTerrainHeightAt(i.worldPos, origin) * i.worldNormal; //GetTerrainHeightAt(i.worldNormal * _WorldPlanetRadius * _ScaleFactor, origin) + i.worldNormal * ETA;
                float3 rayPos = initialRayPos;
        
                float3 rayDir = normalize(_WorldSpaceLightPos0);
                float stepSize = (float)worldMeshRadius / (float)stepCount;
        
                for (int b = 0; b < stepCount; b++)
                {
                    float worldTerrainAltitude = GetTerrainHeightAt(rayPos, origin);
                    float worldRayAltitude = GetAltitudeAt(rayPos, origin);
        
                    if (worldTerrainAltitude > worldRayAltitude)
                    {
                        shadowAttenuation = 0;
                        break;
                    }
        
                    // Advance ray
                    rayPos += rayDir * stepSize;
                }
        
                PS_Output output;
        
                // Max possible distance a ray could travel (planet radius world space)
        
                output.shadowAttenuation = shadowAttenuation;
                output.shadowDistance = (distance(rayPos, initialRayPos));

                float pixelDepth = i.pos.z / i.pos.w;
                float sceneDepth = tex2D(_CameraDepthTexture, i.screenPos.xy / i.screenPos.w);

                if (sceneDepth < pixelDepth)
                {
                    //discard;
                }

                //output.shadowAttenuation = LinearEyeDepth(sceneDepth);
                //output.shadowAttenuation = pixelDepth;

                //float atten = SHADOW_ATTENUATION(i);
                //output.shadowAttenuation = 1;

                return output;
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
            
            sampler2D _CameraDepthTexture;
            sampler2D _DepthTex;
        
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

                o.pos = ParallaxClipSpaceShadowCasterPos(o.worldPos, worldNormal);
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
