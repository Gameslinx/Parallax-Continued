Shader "Custom/DiscardAll"
{
    // Invisible shader
    // Until I find a way to get quad mesh renderers to turn off without breaking the entire game, we're using this stupid thing
    Properties
    {

    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
        LOD 200
        ZWrite Off
        Cull Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };
            
            v2f vert ()
            {
                v2f o;

                // move triangle off screen, create degen triangle, skipped by gpu
                o.vertex.xyz = float3(-10000, -10000, -10000);
                o.vertex.w = 1;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_TARGET
            {
                discard;
                return 0; 
            }
            ENDCG
        }
    }
}