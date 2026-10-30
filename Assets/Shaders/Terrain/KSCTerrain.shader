Shader "Custom/ParallaxKSCTerrain"
{
    // Parallax terrain where the blend mask is black stock tarmac where it is white
    // No tessellation because structure ground topology is shite
    Properties
    {
        [Header(Tarmac)]
        [Space(10)]
        _TarmacTexture("Tarmac Texture", 2D) = "gray" {}
        _TarmacNormal("Tarmac Normal", 2D) = "bump" {}
        _TarmacColor("Tarmac Color", Color) = (1, 1, 1, 1)
        _BlendMaskTexture("Blend Mask", 2D) = "black" {}
        _TerrainColor("Terrain Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _BlendMaskUsesUV0("Blend Mask Uses UV0", Float) = 0
        [HideInInspector] _RimColor("Rim Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _RimFalloff("Rim Falloff", Range(0.01, 5)) = 0.1

        // Unused, but KK sets mainTexture on its grass materials
        [HideInInspector] _MainTex("Main Tex", 2D) = "white" {}

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

        [Space(10)]
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
        _Tiling("Texture Tiling", Range(0.001, 0.2)) = 0.2
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
        _Hapke("Hapke", Range(0.001, 2)) = 1
        _BumpScale("Bump Scale", Range(0.001, 2)) = 1
        _EmissionColor("Emission Color", COLOR) = (0,0,0)
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
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            #pragma multi_compile_local            PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _          INFLUENCE_MAPPING
            #pragma multi_compile_local _          ADVANCED_BLENDING
            #pragma multi_compile_local _          EMISSION
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #include "../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"
            #include "KSCTerrainUtils.cginc"

            #pragma multi_compile_fwdbase

            #pragma vertex Vertex_Shader
            #pragma fragment Frag_Shader

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Interpolators
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD2;
                LIGHTING_COORDS(3, 4)
                UNITY_FOG_COORDS(5)
            };

            Interpolators Vertex_Shader (appdata v)
            {
                Interpolators o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float4(v.uv0, GetBlendMaskUV(v.uv0, v.uv1));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {
                float terrainDistance = length(i.viewDir);
                float3 vertexColor = _TerrainColor.rgb;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float4 landMask = GetLandMask(i.worldPos, i.worldNormal);

                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                DECLARE_DISPLACEMENT_TEXTURE(displacement, _DisplacementMap)
                CALCULATE_ADVANCED_BLENDING_FACTORS(landMask, displacement)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                NORMAL_FLOAT altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 terrainDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                NORMAL_FLOAT terrainNormal = lerp(altitudeNormal, steepNormal, landMask.b);

                float tarmacBlend = GetTarmacBlend(i.uv.zw);
                fixed4 finalDiffuse = lerp(terrainDiffuse, GetTarmacDiffuse(i.uv.xy), tarmacBlend);
                NORMAL_FLOAT finalNormal = lerp(terrainNormal, GetTarmacNormal(i.uv.xy, i.worldPos, i.worldNormal), tarmacBlend);

                float3 result = CalculateLighting(finalDiffuse, finalNormal.xyz, viewDir, GET_SHADOW, _WorldSpaceLightPos0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                APPLY_EMISSION
                result += GetRimHighlight(viewDir, finalNormal.xyz);
                return float4(result, 1 - _PlanetOpacity);
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

            #pragma multi_compile_shadowcaster

            #define PARALLAX_SHADOW_CASTER_PASS

            #pragma vertex Vertex_Shader
            #pragma fragment Frag_Shader

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #include "../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct Interpolators
            {
                float4 pos : SV_POSITION;
            };

            Interpolators Vertex_Shader (appdata v)
            {
                Interpolators o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);

                o.pos = ParallaxClipSpaceShadowCasterPos(worldPos, worldNormal);
                o.pos = UnityApplyLinearShadowBias(o.pos);

                return o;
            }

            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        //
        //  Forward Add Pass
        //

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One
            CGPROGRAM

            #pragma multi_compile_local           PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _         INFLUENCE_MAPPING
            #pragma multi_compile_fog
            #pragma multi_compile_fwdadd_fullshadows

            #pragma vertex Vertex_Shader
            #pragma fragment Frag_Shader

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #include "../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"
            #include "KSCTerrainUtils.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Interpolators
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD2;
                float3 lightDir : TEXCOORD3;
                LIGHTING_COORDS(4, 5)
            };

            Interpolators Vertex_Shader (appdata v)
            {
                Interpolators o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float4(v.uv0, GetBlendMaskUV(v.uv0, v.uv1));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.lightDir = UnityWorldSpaceLightDir(o.worldPos);

                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }

            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {
                float terrainDistance = length(i.viewDir);
                float3 vertexColor = _TerrainColor.rgb;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(i.lightDir);

                float4 landMask = GetLandMask(i.worldPos, i.worldNormal);

                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                float3 altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 terrainDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                float3 terrainNormal = lerp(altitudeNormal, steepNormal, landMask.b);

                float tarmacBlend = GetTarmacBlend(i.uv.zw);
                fixed4 finalDiffuse = lerp(terrainDiffuse, GetTarmacDiffuse(i.uv.xy), tarmacBlend);
                float3 finalNormal = lerp(terrainNormal, GetTarmacNormal(i.uv.xy, i.worldPos, i.worldNormal).xyz, tarmacBlend);

                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, lightDir);
                return float4(result, 1 - _PlanetOpacity);
            }
            ENDCG
        }

        //
        //  Deferred Pass
        //

        Pass
        {
            Tags { "LightMode" = "Deferred" }

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
            }

            CGPROGRAM

            #define PARALLAX_DEFERRED_PASS

            #pragma multi_compile_local            PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_local _          INFLUENCE_MAPPING
            #pragma multi_compile_local _          EMISSION
            #pragma multi_compile_local _          ADVANCED_BLENDING
            #pragma multi_compile_local _          AMBIENT_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile _ UNITY_HDR_ON

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityPBSLighting.cginc"

            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxUtils.cginc"
            #include "KSCTerrainUtils.cginc"

            #pragma vertex Vertex_Shader
            #pragma fragment Frag_Shader

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Interpolators
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD2;
                LIGHTING_COORDS(3, 4)
                UNITY_FOG_COORDS(5)
            };

            Interpolators Vertex_Shader (appdata v)
            {
                Interpolators o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float4(v.uv0, GetBlendMaskUV(v.uv0, v.uv1));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            void Frag_Shader (Interpolators i, PARALLAX_DEFERRED_OUTPUT_BUFFERS)
            {
                float terrainDistance = length(i.viewDir);
                float3 vertexColor = _TerrainColor.rgb;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float4 landMask = GetLandMask(i.worldPos, i.worldNormal);

                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_MID_TEXTURE_SET(midDiffuse, midNormal, _MainTexMid, _BumpMapMid)
                DECLARE_HIGH_TEXTURE_SET(highDiffuse, highNormal, _MainTexHigh, _BumpMapHigh)
                DECLARE_STEEP_TEXTURE_SET(steepDiffuse, steepNormal, _MainTexSteep, _BumpMapSteep)

                DECLARE_AMBIENT_OCCLUSION_TEXTURE(occlusion, _OcclusionMap)

                DECLARE_DISPLACEMENT_TEXTURE(displacement, _DisplacementMap)
                CALCULATE_ADVANCED_BLENDING_FACTORS(landMask, displacement)

                fixed4 altitudeDiffuse = BLEND_TEXTURES(landMask, lowDiffuse, midDiffuse, highDiffuse);
                NORMAL_FLOAT altitudeNormal = BLEND_TEXTURES(landMask, lowNormal, midNormal, highNormal);

                fixed4 terrainDiffuse = lerp(altitudeDiffuse, steepDiffuse, landMask.b);
                NORMAL_FLOAT terrainNormal = lerp(altitudeNormal, steepNormal, landMask.b);

                float tarmacBlend = GetTarmacBlend(i.uv.zw);
                fixed4 finalDiffuse = lerp(terrainDiffuse, GetTarmacDiffuse(i.uv.xy), tarmacBlend);
                NORMAL_FLOAT finalNormal = lerp(terrainNormal, GetTarmacNormal(i.uv.xy, i.worldPos, i.worldNormal), tarmacBlend);

                BLEND_OCCLUSION(landMask, occlusion)

                #if defined (AMBIENT_OCCLUSION)
                finalOcclusion = lerp(finalOcclusion, 1, tarmacBlend);
                #endif

                SurfaceOutputStandardSpecular surfaceInput = GetPBRStruct(finalDiffuse, GET_EMISSION + GetRimHighlight(viewDir, finalNormal.xyz), finalNormal.xyz, i.worldPos ADDITIONAL_PBR_PARAMS);
                UnityGI gi = GetUnityGI();
                UnityGIInput giInput = GetGIInput(i.worldPos, viewDir);
                LightingStandardSpecular_GI(surfaceInput, giInput, gi);

                OUTPUT_GBUFFERS(surfaceInput, gi)
                SET_OUT_SHADOWMASK(i)
            }
            ENDCG
        }
    }
}
