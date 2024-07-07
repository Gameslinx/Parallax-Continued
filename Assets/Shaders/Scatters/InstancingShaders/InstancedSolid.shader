//
//  InstancedSolid - Basic instancing shader with an albedo and normal
//

Shader "Custom/ParallaxInstancedSolid"
{
    Properties
    {
        // Texture params
        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _SpecularTexture("Alt Specular Map", 2D) = "black" {}
        _ThicknessMap("Subsurface Thickness Map", 2D) = "black" {}

        // Wind params
        [Space(10)]
        [Header(Wind Parameters)]
        [Space(10)]
        _WindMap("Wind Map", 2D) = "grey" {}

        _WindScale("Wind Scale", Range(0.00001, 0.1)) = 0.05
        _WindHeightStart("Wind Height Start", Range(0, 5)) = 0.05
        _WindHeightFactor("Wind Height Factor", Range(0.00001, 5)) = 0.05
        _WindSpeed("Wind Speed", Range(0, 15)) = 0.05
        _WindIntensity("Wind Intensity", Range(0, 1)) = 0.05

        // Lighting params
        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        _Color("Color", COLOR) = (1, 1, 1)
        _BumpScale("Bump Scale", Range(0, 2)) = 1
        [PowerSlider(3.0)] _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _FresnelColor("Fresnel Color", COLOR) = (0, 0, 0)
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1
        _Hapke("Hapke", Range(0.001, 2)) = 1

        // Subsurface
        [Space(10)]
        [Header(Subsurface Parameters)]
        [Space(10)]
        _SubsurfaceNormalInfluence("Subsurface Normal Influence", Range(0, 1)) = 0.5
        _SubsurfacePower("Subsurface Power", Range(0.001, 6)) = 2
        _SubsurfaceIntensity("Subsurface Intensity", Range(0, 3)) = 1
        _SubsurfaceColor("Subsurface Color", COLOR) = (1, 1, 1)
        _SubsurfaceMax("Subsurface Max", Range(0, 1)) = 1
        _SubsurfaceMin("Subsurface Min", Range(0, 1)) = 0

        // Other params
        [Space(10)]
        [Header(Other Parameters)]
        [Space(10)]
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0

        // 0 = cull off
        // 1 = cull frontfaces
        // 2 = cull backfaces
        _CullMode("Cull Mode", int) = 0
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque"}
        ZWrite On
        Cull [_CullMode]
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            // Shader variants

            #pragma multi_compile_fragment      _ TWO_SIDED
            
            #pragma multi_compile_fragment      _ ALPHA_CUTOFF
            #pragma multi_compile_fragment      _ ALTERNATE_SPECULAR_TEXTURE
            #pragma multi_compile_vertex        _ BILLBOARD                         BILLBOARD_USE_MESH_NORMALS
            #pragma multi_compile_fragment      _ DEBUG_FACE_ORIENTATION            DEBUG_SHOW_WIND_TEXTURE
            #pragma multi_compile_fragment      _ SUBSURFACE_SCATTERING             SUBSURFACE_USE_THICKNESS_TEXTURE
            #pragma multi_compile        _ WIND

            // Shader stages
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            // Unity includes
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            // Parallax includes
            #include "ParallaxScatterStructs.cginc"
            #include "ParallaxScatterParams.cginc"
            #include "../ScatterStructs.cginc"
            #include "../../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxScatterUtils.cginc"

            // The necessary structs
            DECLARE_INSTANCING_DATA

            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F
          
            //
            // Vertex Shader 
            //

            v2f vert(appdata i, uint instanceID : SV_InstanceID) 
            {
                v2f o;

                float4x4 objectToWorld = INSTANCE_DATA.objectToWorld;
                BILLBOARD_IF_ENABLED(i.vertex, i.normal, i.tangent, objectToWorld);

                o.worldNormal = mul(objectToWorld, float4(i.normal, 0)).xyz;
                o.worldTangent = mul(objectToWorld, float4(i.tangent.xyz, 0));
                o.worldBinormal = cross(o.worldTangent, o.worldNormal) * i.tangent.w;

                float3 worldPos = mul(objectToWorld, i.vertex);
                float3 planetNormal = normalize(worldPos - _PlanetOrigin);
                PROCESS_WIND

                o.worldPos = worldPos;
                o.uv = i.uv;

                o.planetNormal = planetNormal;
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                o.pos = UnityWorldToClipPos(worldPos);

                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }

            //
            //  Fragment Shader
            //

            fixed4 frag(PIXEL_SHADER_INPUT(v2f)) : SV_Target
            {   
                // Do as little work as possible, clip immediately
                float4 mainTex = tex2D(_MainTex, i.uv * _MainTex_ST);

                ALPHA_CLIP(mainTex.a);
                
                mainTex.rgb *= _Color;
                
                // Get specular from MainTex or, if ALTERNATE_SPECULAR_TEXTURE is defined, use the specular texture
                GET_SPECULAR(mainTex, i.uv * _MainTex_ST);

                // Get thickness for subsurface scattering if defined
                GET_THICKNESS(i.uv * _MainTex_ST);
                
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);
                
                CORRECT_TWOSIDED_WORLDNORMAL

                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = _WorldSpaceLightPos0;

                // Compute tangent to world matrix
                float3x3 TBN = float3x3(normalize(i.worldTangent), normalize(i.worldBinormal), normalize(i.worldNormal));
                TBN = transpose(TBN);
                
                float4 bumpMap = tex2D(_BumpMap, i.uv * _BumpMap_ST);

                float3 normal = normalize(UnpackScaleNormal(bumpMap, _BumpScale));
                float3 worldNormal = normalize(mul(TBN, normal));

                // Calculate lighting from core params, plus potential additional params (worldpos required for subsurface scattering)
                float3 result = CalculateLighting(
                                mainTex, worldNormal, viewDir, GET_SHADOW, lightDir
                                ADDITIONAL_LIGHTING_PARAMS(i.worldPos, THICKNESS)
                );

                // Process any enabled debug options that affect the output colour
                DEBUG_IF_ENABLED
                return float4(result, 1);
            }

            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
        
            #pragma multi_compile _ TWO_SIDED
            #pragma multi_compile _ WIND
            #pragma multi_compile _ ALPHA_CUTOFF
            #pragma multi_compile _ ALTERNATE_SPECULAR_TEXTURE
            #pragma multi_compile _ BILLBOARD
        
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
        
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
        
            #include "ParallaxScatterStructs.cginc"
            #include "ParallaxScatterParams.cginc"
            #include "../ScatterStructs.cginc"
            #include "../../Includes/ParallaxGlobalFunctions.cginc"
            #include "ParallaxScatterUtils.cginc"
        
            DECLARE_INSTANCING_DATA
        
            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F
          
            v2f vert(appdata i, uint instanceID : SV_InstanceID)
            {
                v2f o;
        
                float4x4 objectToWorld = INSTANCE_DATA.objectToWorld;
        
                BILLBOARD_IF_ENABLED(i.vertex, i.normal, i.tangent, objectToWorld);
        
                o.worldNormal = mul(objectToWorld, float4(i.normal, 0)).xyz;
                o.worldTangent = mul(objectToWorld, float4(i.tangent.xyz, 0));
                o.worldBinormal = cross(o.worldTangent, o.worldNormal) * i.tangent.w;
                
                float3 worldPos = mul(objectToWorld, i.vertex);
                float3 planetNormal = normalize(worldPos - _PlanetOrigin);
                PROCESS_WIND

                o.worldPos = worldPos;
                o.uv = i.uv;
        
                o.planetNormal = planetNormal;
                o.viewDir = _WorldSpaceCameraPos - worldPos;

                o.pos = UnityWorldToClipPos(worldPos);
                o.pos = UnityApplyLinearShadowBias(o.pos);
        
                return o;
            }
        
            void frag(PIXEL_SHADER_INPUT(v2f))
            {   
                // Remove this from build
                //i.uv.y = -i.uv.y;
                // Do as little work as possible, clip immediately
                float mainTex = tex2D(_MainTex, i.uv * _MainTex_ST).a;
                ALPHA_CLIP(mainTex);
            }
        
            ENDCG
        }
    }
}