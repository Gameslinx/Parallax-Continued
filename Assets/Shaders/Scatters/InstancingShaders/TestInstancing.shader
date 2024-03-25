﻿Shader "Custom/TestInstancingShader" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque"}
        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            #include "../ScatterStructs.cginc"
            #include "ParallaxScatterStructs.cginc"
            #include "ParallaxScatterParams.cginc"
            #include "ParallaxScatterUtils.cginc"

            DECLARE_INSTANCING_DATA
            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F

            v2f vert(appdata i, uint instanceID : SV_InstanceID) {
                v2f o;

                float4x4 objectToWorld = INSTANCE_DATA.objectToWorld;
                float3 worldPos = mul(objectToWorld, i.vertex);

                o.pos = UnityWorldToClipPos(worldPos);
                o.worldPos = worldPos;
                o.uv = i.uv;
                o.color = 1;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                return float4(i.color, 1);
            }

            ENDCG
        }
    }
    Fallback "Cutout"
}