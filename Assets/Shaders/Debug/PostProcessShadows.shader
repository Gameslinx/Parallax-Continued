Shader "Unlit/PostProcessShadows"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        BlendOp Min
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _ShadowDistances;
            sampler2D _ShadowDepth;

            sampler2D _CameraDepthTexture; 
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float shadowDistance = tex2Dlod(_ShadowDistances, float4(i.uv, 0, 0)).r;

                float mip = min(shadowDistance * 32, 6);

                float screenSpaceShadowAttenuation = tex2Dlod(_MainTex, float4(i.uv, 0, mip -1 )).r;
                
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // Sample scene depth
                float sceneDepth = LinearEyeDepth(tex2D(_CameraDepthTexture, screenUV).r);
                
                // Sample shadow caster's depth from the shadow attenuation render texture's depth
                float shadowCasterDepth = LinearEyeDepth(tex2D(_ShadowDepth, screenUV).r);
                
                // Define a depth threshold to avoid z-fighting issues
                float depthThreshold = 0.005; // Tweak this value for better results
                
                if (abs(sceneDepth - shadowCasterDepth) > depthThreshold)
                {
                    // Discard shadow: return white (no shadow attenuation)
                    return fixed4(1.0, 1.0, 1.0, 1.0);
                }

                //return shadowCasterDepth;

                return screenSpaceShadowAttenuation;
            }
            ENDCG
        }
    }
}
