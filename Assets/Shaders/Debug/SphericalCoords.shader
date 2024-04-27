//
//  InstancedSolid - Basic instancing shader with an albedo and normal
//

Shader "Custom/SphericalCoords"
{
    Properties
    {
        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}

        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        [PowerSlider(3.0)]
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _FresnelColor("Fresnel Color", COLOR) = (0, 0, 0)
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1

        [Space(10)]
        [Header(Other Parameters)]
        [Space(10)]
        _PlanetOrigin("Planet Origin", vector) = (0, 0, 0)
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque"}
        ZWrite On
        Cull Off
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM

            #pragma multi_compile _ TWO_SIDED

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #include "../Scatters/InstancingShaders/ParallaxScatterStructs.cginc"
            #include "../Scatters/InstancingShaders/ParallaxScatterParams.cginc"
            #include "../Scatters/ScatterStructs.cginc"
            #include "../Includes/ParallaxGlobalFunctions.cginc"
            #include "../Scatters/InstancingShaders/ParallaxScatterUtils.cginc"

            DECLARE_INSTANCING_DATA

            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F
          
            float2 WorldPosToUV(float3 worldPos) 
            {
                // Assuming your mesh is centered at the origin, calculate the spherical coordinates
                float radius = length(worldPos);
                float theta = atan2(worldPos.z, worldPos.x);
                float phi = acos(worldPos.y / radius);

                // Map spherical coordinates to UV coordinates
                float2 uv;
                uv.x = theta / (2 * 3.14159); // Normalize theta to [0, 1]
                uv.y = phi / 3.14159; // Normalize phi to [0, 1]

                return uv;
            }

            v2f vert(appdata i) 
            {
                v2f o;

                float4x4 objectToWorld = unity_ObjectToWorld;
                float3 worldPos = mul(objectToWorld, i.vertex);

                o.pos = UnityWorldToClipPos(worldPos);
                o.worldPos = worldPos;
                o.uv = i.uv;
                o.worldNormal = mul(objectToWorld, float4(i.normal, 0)).xyz;
                o.worldTangent = mul(objectToWorld, i.tangent);
                o.worldBinormal = mul(objectToWorld, cross(o.worldNormal, o.worldTangent)) * i.tangent.w;

                o.viewDir = _WorldSpaceCameraPos - worldPos;
                return o;
            }

            fixed4 frag(PIXEL_SHADER_INPUT(v2f)) : SV_Target
            {
                i.worldNormal = normalize(i.worldNormal);
                i.worldTangent = normalize(i.worldTangent);
                i.worldBinormal = normalize(i.worldBinormal);
                // Remove this from build
                i.uv.y = -i.uv.y;

                // Flip normal based on facing dir
                CORRECT_TWOSIDED_WORLDNORMAL

                return tex2D(_MainTex, WorldPosToUV(i.worldPos) * _MainTex_ST);
                return float4(WorldPosToUV(i.worldPos), 0, 1);
            }

            ENDCG
        }
    }
}