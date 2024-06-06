﻿Shader "Custom/ShowNoiseEditor" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _PlanetOrigin("Planet Origin", vector) = (0,0,0)

        _Frequency("Frequency", Range(0.0001, 10)) = 1
        _Persistence("Persistence", Range(0.001, 4)) = 0.5
        _Lacunarity("Lacunarity", Range(0.001, 8)) = 2
        _Octaves("Octaves", Range(1, 8)) = 2

        _NoiseMode("Noise Mode", Range(0, 2)) = 0
        _Inverse("Inverse", Range(0, 1)) = 0
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

            #include "../Includes/gpu_noise_lib.glsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 directions : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 directions : COLOR;
            };

            float _Frequency;
            float _Persistence;
            float _Lacunarity;
            int _Octaves;

            int _NoiseMode;
            int _Inverse;

            float3 _PlanetOrigin;

            v2f vert(appdata i)
            {
                v2f o;

                float4x4 objectToWorld = unity_ObjectToWorld;
                float3 worldPos = mul(objectToWorld, i.vertex);

                o.pos = UnityWorldToClipPos(worldPos);
                o.worldPos = worldPos;
                o.uv = i.uv;
                o.directions = i.directions;
                return o;
            }
            
            float GetNoise(float3 pos, int noiseMode)
            {
                if (noiseMode == 0)
                {
                    return SimplexPerlin3D(pos) * 1.5;
                }
                if (noiseMode == 1)
                {
                    return Cellular3D(pos) * 2;
                }
                if (noiseMode == 2)
                {
                    return SimplexPolkaDot3D(pos, 0.3, 1);
                }
                return 1;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                int b = 0;
                float3 dirFromCenter = normalize(i.worldPos - _PlanetOrigin);
                float noiseValue = 0;

                // Defaults to 1 on first run

                float frequency = _Frequency;
                float amplitude = 1;
                for (int i = 0; i < _Octaves; i++)
                {
                    float perOctaveOffset = 2; // Offset dir from center by entire phase
                    dirFromCenter += perOctaveOffset;

                    frequency *= _Lacunarity;
                    amplitude *= 0.5;
                    float newNoiseValue = GetNoise(dirFromCenter * frequency, _NoiseMode);
                    noiseValue += newNoiseValue * amplitude;
                }

                int inverse = _Inverse;
                if (inverse == 1)
                {
                    noiseValue = 1 - abs(noiseValue);
                }
                else
                {
                    noiseValue = noiseValue * 0.5f + 0.5f;
                }

                float debugNoise = SimplexPerlin3D(dirFromCenter);
                return noiseValue;
            }

            ENDCG
        }
    }
    Fallback "Cutout"
}