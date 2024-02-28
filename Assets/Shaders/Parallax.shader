Shader "Custom/Parallax"
{
    Properties
    {
        [Header(Tessellation Settings)]
        [Space(10)]
        _MaxTessellation("Max Tessellation", Range(1, 64)) = 1
        _TessellationEdgeLength("Tessellation Edge Length (Pixels)", Range(0.01, 100)) = 1

        [Space(10)]
        [Header(Textures)]
        [Space(10)]
        _MainTex("Texture", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _DisplacementMap("Displacement Map", 2D) = "black" {}

        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _Tiling("Texture Tiling", Range(0.001, 30)) = 0.2
        _DisplacementScale("Displacement Scale", Range(0, 0.3)) = 0
        _BiplanarBlendFactor("Biplanar Blend Factor", Range(0.01, 8)) = 1

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
        _TerrainShaderOffset("Terrain Shader Offset", VECTOR) = (0, 0, 0)
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

            #pragma multi_compile_fwdbase

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
            PARALLAX_FORWARDBASE_STRUCT_APPDATA

            // Vertex to hull shader
            PARALLAX_FORWARDBASE_STRUCT_CONTROL

            // Patch constant function
            PARALLAX_STRUCT_PATCH_CONSTANT

            // Interpolators to frag shader
            PARALLAX_FORWARDBASE_STRUCT_INTERP
            
            // Do expensive shit here!!
            // Before tessellation!!
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.color = v.color;
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

                float terrainDistance = length(o.viewDir);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, o.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, o.worldNormal);

                // Defines 'displacedWorldPos'
                CALCULATE_VERTEX_DISPLACEMENT(o)
                o.pos = UnityWorldToClipPos(displacedWorldPos);
            
                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                i.worldNormal = normalize(i.worldNormal);
                // Calculate scaling params
                float terrainDistance = length(i.viewDir);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                float3 viewDir = normalize(i.viewDir);

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                fixed4 col = SampleBiplanarTexture(_MainTex, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);
                float3 normal = SampleBiplanarNormal(_BumpMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

                float3 result = CalculateLighting(col, normal, viewDir, GET_SHADOW);

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

                float terrainDistance = length(_WorldSpaceCameraPos - v.worldPos);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, v.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, v.worldNormal);

                // Defines 'displacedWorldPos'
                CALCULATE_VERTEX_DISPLACEMENT(v)

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

                float terrainDistance = length(v.viewDir);
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, v.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, v.worldNormal);

                // Defines 'displacedWorldPos' 
                CALCULATE_VERTEX_DISPLACEMENT(v)
                //TRANSFER_SHADOW(o);
                v.pos = UnityWorldToClipPos(displacedWorldPos);
                
                TRANSFER_VERTEX_TO_FRAGMENT(v);

                return v;
            }
            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                i.worldNormal = normalize(i.worldNormal);
                i.lightDir = normalize(i.lightDir);
                // Calculate scaling params
                float terrainDistance = length(i.viewDir);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                float3 viewDir = normalize(i.viewDir);

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                fixed4 col = SampleBiplanarTexture(_MainTex, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);
                float3 normal = SampleBiplanarNormal(_BumpMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

                float atten = LIGHT_ATTENUATION(i);
                //UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos.xyz);

                float3 result = CalculateLighting(col, normal, viewDir, 1) * atten;
                //return atten;
                return float4(result, atten);
            }
            ENDCG
        }
    }
    //FallBack "VertexLit"
}
    