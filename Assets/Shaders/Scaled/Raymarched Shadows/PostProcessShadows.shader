Shader "Custom/PostProcessShadows"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        //BlendOp Min
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
            float4 _MainTex_TexelSize;

            // Gaussian kernel weights for a 5x5 blur

            static const int samples = 35, LOD = 3, sLOD = 1 << LOD;
            static const float sigma = float(samples) * 0.25;

            float gaussian(float2 i) 
            {
                return exp( -0.5* dot(i/=sigma,i) ) / ( 6.28 * sigma*sigma );
            }

            float4 blur(sampler2D sp, float2 U, float2 scale, float blurStrength) 
            {
                float4 O = 0;  
                int s = samples/sLOD;
                
                for ( int i = 0; i < s*s; i++ ) 
                {
                    float2 d = float2(i%s, i/s)*float(sLOD) - float(samples)/2.0;
                    O += gaussian(d) * tex2Dlod( sp, float4(U + scale * d * blurStrength, 0, float(LOD)) );
                }
                
                return O / O.a;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float GuassianBlur(float2 uv, float blurStrength)
            {
                return 1;
            }

            float boxBlur(float2 uv, float2 texelSize)
            {
                // blur x
                float res = 0;
                int kernelSize = 25;
                for (int i = -kernelSize; i < kernelSize; i++)
                {
                    for (int b = -kernelSize; b < kernelSize; b++)
                    {
                        res += tex2Dlod(_MainTex, float4(uv + texelSize * float2(i, b), 0, 0));
                    }
                }
                res /= (kernelSize * kernelSize);
                return res;
            }

            #define SHADOW_KERNEL_SPREAD 0.01
            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Blur kernel weight
                float shadowDistance = tex2Dlod(_ShadowDistances, float4(screenUV, 0, 0)).r;

                float screenSpaceShadowAttenuation = tex2Dlod(_MainTex, float4(i.uv, 0, 0)).r; //tex2Dlod(_MainTex, float4(i.uv, 0, mip -1 )).r;
                
                
                
                // Sample scene depth
                float sceneDepth = LinearEyeDepth(tex2D(_CameraDepthTexture, screenUV).r);
                
                // Sample shadow caster's depth from the shadow attenuation render texture's depth
                float shadowCasterDepth = LinearEyeDepth(tex2D(_ShadowDepth, screenUV).r);
                
                // Define a depth threshold to avoid z-fighting issues
                float depthThreshold = 0.01; // Tweak this value for better results
                
                if (abs(sceneDepth - shadowCasterDepth) > depthThreshold)
                {
                    // Discard shadow: return white (no shadow attenuation)
                    //return fixed4(1.0, 1.0, 1.0, 1.0);
                }

                //return shadowCasterDepth;
                //return screenSpaceShadowAttenuation;
                return tex2Dlod(_MainTex, float4(i.uv, 0, 0)).r;
            }
            ENDCG
        }
    }
}
