Shader "Custom/ParallaxScaledBaked"
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
        _EmissiveMap("Planet Emissive Map", 2D) = "black" {}
        _OceanColor("Planet Ocean Color", COLOR) = (0,0,0,1)
        _AtmosphereRimMap("Atmosphere Rim", 2D) = "black" {}
        _AtmosphereThickness("Transition Width", Range(0.0001, 5)) = 2

        _Skybox("Skybox", CUBE) = "black" {}

        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        [PowerSlider(3.0)]
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _OceanSpecularPower("Ocean Specular Power", Range(0.001, 200)) = 1
        _OceanSpecularIntensity("Ocean Specular Intensity", Range(0.0, 5.0)) = 1
        _EmissiveIntensity("Emissive Intensity", Range(0.0, 5.0)) = 1
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
            #define BAKED

            // Single:  One surface texture
            // Double:  Blend between two altitude based textures
            // Full:    Blend between three altitude based textures
            // All have slope based textures

            // For anyone wondering, the _ after multi_compile tells unity the keyword is a toggle, and avoids creating variants "_ON" and "_OFF"
            // I would move this to ParallaxStructs.cginc but as we're on unity 2019 you can't have preprocessor directives in cgincludes. Sigh
            #pragma multi_compile_local _          OCEAN OCEAN_FROM_COLORMAP
            #pragma multi_compile_local _          ATMOSPHERE
            #pragma multi_compile_local _          SCALED_EMISSIVE_MAP
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

                // Height value
                float planetHeight = tex2D(_HeightMap, i.uv).r;
                planetHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, planetHeight);

                // Atmosphere
                float3 atmosphereColor = GetAtmosphereColor(i.worldNormal, viewDir);

                // Construct TBN matrix
                float3 planetNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _PlanetBumpScale);
                float3x3 TBN = BuildTBN(i.worldTangent, i.worldBinormal, i.worldNormal);

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                float4 finalDiffuse = diffuseColor;
                float3 finalNormal = planetNormal;

                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(TBN, float4(finalNormal.xyz, 0)).xyz);

                // Ocean
                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)

                GET_OCEAN_DIFFUSE

                if (planetHeight <= _OceanAltitude)
                {
                    finalDiffuse = oceanDiffuse;
                    finalNormal.xyz = i.worldNormal;

                    _SpecularPower = _OceanSpecularPower;
                    _SpecularIntensity = _OceanSpecularIntensity;
                }

                #endif

                float3 result = CalculateLighting(finalDiffuse, finalNormal.xyz, viewDir, GET_SHADOW, _WorldSpaceLightPos0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                APPLY_SCALED_EMISSION
                return float4(result + atmosphereColor, 1);
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
            #define BAKED

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
            #define BAKED

            #pragma multi_compile_local _         OCEAN OCEAN_FROM_COLORMAP
            #pragma multi_compile_local _         ATMOSPHERE
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
                float displacement = tex2Dlod(_HeightMap, float4(v.uv, 0, 0)).r;
                CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(v, displacement);

                v.pos = UnityWorldToClipPos(v.worldPos);

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

                // Height value
                float planetHeight = tex2D(_HeightMap, i.uv).r;
                planetHeight = lerp(_MinRadialAltitude, _MaxRadialAltitude, planetHeight);

                // Atmosphere
                float3 atmosphereColor = GetAtmosphereColor(i.worldNormal, viewDir);

                // Construct TBN matrix
                float3 planetNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _PlanetBumpScale);
                float3x3 TBN = BuildTBN(i.worldTangent, i.worldBinormal, i.worldNormal);

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                float4 finalDiffuse = diffuseColor;
                float3 finalNormal = planetNormal;

                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(TBN, float4(finalNormal.xyz, 0)).xyz);

                // Ocean
                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)

                GET_OCEAN_DIFFUSE
                float3 oceanNormal = i.worldNormal;

                if (planetHeight <= _OceanAltitude)
                {
                    finalDiffuse = oceanDiffuse;
                    finalNormal.xyz = i.worldNormal;

                    _SpecularPower = _OceanSpecularPower;
                    _SpecularIntensity = _OceanSpecularIntensity;
                }

                #endif

                float atten = LIGHT_ATTENUATION(i);
                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, lightDir);

                return float4(result + atmosphereColor, atten);
            }
            ENDCG
        }

        //
        // Deferred Pass
        //

        Pass
        {
            Tags { "LightMode" = "Deferred" }

            CGPROGRAM

            // Single:  One surface texture
            // Double:  Blend between two altitude based textures
            // Full:    Blend between three altitude based textures
            // All have slope based textures

            #define PARALLAX_DEFERRED_PASS
            #define SCALED
            #define BAKED

            // For anyone wondering, the _ after multi_compile tells unity the keyword is a toggle, and avoids creating variants "_ON" and "_OFF"
            // I would move this to ParallaxStructs.cginc but as we're on unity 2019 you can't have preprocessor directives in cgincludes. Sigh
            #pragma multi_compile_local _          OCEAN OCEAN_FROM_COLORMAP
            #pragma multi_compile_local _          ATMOSPHERE
            #pragma multi_compile_local _          SCALED_EMISSIVE_MAP
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

                float4 diffuseColor = tex2D(_ColorMap, i.uv);
                float3 vertexColor = diffuseColor.rgb;

                float4 finalDiffuse = diffuseColor;
                float3 finalNormal = planetNormal;

                // Convert local normal back into world space
                finalNormal.xyz = normalize(mul(TBN, float4(finalNormal.xyz, 0)).xyz);
                // Ocean
                #if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)

                GET_OCEAN_DIFFUSE

                if (planetHeight <= _OceanAltitude)
                {
                    finalDiffuse = oceanDiffuse;
                    finalNormal.xyz = i.worldNormal;

                    _SpecularPower = _OceanSpecularPower;
                    _SpecularIntensity = _OceanSpecularIntensity;
                }

                #endif

                // Deferred functions
                // Output diffuse, normals, specular
                SurfaceOutputStandardSpecular surfaceInput = GetPBRStruct(finalDiffuse, 0, finalNormal.xyz, i.worldPos ADDITIONAL_PBR_PARAMS);
                UnityGI gi = GetUnityGI();
                UnityGIInput giInput = GetGIInput(i.worldPos, viewDir);
                LightingStandardSpecular_GI(surfaceInput, giInput, gi);
                
                OUTPUT_GBUFFERS(surfaceInput, gi)
                SET_OUT_SHADOWMASK(i)

                //
                //  Environment Reflections (can't just use reflection probe)
                //  And atmosphere
                //

                float3 eyeVec = -viewDir;
                eyeVec = mul(_SkyboxRotation, eyeVec);
                finalNormal.xyz = mul(_SkyboxRotation, finalNormal);

                Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(outGBuffer2.w, -eyeVec, finalNormal.xyz, outGBuffer1.rgb);

                half3 envColor = UnityGI_IndirectSpecularBasic(_Skybox_HDR, outGBuffer1.w, g);
                half3 rgb = BRDF_EnvironmentReflection(outGBuffer1.xyz, outGBuffer2.w, finalNormal.xyz, -eyeVec, envColor).rgb;
                
                rgb *= _EnvironmentMapFactor;
                rgb += atmosphereColor;
                rgb += GET_SCALED_EMISSION;

                SET_OUT_EMISSION(float4(rgb, 1));
            }
            ENDCG
        }
    }
    //FallBack "VertexLit"
}
    