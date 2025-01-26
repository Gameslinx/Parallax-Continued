Shader "Unlit/VolumeShader"
{
    Properties
    {
        _MainTex ("SDF Texture", 3D) = "white" {}
        _Alpha ("Alpha", float) = 0.02
        _StepSize ("Step Size", float) = 0.01
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Maximum number of raymarching samples
            #define MAX_STEP_COUNT 64

            // Allowed floating point inaccuracy
            #define EPSILON 0.0001f

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPos : TEXCOORD0;
                float3 rayDirection : TEXCOORD1;
            };

            sampler3D _MainTex;
            float _Alpha;
            float _StepSize;

            v2f vert (appdata v)
            {
                v2f o;

                // Vertex in object space (cube bounds)
                o.objectPos = v.vertex.xyz;

                // Ray direction from camera to vertex
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.rayDirection = normalize(worldPos - _WorldSpaceCameraPos);

                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Initialize ray origin and direction
                float3 rayOrigin = i.objectPos;
                float3 rayDirection = mul((float3x3)unity_WorldToObject, i.rayDirection);

                // Accumulated color and alpha
                float4 color = float4(0, 0, 0, 0);
                float3 samplePos = rayOrigin;

                // Perform raymarching
                [unroll]
                for (int step = 0; step < MAX_STEP_COUNT; step++)
                {
                    // Check if the current sample position is inside the unit cube
                    if (all(abs(samplePos) <= 0.5f + EPSILON))
                    {
                        // Sample the SDF texture (remapped to [0, 1] texture space)
                        float sdfValue = tex3D(_MainTex, samplePos + 0.5f).r;

                        // Convert SDF value to color (adjust contrast or gradient mapping as needed)
                        float density = exp(-sdfValue * 0.6f); // Example mapping
                        float4 sampledColor = float4(density, density, density, density * _Alpha);

                        // Blend the sampled color with the accumulated color
                        color.rgb += (1.0 - color.a) * sampledColor.rgb * sampledColor.a;
                        color.a += (1.0 - color.a) * sampledColor.a;

                        if (density > 0.6)
                        {
                        return 1;
                        }
                        // Break if color is fully opaque
 
                    }

                    // Advance ray position
                    samplePos += rayDirection * _StepSize;
                }

                return color;
            }
            ENDCG
        }
    }
}