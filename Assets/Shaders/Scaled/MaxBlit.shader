Shader "Custom/MaxBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _KernelSize ("Kernel Size", int) = 1
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            int _KernelSize;

            float MaxFilter(int kernelSize, float2 uv)
            {
                float result = 0;
                for (int x = -kernelSize; x < kernelSize + 1; x++)
                {
                    for (int y = -kernelSize; y < kernelSize + 1; y++)
                    {
                        float heightHere = tex2D(_MainTex, uv + _MainTex_TexelSize.xy * uv * float2(x, y));
                        if (heightHere > result) 
                        {
                            result = heightHere;
                        }
                    }
                }
                return result;
            }

            float frag (v2f i) : SV_Target
            {
                
                float col = MaxFilter(_KernelSize, i.uv);

                return col;
            }
            ENDCG
        }
    }
}
