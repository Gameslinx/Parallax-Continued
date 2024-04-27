﻿Shader "Custom/TestShader" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _PlanetOrigin("Planet Origin", vector) = (0,0,0)
        _NoiseScale("Noise Scale", Range(0.1, 10)) = 1
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque"}
        ZWrite On
        Cull Back
        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #include "../ScatterStructs.cginc"
            #include "ParallaxScatterStructs.cginc"
            #include "ParallaxScatterParams.cginc"
            #include "ParallaxScatterUtils.cginc"
            #include "../SimplexNoise.cginc"
            #include "../../Includes/gpu_noise_lib.glsl"

            DECLARE_INSTANCING_DATA
            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F

            float _NoiseScale;

            v2f vert(appdata i)
            {
                v2f o;

                float4x4 objectToWorld = unity_ObjectToWorld;
                float3 worldPos = mul(objectToWorld, i.vertex);

                o.pos = UnityWorldToClipPos(worldPos);
                o.worldPos = worldPos;
                o.uv = i.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirFromCenter = normalize(i.worldPos - _PlanetOrigin);
                float noise = SimplexPerlin3D(dirFromCenter * _NoiseScale); //snoise(dirFromCenter * _NoiseScale);
                noise = 1 - abs(noise);
                return noise;
            }

            ENDCG
        }
    }
    Fallback "Cutout"
}