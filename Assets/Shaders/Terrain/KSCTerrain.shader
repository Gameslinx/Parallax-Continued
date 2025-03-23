Shader "Custom/ParallaxKSCTerrain"
{
    // Basic parallax shader for the KSC grass
    // No tessellation because the KSC has some of the worst topology I've ever had to witness, that shit is ass
    Properties
    {
        _TarmacTexture("Tarmac Texture", 2D) = "gray" {}
        _BlendMaskTexture("Blend Mask", 2D) = "gray" {}

        // Underlying terrain colour - Used in influence mapping
        _GrassColor("Terrain Color", Color) = (1.0, 1.0, 1.0, 1.0)

        // Keep for parity sake
        _TarmacColor("Ground Color", Color) = (1.0, 1.0, 1.0, 1.0)

        // Parallax Textures
        _MainTexLow("Main Tex", 2D) = "white" {}
        _BumpMapLow("Bump Map", 2D) = "bump" {}
        _InfluenceMap("Influence Map", 2D) = "white" {}
        _OcclusionMap("Occlusion Map", 2D) = "white" {}

        _Tiling("Texture Tiling", Range(0.001, 0.2)) = 0.2
        _BiplanarBlendFactor("Biplanar Blend Factor", Range(0.01, 8)) = 1

        // Lighting
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1
        _Hapke("Hapke", Range(0.001, 2)) = 1
        _BumpScale("Bump Scale", Range(0.001, 2)) = 1

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            #define PARALLAX_SINGLE_LOW
            #define INFLUENCE_MAPPING

            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 worldPos : TEXCOORD3;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD4;
                float3 localPos : TEXCOORD5;

                LIGHTING_COORDS(6, 7)
                UNITY_FOG_COORDS(8)
            };

            sampler2D _NearGrassTexture;
            sampler2D _BlendMaskTexture;
            sampler2D _TarmacTexture;

            float4 _GrassColor;
            float4 _TarmacColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;

                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.localPos = v.vertex.xyz;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Blend mask 1 = tarmac, 0 = terrain
                float blendMask = tex2D(_BlendMaskTexture, i.uv2);
                
                float terrainDistance = length(i.viewDir);

                // Maybe gamma correct at some point
                float3 vertexColor = _GrassColor.xyz;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                float4 tarmacColor = tex2D(_TarmacTexture, i.localPos.xz / 3.0f);
                float3 tarmacNormal = i.worldNormal;

                fixed4 finalDiffuse = lerp(lowDiffuse, tarmacColor, blendMask);
                NORMAL_FLOAT finalNormal = lerp(lowNormal, tarmacNormal, blendMask);

                float3 result = CalculateLighting(finalDiffuse, finalNormal.xyz, viewDir, GET_SHADOW, _WorldSpaceLightPos0);
                UNITY_APPLY_FOG(i.fogCoord, result);

                return float4(result, 1);
            }
            ENDCG
        }

        //
        //  Pass: Shadow Caster
        //

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
        
            #pragma multi_compile_local PARALLAX_SINGLE_LOW PARALLAX_SINGLE_MID PARALLAX_SINGLE_HIGH PARALLAX_DOUBLE_LOWMID PARALLAX_DOUBLE_MIDHIGH PARALLAX_FULL
            #pragma multi_compile_fog
            #pragma multi_compile_shadowcaster

            #define PARALLAX_SHADOW_CASTER_PASS
        
            #pragma vertex vert
            #pragma fragment frag
            
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
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                float3 worldPos : TEXCOORD0;
                float3 worldNormal : NORMAL;
                float3 normal : TEXCOORD1;
                float4 vertex : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
        
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.normal = v.normal;
                o.vertex = v.vertex;

                o.pos = ParallaxClipSpaceShadowCasterPos(o.worldPos, o.worldNormal);
                o.pos = UnityApplyLinearShadowBias(o.pos);

                return o;
            }
        
            fixed4 frag (v2f i) : SV_Target
            {   
                return 0;
            }
        
            ENDCG
        }

        //
        //  Pass: ForwardAdd
        //

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One

            CGPROGRAM

            #define PARALLAX_SINGLE_LOW
            #define INFLUENCE_MAPPING

            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"

            #pragma multi_compile_fwdadd_fullshadows

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 worldPos : TEXCOORD3;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD4;
                float3 lightDir : TEXCOORD5;
                float3 localPos : TEXCOORD6;

                LIGHTING_COORDS(7, 8)
            };

            sampler2D _NearGrassTexture;
            sampler2D _BlendMaskTexture;
            sampler2D _TarmacTexture;

            float4 _GrassColor;
            float4 _TarmacColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;

                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.lightDir = _WorldSpaceLightPos0 - o.worldPos;
                o.localPos = v.vertex.xyz;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Blend mask 1 = tarmac, 0 = terrain
                float blendMask = tex2D(_BlendMaskTexture, i.uv2);
                
                float terrainDistance = length(i.viewDir);

                // Maybe gamma correct at some point
                float3 vertexColor = _GrassColor.xyz;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(i.lightDir);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                float4 tarmacColor = tex2D(_TarmacTexture, i.localPos.xz / 3.0f);
                float3 tarmacNormal = i.worldNormal;

                fixed4 finalDiffuse = lerp(lowDiffuse, tarmacColor, blendMask);
                NORMAL_FLOAT finalNormal = lerp(lowNormal, tarmacNormal, blendMask);

                float atten = LIGHT_ATTENUATION(i);
                float3 result = CalculateLighting(finalDiffuse, finalNormal, viewDir, GET_SHADOW, lightDir);

                return float4(result, atten * (1 - _PlanetOpacity));
            }
            ENDCG
        }

        //
        // Pass: Deferred
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

            #define PARALLAX_SINGLE_LOW
            #define INFLUENCE_MAPPING

            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityPBSLighting.cginc"

            #include "../Includes/ParallaxGlobalFunctions.cginc" 
            #include "ParallaxStructs.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"
            
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile _             UNITY_HDR_ON
            #pragma multi_compile_local _       AMBIENT_OCCLUSION

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float3 worldPos : TEXCOORD3;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD4;
                float3 localPos : TEXCOORD5;

                LIGHTING_COORDS(6, 7)
                UNITY_FOG_COORDS(8)
            };

            sampler2D _NearGrassTexture;
            sampler2D _BlendMaskTexture;
            sampler2D _TarmacTexture;

            float4 _GrassColor;
            float4 _TarmacColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;

                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                o.localPos = v.vertex.xyz;

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            void frag (v2f i, PARALLAX_DEFERRED_OUTPUT_BUFFERS)
            {
                // Blend mask 1 = tarmac, 0 = terrain
                float blendMask = tex2D(_BlendMaskTexture, i.uv2);
                
                float terrainDistance = length(i.viewDir);

                // Maybe gamma correct at some point
                float3 vertexColor = _GrassColor.xyz;

                i.worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS(terrainDistance * 0.2, i.worldPos)
                
                // Get biplanar params for texture sampling
                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texScale0, texScale1);

                // Declares float4 'globalInfluence' if influence mapping is enabled
                DECLARE_INFLUENCE_TEXTURE
                DECLARE_INFLUENCE_VALUES

                DECLARE_LOW_TEXTURE_SET(lowDiffuse, lowNormal, _MainTexLow, _BumpMapLow)
                DECLARE_AMBIENT_OCCLUSION_TEXTURE(occlusion, _OcclusionMap)

                float4 tarmacColor = tex2D(_TarmacTexture, i.localPos.xz / 3.0f);
                float3 tarmacNormal = i.worldNormal;

                fixed4 finalDiffuse = lerp(lowDiffuse, tarmacColor, blendMask);
                NORMAL_FLOAT finalNormal = lerp(lowNormal, tarmacNormal, blendMask);
                
                #if defined (AMBIENT_OCCLUSION)
                
                float finalOcclusion = lerp(occlusion, 1, blendMask);

                #endif

                // Deferred functions
                SurfaceOutputStandardSpecular surfaceInput = GetPBRStruct(finalDiffuse, GET_EMISSION, finalNormal.xyz, i.worldPos ADDITIONAL_PBR_PARAMS);
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
