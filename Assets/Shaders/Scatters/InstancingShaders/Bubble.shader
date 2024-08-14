Shader "Custom/Bubble" 
{
    //
    //  Cheap bubble shader that approximates the thin film effect - Credit to Alan Zucconi for the implementation
    //  https://www.alanzucconi.com/2017/10/27/carpaint-shader-thin-film-interference/
    //

    Properties
    {
        _BumpMap("Bump Map",2D) = "bump"{}
        _ReflectionMap("Reflection Map", CUBE) = "black" {}
        _Tiling("Tiling", Range(0, 10)) = 1

        _SpecularPower("Specular Power", Range(0.0001, 1000)) = 10
        _SpecularIntensity("Specular Intensity", Range(0, 10)) = 1
        _FresnelColor("Fresnel Color", COLOR) = (0,0,0)

        // In nanometers
        _N1("Refractive Index Medium 1", Range(0.0001, 10)) = 1
        _N2("Refractive Index Film", Range(0.0001, 10)) = 1.5
        _N3("Refractive Index Medium 2", Range(0.0001, 10)) = 1.33
        _DiffractionDistance("DiffractionDistance", Range(0, 5000)) = 1600
        _Order("Order", int) = 8

        _YOffsetMin("Y Offset Min", Range(-10, 10)) = 0
        _YOffsetMax("Y Offset Max", Range(0, 20)) = 1
        _DisplacementScale("Displacement Scale", Range(0, 1)) = 1
        _BobNoiseScale("Bob Noise Scale", Range(0, 0.1)) = 1
        _DisplacementNoiseScale("Displacement Noise Scale", Range(0, 10.0)) = 1
        _BobTimeScale("Bobbing Time Scale", Range(0.01, 10)) = 1
        _DisplacementTimeScale("Displacement Time Scale", Range(0.01, 10)) = 1
    }
    SubShader
    {
        ZWrite Off
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "False" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Max
            Cull Off
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM

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
            #include "../SimplexNoise.cginc"

            // The necessary structs
            DECLARE_INSTANCING_DATA

            PARALLAX_FORWARDBASE_STRUCT_APPDATA
            PARALLAX_FORWARDBASE_STRUCT_V2F

            samplerCUBE _ReflectionMap;

            // Film and texture params

            float _N1;
            float _N2;
            float _N3;
            float _DiffractionDistance;
            int _Order;
            float _Tiling;

            // Displacement Params
            float _YOffsetMin;
            float _YOffsetMax;
            float _DisplacementScale;
            float _BobNoiseScale;
            float _DisplacementNoiseScale;
            float _BobTimeScale;
            float _DisplacementTimeScale;

            v2f vert(appdata i, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float4x4 objectToWorld = INSTANCE_DATA.objectToWorld;
                DECODE_INSTANCE_DATA(objectToWorld, color)
                
                o.worldNormal = normalize(mul(objectToWorld, float4(i.normal, 0)).xyz);
                o.worldTangent = normalize(mul(objectToWorld, float4(i.tangent.xyz, 0)));
                o.worldBinormal = cross(o.worldTangent, o.worldNormal) * i.tangent.w;

                float3 worldPos = mul(objectToWorld, float4(i.vertex.xyz, 1)).xyz;
                float3 planetNormal = normalize(worldPos - _PlanetOrigin);

                //
                //  Displacement
                //

                float displacementTime = (_Time.x * _DisplacementTimeScale);
                float displacementScale = snoise((worldPos + _TerrainShaderOffset) * _DisplacementNoiseScale + displacementTime) * _DisplacementScale;
                float3 displacement = o.worldNormal * displacementScale;
                worldPos += displacement;

                //
                //  Bobbing Effect
                //

                float initialOffset = snoise((worldPos + _TerrainShaderOffset) * _BobNoiseScale) * 0.5f + 0.5f;
                float bobMinMaxLerp = (sin(_Time.x * _BobTimeScale + initialOffset * 2 * PI)).x * 0.5 + 0.5;
                float3 yOffset = lerp(_YOffsetMin, _YOffsetMax, bobMinMaxLerp) * planetNormal;
                worldPos += yOffset;

                o.worldPos = worldPos;
                o.uv = i.uv;
                o.color = color;

                o.planetNormal = planetNormal;
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                o.pos = UnityWorldToClipPos(worldPos);

                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }

            fixed4 frag(PIXEL_SHADER_INPUT(v2f), float facing : VFACE) : SV_Target
            {
                float3 normal = UnpackNormal(tex2D(_BumpMap, i.uv * _Tiling));
                i.worldNormal = normalize(i.worldNormal) * facing;

                // Normal map only used for bubble film effect
                float3 worldNormal = normalize(i.worldNormal);
                float3 worldTangent = normalize(i.worldTangent);
                float3 worldBinormal = normalize(i.worldBinormal);

                float3x3 TBN = float3x3(worldTangent, worldBinormal, worldNormal);
                TBN = transpose(TBN);

                worldNormal = normalize(mul(TBN, normal));

                // Lighting params
                float3 L = _WorldSpaceLightPos0;
                float3 V = normalize(i.viewDir);
                float3 N = worldNormal;
                float3 T = worldTangent;
                float3 H = normalize(L + V);
                float d = _DiffractionDistance;

                // Calculate the thin film effect, with a slight variation from the original implementation that (incorrectly) takes into account the view direction
                // Credit to Alan Zucconi for the original implementation
                float cos_ThetaL = dot(normalize(N + V), L);
                float thetaL = acos(cos_ThetaL);

                float sin_thetaR = (_N1 / _N2) * sin(thetaL);
                float thetaR = asin(sin_thetaR);

                float u = _N2 * 2 * d * abs(cos(thetaR));

                // Shift
                float shift = 0;
                if ((_N1 < _N2) != (_N2 < _N3))
                {
                    shift += 0.5;
                }

                // Calculate film colour
                fixed3 color = 0;
                for (int n = 1; n <= _Order; n++)
                {
                    float wavelength = u / (n + shift);
                    color += spectral_zucconi6(wavelength);
                }
                color = saturate(color);

                // We don't have access to shadows so we must approximate the sun going down based off of where it is with respect to the planet
                float sunIntensity = saturate(dot(i.planetNormal, L));
                sunIntensity = pow(sunIntensity, 0.25);
                color *= sunIntensity;

                // Reflectance coefficient
                float R0 = (_N1 - _N2) / (_N1 + _N2);
                R0 *= R0;

                // Correct fresnel
                float trueFresnelReflectance = R0 + (1 - R0) * pow((1 - dot(V, i.worldNormal)), 5.0);
                
                // Fudge the fresnel a bit by sqrting it
                float fresnelReflectance = pow(trueFresnelReflectance, 0.5);
                float transmission = 1 - fresnelReflectance;

                //
                //  Disclaimer - Using local normal in the reflection is NOT correct
                //               but KSP up is the planet normal, not (0, 1, 0), so this approximation is fast and easy and works most of the time for static envmaps
                //

                float3 reflColor = _FresnelColor * fresnelReflectance * sunIntensity;

                // Specular
                float spec = pow(saturate(dot(i.worldNormal, H)), _SpecularPower) * _SpecularIntensity * _LightColor0 * sunIntensity;
                if (facing < 0)
                {
                    spec *= 0.5;
                }

                float res = saturate(dot(i.worldNormal, _WorldSpaceLightPos0));
                //return float4(res, res, res, 1);

                return float4(color * fresnelReflectance + reflColor + spec, fresnelReflectance);
            }

            ENDCG
        }
    }
}