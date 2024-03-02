Shader "Custom/Parallax"
{
    Properties
    {
        [Header(Tessellation Settings)]
        [Space(10)]
        _MaxTessellation("Max Tessellation", Range(1, 64)) = 1
        _TessellationEdgeLength("Tessellation Edge Length (Pixels)", Range(0.01, 100)) = 1
        _MaxTessellationRange("Max Tessellation Range", Range(1, 100)) = 5

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

        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _Tiling("Texture Tiling", Range(0.001, 0.2)) = 0.2
        _DisplacementScale("Displacement Scale", Range(0, 0.3)) = 0
        _DisplacementOffset("Displacement Offset", Range(-1, 1)) = 0
        _BiplanarBlendFactor("Biplanar Blend Factor", Range(0.01, 8)) = 1

        [Space(10)]
        [Header(Texture Blending Parameters)]
        [Space(10)]
        _LowMidBlendStart("Low-Mid Fade Blend Start", Range(-5, 10)) = 1
        _LowMidBlendEnd("Low-Mid Fade Blend End", Range(-5, 10)) = 2
        [Space(10)]
        _MidHighBlendStart("Mid-High Blend Start", Range(-5, 10)) = 4
        _MidHighBlendEnd("Mid-High Blend End", Range(-5, 10)) = 5
        [Space(10)]
        _SteepPower("Steep Power", Range(0.001, 10)) = 1
        _SteepContrast("Steep Contrast", Range(-5, 5)) = 1
        _SteepMidpoint("Steep Midpoint", Range(-1, 1)) = 0

        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        [PowerSlider(3.0)]
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1

        [Space(10)]
        [Header(Other Params)]
        [Space(10)]
        _TerrainShaderOffset("Terrain Shader Offset", vector) = (0, 0, 0)
        _PlanetOrigin("Planet Origin", vector) = (0, 0, 0)
        _PlanetRadius("Planet Radius", Range(0.01, 5000)) = 5 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        //
        //  Forward Base Pass
        //

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"

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
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.color = v.color;
                o.landMask = GetLandMask(o.worldPos, o.worldNormal);
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
                Interpolators o;

                o.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                o.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                o.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);
                o.color = BARYCENTRIC_INTERPOLATE(color);
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);

                float terrainDistance = length(o.viewDir);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, o.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, o.worldNormal);

                // Defines 'displacedWorldPos'
                CALCULATE_VERTEX_DISPLACEMENT(o, landMask)
                o.pos = UnityWorldToClipPos(displacedWorldPos);
            
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o,o.pos);

                return o;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                float terrainDistance = length(i.viewDir);

                // Maybe gamma correct at some point
                float3 vertexColor = i.color;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                
                // Red low-mid blend, green mid-high blend, blue steep, alpha midpoint which distinguishes low from high
                // We need to get this mask again on a pixel level because the blending looks much nicer
                float4 landMask = GetLandMask(i.worldPos, i.worldNormal);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                float4 globalDisplacement = SampleBiplanarTexture(_DisplacementMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused
                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                float3 altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 finalDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                float3 finalNormal = lerp(altitudeNormal, steepNormal, landMask.b); 

                //return float4(finalNormal, 1);

                //return float4(i.landMask, 1);

                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, _WorldSpaceLightPos0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return float4(result, 1);
            }
            ENDCG
        }

        //
        //  Shadow Caster Pass
        //

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
        
            #define PARALLAX_SHADOW_CASTER_PASS
        
            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"
        
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
                o.normal = v.normal;
                o.vertex = v.vertex;
                o.landMask = GetLandMask(o.worldPos, o.worldNormal);
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
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);
            
                float terrainDistance = length(_WorldSpaceCameraPos - v.worldPos);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, v.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, v.worldNormal);
                
                // Defines 'displacedWorldPos'
                CALCULATE_VERTEX_DISPLACEMENT(v, landMask)
        
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(v)
                v.pos = UnityWorldToClipPos(displacedWorldPos);
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
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One
            BlendOp Add
            CGPROGRAM
        
            #pragma multi_compile_fwdadd_fullshadows
        
            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"
        
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
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.lightDir = _WorldSpaceLightPos0 - o.worldPos;
                o.color = v.color;
                o.vertex = v.vertex;
                o.landMask = GetLandMask(o.worldPos, o.worldNormal);
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
        
                v.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                v.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                v.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);
                v.color = BARYCENTRIC_INTERPOLATE(color);
                v.lightDir = BARYCENTRIC_INTERPOLATE(lightDir);
                v.vertex = BARYCENTRIC_INTERPOLATE(vertex);
                
                float4 landMask = BARYCENTRIC_INTERPOLATE(landMask);

                float terrainDistance = length(v.viewDir);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, v.worldPos)
        
                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, v.worldNormal);
        
                // Defines 'displacedWorldPos' 
                CALCULATE_VERTEX_DISPLACEMENT(v, landMask)
                //TRANSFER_SHADOW(o);
                v.pos = UnityWorldToClipPos(displacedWorldPos); 
                
                TRANSFER_VERTEX_TO_FRAGMENT(v);
        
                return v;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                float terrainDistance = length(i.viewDir);

                // Maybe gamma correct at some point
                float3 vertexColor = i.color;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(i.lightDir);

                // Red low-mid blend, green mid-high blend, blue steep, alpha midpoint which distinguishes low from high
                // We need to get this mask again on a pixel level because the blending looks much nicer
                float4 landMask = GetLandMask(i.worldPos, i.worldNormal);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                float4 globalDisplacement = SampleBiplanarTexture(_DisplacementMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

                //
                // Localised altitude based textures
                // Totals 16 texture samples, BUH!
                //

                // These declarations perform the texture samples, and within them are checks to see if they should be skipped or not
                // They will be optimized out if unused
                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                float3 altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 finalDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                float3 finalNormal = lerp(altitudeNormal, steepNormal, landMask.b); 

                //return float4(finalNormal, 1);

                //return float4(i.landMask, 1);
                float atten = LIGHT_ATTENUATION(i);
                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, lightDir);
                //UNITY_APPLY_FOG(i.fogCoord, result);
                return float4(result, atten);
            }
            ENDCG
        }
    }
    //FallBack "VertexLit"
}
    