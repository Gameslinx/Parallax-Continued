Shader "Custom/ParallaxScaled"
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
        _ColorMap("Planet Color Map", 2D) = "white" {}
        _BumpMap("Planet Bump Map", 2D) = "bump" {}
        _HeightMap("Planet Height Map", 2D) = "black" {}
        _OceanColor("Planet Ocean Color", COLOR) = (0,0,0,1)
        _AtmosphereRimMap("Atmosphere Rim", 2D) = "black" {}
        _AtmosphereThickness("Transition Width", Range(0.0001, 5)) = 2

        _Skybox("Skybox", CUBE) = "black" {}

        [Space(10)]
        [Header(Low Textures)]
        [Space(10)]
        _MainTexLow("Low Texture", 2D) = "white" {}
        _BumpMapLow("Low Bump Map", 2D) = "bump" {}

        [Space(10)]
        [Header(Mid Textures)]
        [Space(10)]
        _MainTexMid("Mid Texture", 2D) = "white" {}
        _BumpMapMid("Mid Bump Map", 2D) = "bump" {}

        [Space(10)]
        [Header(High Textures)]
        [Space(10)]
        _MainTexHigh("High Texture", 2D) = "white" {}
        _BumpMapHigh("High Bump Map", 2D) = "bump" {}

        [space(10)]
        [Header(Steep Textures)]
        [Space(10)]
        _MainTexSteep("Steep Texture", 2D) = "white" {}
        _BumpMapSteep("Steep Bump Map", 2D) = "bump" {}

        [Space(10)]
        _InfluenceMap("Influence Map", 2D) = "white" {}
        _DisplacementMap("Displacement Map", 2D) = "black" {}
        _OcclusionMap("Occlusion Map", 2D) = "white" {}

        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _Tiling("Texture Tiling", Range(0.001, 10.0)) = 0.2
        _BiplanarBlendFactor("Biplanar Blend Factor", Range(0.01, 8)) = 1

        [Space(10)]
        [Header(Texture Blending Parameters)]
        [Space(10)]
        _LowMidBlendStart("Low-Mid Fade Blend Start", Range(-0.03, 0.05)) = 1
        _LowMidBlendEnd("Low-Mid Fade Blend End", Range(0, 0.07)) = 2
        [Space(10)]
        _MidHighBlendStart("Mid-High Blend Start", Range(0, 0.1)) = 4
        _MidHighBlendEnd("Mid-High Blend End", Range(0, 0.1)) = 5
        [Space(10)]
        _SteepPower("Steep Power", Range(0.001, 20)) = 1
        _SteepContrast("Steep Contrast", Range(-5, 5)) = 1
        _SteepMidpoint("Steep Midpoint", Range(-1, 1)) = 0

        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        [PowerSlider(3.0)]
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _OceanSpecularPower("Ocean Specular Power", Range(0.001, 200)) = 1
        _OceanSpecularIntensity("Ocean Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1
        _Hapke("Hapke", Range(0.001, 2)) = 1
        _BumpScale("Bump Scale", Range(0.001, 2)) = 1
        _PlanetBumpScale("Planet Bump Scale", Range(0.001, 2)) = 1
        _EmissionColor("Emission Color", COLOR) = (0,0,0)

        _OceanAltitude("Ocean Altitude", Range(-0.001, 0.001)) = 0

        _PlanetRadius("Planet Radius", Range(0, 100000)) = 0
        _WorldPlanetRadius("World Planet Radius", Range(0, 2000)) = 0
        _PlanetOrigin("Planet Origin", VECTOR) = (0,0,0)

        // Saves a multicompile
        _DisableDisplacement("Disable Displacement", int) = 0
        _Debug("Debug", Range(-1, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite On
        //Cull Back
        //
        //  Forward Base Pass
        //
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            #define SCALED

            // Single:  One surface texture
            // Double:  Blend between two altitude based textures
            // Full:    Blend between three altitude based textures
            // All have slope based textures

            // For anyone wondering, the _ after multi_compile tells unity the keyword is a toggle, and avoids creating variants "_ON" and "_OFF"
            // I would move this to ParallaxStructs.cginc but as we're on unity 2019 you can't have preprocessor directives in cgincludes. Sigh
            #pragma multi_compile_local            PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _          INFLUENCE_MAPPING
            #pragma multi_compile_local _          ADVANCED_BLENDING
            #pragma multi_compile_local _          EMISSION
            //#pragma skip_variants POINT_COOKIE LIGHTMAP_ON DIRLIGHTMAP_COMBINED DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING VERTEXLIGHT_ON

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxScaledStructs.cginc"
            #include "../Terrain/ParallaxVariables.cginc"
            #include "ParallaxScaledVariables.cginc"
            #include "../Terrain/ParallaxUtils.cginc"
            #include "ParallaxScaledUtils.cginc"

            #pragma multi_compile_fwdbase

            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            

            // Input
            PARALLAX_FORWARDBASE_STRUCT_APPDATA

            // Vertex to hull shader
            PARALLAX_FORWARDBASE_STRUCT_CONTROL

            // Patch constant function
            PARALLAX_STRUCT_PATCH_CONSTANT

            // Interpolators to frag shader
            PARALLAX_FORWARDBASE_STRUCT_INTERP
            
            // Do expensive shit here!!
            // Before tessellation!!

            // Return type is TessellationControlPoint if Tessellation is enabled, or Interpolators if not
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.biplanarTextureCoords = v.vertex;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal, v.tangent.xyz), 0)).xyz) * v.tangent.w;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.landMask = GetScaledLandMask(o.worldPos, o.worldNormal);
                o.uv = v.uv;
                return o;
            }
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
            {
                TessellationFactors f;

                if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
                {
                    // Cull the patch - This should be set to 1 in the shadow caster
                    // Also set to 1 in the pixel shader, because smooth normals can mess with this
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 1;
                } 
                else
                {
                    float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
                    float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
                    float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);

                    f.edge[0] = min(tessFactor0, _MaxTessellation);
                    f.edge[1] = min(tessFactor1, _MaxTessellation);
                    f.edge[2] = min(tessFactor2, _MaxTessellation);
                    f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
                }
                return f;
            }

            HULL_SHADER_ATTRIBUTES
            TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // Domain shader
            [domain("tri")]
            Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                Interpolators o;

                o.biplanarTextureCoords = BARYCENTRIC_INTERPOLATE(biplanarTextureCoords);
                o.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                o.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                o.worldTangent = normalize(BARYCENTRIC_INTERPOLATE(worldTangent));
                o.worldBinormal = normalize(BARYCENTRIC_INTERPOLATE(worldBinormal));
                o.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);
                o.uv = BARYCENTRIC_INTERPOLATE(uv);
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);

                float displacement = tex2Dlod(_HeightMap, float4(o.uv, 0, 0));
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement);

                o.pos = UnityWorldToClipPos(o.worldPos);

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o,o.pos);

                return o;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                //
                //  Block - Prerequisites
                //

                // Necessary normalizations
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);
                float3 viewDir = normalize(i.viewDir);

                // Construct TBN matrix
                float3 planetNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _PlanetBumpScale);
                float3x3 TBN = BuildTBN(i.worldTangent, i.worldBinormal, i.worldNormal);

                // Get planet normal in world and local space
                float3 worldPlanetNormal = normalize(mul(TBN, float4(planetNormal.xyz, 0)));
                float3 localPlanetNormal = normalize(mul(unity_WorldToObject, float4(worldPlanetNormal, 0))).xyz;
                i.worldNormal = worldPlanetNormal;

                float3 localPos = i.biplanarTextureCoords;

                //
                //  Block - Biplanar Setup
                //  All biplanar sampling is done in local space to keep texture coords snapped to the mesh
                //  Involves some non-ideal matrix transforms to and from local space for accurate normals
                //

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(localPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, localPos, localPlanetNormal, texScale);

                // Red low-mid blend, green mid-high blend, blue steep, alpha midpoint which distinguishes low from high
                // We need to get this mask again on a pixel level because the blending looks much nicer
                float4 landMask = GetScaledLandMask(i.worldPos, i.worldNormal);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE_SCALED
                DECLARE_INFLUENCE_VALUES

                float4 globalDisplacement = SampleBiplanarTexture(_DisplacementMap, params, worldUVs);

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused
                DECLARE_LOW_TEXTURE_SET_SCALED(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET_SCALED(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET_SCALED(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET_SCALED(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                //
                //  Displacement Based Texture Blending
                //

                DECLARE_DISPLACEMENT_TEXTURE_SCALED(displacement, _DisplacementMap)
                CALCULATE_ADVANCED_BLENDING_FACTORS_SCALED(landMask, displacement)
                
                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                NORMAL_FLOAT altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 finalDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                NORMAL_FLOAT finalNormal = lerp(altitudeNormal, steepNormal, landMask.b); 

                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(unity_ObjectToWorld, float4(finalNormal.xyz, 0)).xyz);

                float3 result = CalculateLighting(finalDiffuse, finalNormal.xyz, viewDir, GET_SHADOW, _WorldSpaceLightPos0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                APPLY_EMISSION
                return float4(result, 1 - _PlanetOpacity);
            }
            ENDCG
        }

        //
        //  Shadow Caster Pass
        //

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
        
            #define SCALED

            #pragma multi_compile_local PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_shadowcaster

            #define PARALLAX_SHADOW_CASTER_PASS
        
            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxScaledStructs.cginc"
            #include "../Terrain/ParallaxVariables.cginc"
            #include "ParallaxScaledVariables.cginc"
            #include "../Terrain/ParallaxUtils.cginc"
            #include "ParallaxScaledUtils.cginc"

            PARALLAX_SHADOW_CASTER_STRUCT_APPDATA
            PARALLAX_SHADOW_CASTER_STRUCT_CONTROL
            PARALLAX_STRUCT_PATCH_CONSTANT
            PARALLAX_SHADOW_CASTER_STRUCT_INTERP
        
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;
        
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal, v.tangent.xyz), 0)).xyz) * v.tangent.w;
                o.normal = v.normal;
                o.vertex = v.vertex;
                o.landMask = GetScaledLandMask(o.worldPos, o.worldNormal);
                o.uv = v.uv;
                return o;
            }
        
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
            {
                TessellationFactors f;
        
                if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
                {
                    // Cull the patch - This should be set to 1 in the shadow caster
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 1;
                } 
                else 
                {
                    float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
                    float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
                    float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);
        
                    f.edge[0] = min(tessFactor0, _MaxTessellation);
                    f.edge[1] = min(tessFactor1, _MaxTessellation);
                    f.edge[2] = min(tessFactor2, _MaxTessellation);
                    f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
                }
                return f;
            }
        
            HULL_SHADER_ATTRIBUTES
            TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }
        
            // Domain shader
            [domain("tri")]
            Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                Interpolators v;
        
                v.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                v.vertex = BARYCENTRIC_INTERPOLATE(vertex);
                v.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                v.normal = BARYCENTRIC_INTERPOLATE(normal);
                float2 uv = BARYCENTRIC_INTERPOLATE(uv);
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);
            
                // Defines 'displacedWorldPos'
                float displacement = tex2Dlod(_HeightMap, float4(uv, 0, 0));
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(v, displacement);
        
                v.pos = ParallaxClipSpaceShadowCasterPos(v.worldPos, v.worldNormal);
                v.pos = UnityApplyLinearShadowBias(v.pos);
                
                return v;
            }
        
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                return 0;
            }
        
            ENDCG
        }

        //
        // Forward Add Pass
        // Same as Forward Base pass except we need to calculate the lightDir, as _WorldSpaceLightPos0 is now a position
        // And we must pass through vertex for TRANSFER_VERTEX_TO_FRAGMENT macro, which is adamant on using vertex instead of worldpos :/
        //

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One
            //BlendOp Add
            CGPROGRAM
        
            #define SCALED

            #pragma multi_compile_local           PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _         INFLUENCE_MAPPING
            #pragma multi_compile_fwdadd_fullshadows
        
            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxScaledStructs.cginc"
            #include "../Terrain/ParallaxVariables.cginc"
            #include "ParallaxScaledVariables.cginc"
            #include "../Terrain/ParallaxUtils.cginc"
            #include "ParallaxScaledUtils.cginc"
        
            // Input
            PARALLAX_FORWARDADD_STRUCT_APPDATA
        
            // Vertex to hull shader
            PARALLAX_FORWARDADD_STRUCT_CONTROL
        
            // Patch constant function
            PARALLAX_STRUCT_PATCH_CONSTANT
        
            // Interpolators to frag shader
            PARALLAX_FORWARDADD_STRUCT_INTERP
            
            // Do expensive shit here!!
            // Before tessellation!!
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;
        
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.biplanarTextureCoords = v.vertex;
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal, v.tangent.xyz), 0)).xyz) * v.tangent.w;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.lightDir = _WorldSpaceLightPos0 - o.worldPos;
                o.vertex = v.vertex;
                o.landMask = GetScaledLandMask(o.worldPos, o.worldNormal);
                o.uv = v.uv;
                return o;
            }
        
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
            {
                TessellationFactors f;
        
                if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
                {
                    // Cull the patch - This should be set to 1 in the shadow caster
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0;
                } 
                else 
                {
                    float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
                    float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
                    float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);
        
                    f.edge[0] = min(tessFactor0, _MaxTessellation);
                    f.edge[1] = min(tessFactor1, _MaxTessellation);
                    f.edge[2] = min(tessFactor2, _MaxTessellation);
                    f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
                }
                return f;
            }
        
            HULL_SHADER_ATTRIBUTES
            TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }
        
            // Domain shader
            [domain("tri")]
            Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                // We have to use "v" instead of "o" because it's hardcoded into unity's macros...
                Interpolators v;
        
                v.biplanarTextureCoords = BARYCENTRIC_INTERPOLATE(biplanarTextureCoords);
                v.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                v.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                v.worldTangent = normalize(BARYCENTRIC_INTERPOLATE(worldTangent));
                v.worldBinormal = normalize(BARYCENTRIC_INTERPOLATE(worldBinormal));
                v.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);
                v.lightDir = BARYCENTRIC_INTERPOLATE(lightDir);
                v.vertex = BARYCENTRIC_INTERPOLATE(vertex);
                v.uv = BARYCENTRIC_INTERPOLATE(uv);
                
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);

                // Defines 'displacedWorldPos' 
                float displacement = tex2Dlod(_HeightMap, float4(v.uv, 0, 0));
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(v, displacement);

                v.pos = UnityWorldToClipPos(v.worldPos); 
                
                TRANSFER_VERTEX_TO_FRAGMENT(v);
        
                return v;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                //
                //  Block - Prerequisites
                //

                // Necessary normalizations
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(i.lightDir);

                // Construct TBN matrix
                float3 planetNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _PlanetBumpScale);
                float3x3 TBN = BuildTBN(i.worldTangent, i.worldBinormal, i.worldNormal);

                // Get planet normal in world and local space
                float3 worldPlanetNormal = normalize(mul(TBN, float4(planetNormal.xyz, 0)));
                float3 localPlanetNormal = normalize(mul(unity_WorldToObject, float4(worldPlanetNormal, 0))).xyz;
                i.worldNormal = worldPlanetNormal;

                float3 localPos = i.biplanarTextureCoords;

                //
                //  Block - Biplanar Setup
                //  All biplanar sampling is done in local space to keep texture coords snapped to the mesh
                //  Involves some non-ideal matrix transforms to and from local space for accurate normals
                //

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(localPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, localPos, localPlanetNormal, texScale);

                // Red low-mid blend, green mid-high blend, blue steep, alpha midpoint which distinguishes low from high
                // We need to get this mask again on a pixel level because the blending looks much nicer
                float4 landMask = GetScaledLandMask(i.worldPos, i.worldNormal);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE_SCALED
                DECLARE_INFLUENCE_VALUES

                float4 globalDisplacement = SampleBiplanarTexture(_DisplacementMap, params, worldUVs);

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused
                DECLARE_LOW_TEXTURE_SET_SCALED(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET_SCALED(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET_SCALED(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET_SCALED(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                //
                //  Displacement Based Texture Blending
                //

                DECLARE_DISPLACEMENT_TEXTURE_SCALED(displacement, _DisplacementMap)
                CALCULATE_ADVANCED_BLENDING_FACTORS_SCALED(landMask, displacement)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                float3 altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 finalDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                float3 finalNormal = lerp(altitudeNormal, steepNormal, landMask.b); 

                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(unity_ObjectToWorld, float4(finalNormal.xyz, 0)).xyz);

                float atten = LIGHT_ATTENUATION(i);
                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, lightDir);

                return float4(result, atten * (1 - _PlanetOpacity));
            }
            ENDCG
        }

        //
        // Deferred Pass
        //

        Pass
        {
            Tags { "LightMode" = "Deferred" }

            //Stencil
			//{
			//    Ref 2
			//    Comp Always
			//    Pass Replace
			//}

            CGPROGRAM

            // Single:  One surface texture
            // Double:  Blend between two altitude based textures
            // Full:    Blend between three altitude based textures
            // All have slope based textures

            #define PARALLAX_DEFERRED_PASS
            #define SCALED

            // For anyone wondering, the _ after multi_compile tells unity the keyword is a toggle, and avoids creating variants "_ON" and "_OFF"
            // I would move this to ParallaxStructs.cginc but as we're on unity 2019 you can't have preprocessor directives in cgincludes. Sigh
            #pragma multi_compile_local            PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _          INFLUENCE_MAPPING
            #pragma multi_compile_local _          EMISSION
            #pragma multi_compile_local _          ADVANCED_BLENDING
            #pragma multi_compile_local _          AMBIENT_OCCLUSION
            #pragma multi_compile_local _          OCEAN OCEAN_FROM_COLORMAP
            #pragma multi_compile_local _          ATMOSPHERE
            #pragma multi_compile _ UNITY_HDR_ON

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityPBSLighting.cginc"
            
            #include "ParallaxScaledStructs.cginc"
            #include "../Terrain/ParallaxVariables.cginc"
            #include "ParallaxScaledVariables.cginc"
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "../Terrain/ParallaxUtils.cginc"
            #include "ParallaxScaledUtils.cginc"

            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            

            // Input
            PARALLAX_FORWARDBASE_STRUCT_APPDATA

            // Vertex to hull shader
            PARALLAX_FORWARDBASE_STRUCT_CONTROL

            // Patch constant function
            PARALLAX_STRUCT_PATCH_CONSTANT

            // Interpolators to frag shader
            PARALLAX_FORWARDBASE_STRUCT_INTERP
            
            // Do expensive shit here!!
            // Before tessellation!!

            // Return type is TessellationControlPoint if Tessellation is enabled, or Interpolators if not
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;

                o.pos = UnityObjectToClipPos(v.vertex);
                // Scaled planets rotate/translate. Use local space coords but scale correctly
                o.biplanarTextureCoords = v.vertex;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.worldTangent = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz);
                o.worldBinormal = normalize(mul(unity_ObjectToWorld, float4(cross(v.normal, v.tangent.xyz), 0)).xyz) * v.tangent.w;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.landMask = GetScaledLandMask(o.worldPos, o.worldNormal);
                o.uv = v.uv;
                return o;
            }
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
            {
                TessellationFactors f;

                if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
                {
                    // Cull the patch - This should be set to 1 in the shadow caster
                    // Also set to 1 in the pixel shader, because smooth normals can mess with this
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 1;
                } 
                else
                {
                    float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
                    float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
                    float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);

                    f.edge[0] = min(tessFactor0, _MaxTessellation);
                    f.edge[1] = min(tessFactor1, _MaxTessellation);
                    f.edge[2] = min(tessFactor2, _MaxTessellation);
                    f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
                }
                return f;
            }

            HULL_SHADER_ATTRIBUTES
            TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // Domain shader
            [domain("tri")]
            Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                Interpolators o;

                o.biplanarTextureCoords = BARYCENTRIC_INTERPOLATE(biplanarTextureCoords);
                o.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                o.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                o.worldTangent = normalize(BARYCENTRIC_INTERPOLATE(worldTangent));
                o.worldBinormal = normalize(BARYCENTRIC_INTERPOLATE(worldBinormal));
                o.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);
                o.uv = BARYCENTRIC_INTERPOLATE(uv);
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);

                // Defines 'displacedWorldPos'
                float displacement = tex2Dlod(_HeightMap, float4(o.uv, 0, 0)).r;
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement);

                o.pos = UnityWorldToClipPos(o.worldPos);
                o.biplanarTextureCoords = mul(unity_WorldToObject, float4(o.worldPos, 1)).xyz;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o,o.pos);

                return o;
            }

            float _Debug;

            void Frag_Shader (Interpolators i, PARALLAX_DEFERRED_OUTPUT_BUFFERS, float depth : SV_Depth)
            {   
                //
                //  Block - Prerequisites
                //

                // Necessary normalizations
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);
                float3 viewDir = normalize(i.viewDir);

                // Height value
                float planetHeight = tex2D(_HeightMap, i.uv).r;
                planetHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, planetHeight);

                // Atmosphere
                float3 atmosphereColor = GetAtmosphereColor(i.worldNormal, viewDir);

                // Construct TBN matrix
                float3 planetNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _PlanetBumpScale);
                float3x3 TBN = BuildTBN(i.worldTangent, i.worldBinormal, i.worldNormal);

                // Get planet normal in world and local space
                float3 flatWorldNormal = i.worldNormal;
                float3 worldPlanetNormal = normalize(mul(TBN, float4(planetNormal.xyz, 0)));
                float3 localPlanetNormal = normalize(mul(unity_WorldToObject, float4(worldPlanetNormal, 0))).xyz;
                i.worldNormal = worldPlanetNormal;

                float3 localPos = i.biplanarTextureCoords;

                //
                //  Block - Biplanar Setup
                //  All biplanar sampling is done in local space to keep texture coords snapped to the mesh
                //  Involves some non-ideal matrix transforms to and from local space for accurate normals
                //

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(localPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, localPos, localPlanetNormal, texScale);

                // Red low-mid blend, green mid-high blend, blue steep, alpha midpoint which distinguishes low from high
                // We need to get this mask again on a pixel level because the blending looks much nicer
                float4 landMask = GetScaledLandMask(planetHeight, flatWorldNormal, i.worldNormal);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE_SCALED
                DECLARE_INFLUENCE_VALUES

                float4 globalDisplacement = SampleBiplanarTexture(_DisplacementMap, params, worldUVs);

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                DECLARE_LOW_TEXTURE_SET_SCALED(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET_SCALED(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET_SCALED(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET_SCALED(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                DECLARE_AMBIENT_OCCLUSION_TEXTURE_SCALED(occlusion, _OcclusionMap)

                // Only if displacement blending is enabled
                DECLARE_DISPLACEMENT_TEXTURE_SCALED(displacement, _DisplacementMap)
                CALCULATE_ADVANCED_BLENDING_FACTORS_SCALED(landMask, displacement)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                NORMAL_FLOAT altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 finalDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                NORMAL_FLOAT finalNormal = lerp(altitudeNormal, steepNormal, landMask.b);

                // Ocean
                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)

                GET_OCEAN_DIFFUSE
                float3 oceanNormal = normalize(localPos);

                if (planetHeight <= _OceanAltitude)
                {
                    finalDiffuse = oceanDiffuse;
                    finalNormal.xyz = oceanNormal;
                    _SpecularPower = _OceanSpecularPower;
                    _SpecularIntensity = _OceanSpecularIntensity;
                }

                #endif
                
                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(unity_ObjectToWorld, float4(finalNormal.xyz, 0)).xyz);

                BLEND_OCCLUSION(landMask, occlusion)

                // Deferred functions
                SurfaceOutputStandardSpecular surfaceInput = GetPBRStruct(finalDiffuse, GET_EMISSION, finalNormal.xyz, i.worldPos ADDITIONAL_PBR_PARAMS);
                UnityGI gi = GetUnityGI();
                UnityGIInput giInput = GetGIInput(i.worldPos, viewDir);
                LightingStandardSpecular_GI(surfaceInput, giInput, gi);
                
                OUTPUT_GBUFFERS(surfaceInput, gi)
                SET_OUT_SHADOWMASK(i)

                //
                //
                //
                //
                //

                //outGBuffer0.xyz = 1;
                //outGBuffer2.xyz = i.biplanarTextureCoords * 0.5f + 0.5f;

                float3 worldPos = i.worldPos;

                //half4 gbuffer0 = tex2D (_CameraGBufferTexture0, uv);
                //half4 gbuffer1 = tex2D (_CameraGBufferTexture1, uv);
                //half4 gbuffer2 = tex2D (_CameraGBufferTexture2, uv);
                //UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);

                float3 eyeVec = -viewDir;
                eyeVec = mul(_SkyboxRotation, eyeVec);
                finalNormal = mul(_SkyboxRotation, finalNormal);

                // Rotate eyevec by the skybox rotation
                //eyeVec = normalize(mul(_SkyboxRotation, eyeVec).xyz);

                half oneMinusReflectivity = 1 - max (max (outGBuffer1.r, outGBuffer1.g), outGBuffer1.b);

                // Unused member don't need to be initialized
                UnityGIInput d;
                d.worldPos = worldPos;
                d.worldViewDir = -eyeVec;
                d.probeHDR[0] = _Skybox_HDR;
                d.probeHDR[1] = _Skybox_HDR;
                d.boxMin[0].w = 0; // 1 in .w allow to disable blending in UnityGI_IndirectSpecular call since it doesn't work in Deferred
                d.boxMin[1] = 0;
                d.boxMax[0] = 0;
                d.boxMax[1] = 0;

                d.light.color = 0;
                d.light.dir = half3(0, 1, 0);
                d.light.ndotl = 0;
                d.atten = 1;
                d.lightmapUV = 0.0f;
                d.ambient.rgb = 0;

                d.probePosition[0] = 0;
                d.probePosition[1] = 0;


                Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(outGBuffer2.w, -eyeVec, finalNormal, outGBuffer1.rgb);

                float occlusion2 = 1;
                half3 env0 = UnityGI_IndirectSpecularBasic(d, outGBuffer1.w, g);

                UnityLight light;
                light.color = half3(0, 0, 0);
                light.dir = half3(0, 1, 0);
                
                UnityIndirect ind;
                ind.diffuse = 0;
                ind.specular = env0;
                
                half3 rgb = UNITY_BRDF_PBS (0, outGBuffer1.xyz, oneMinusReflectivity, outGBuffer2.w, finalNormal, -eyeVec, light, ind).rgb;
                
                rgb *= _EnvironmentMapFactor;
                
                rgb += atmosphereColor;
                SET_OUT_EMISSION(float4(rgb, 1));
            }
            ENDCG
        }
    }
    //FallBack "VertexLit"
}
    