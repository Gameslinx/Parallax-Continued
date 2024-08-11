// Upgrade NOTE: replaced 'unity_World2Shadow' with 'unity_WorldToShadow'

// Upgrade NOTE: replaced 'unity_World2Shadow' with 'unity_WorldToShadow'

//
//  Required Variables
//

// Lighting params
float _FresnelPower;
float _SpecularPower;
float _SpecularIntensity;
float _EnvironmentMapFactor;
float _RefractionIntensity;
float _RefractionEta;
float _Hapke;

//
//  Utility Functions
//

float3 SampleNormal(sampler2D tex, float2 uv)
{
    return tex2D(tex, uv) * 2 - 1;
}

float3 ToNormal(float4 tex)
{
    return tex.rgb * 2.0f - 1.0f;
}

float3 CombineNormals(float3 n1, float3 n2)
{
    return normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
}

//
//  Lighting Functions
//

#define eta 0.7519
#if !defined (BILLBOARD) && !defined (BILLBOARD_USE_MESH_NORMALS)
    #define GET_SHADOW LIGHT_ATTENUATION(i)
#else
    #define GET_SHADOW 1
#endif

float FresnelEffect(float3 worldNormal, float3 viewDir, float power)
{
    return pow((1.0 - saturate(dot(worldNormal, viewDir))), power);
}

// We get the reflection color in the directional pass anyway
#if !defined (DONT_SAMPLE_REFLECTIONS)
    #if defined (DIRECTIONAL)
        #define GET_REFLECTION_COLOR                                                    \
            float3 reflDir = reflect(-viewDir, worldNormal);                            \
            float4 reflSkyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflDir);        \
            float3 reflColor = DecodeHDR(reflSkyData, unity_SpecCube0_HDR);             
    
        #define GET_REFRACTION_COLOR                                                    \
            float3 refrDir = refract(-viewDir, worldNormal, eta);                       \
            float4 refrSkyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, refrDir);        \
            float3 refrColor = DecodeHDR(refrSkyData, unity_SpecCube0_HDR);             
    #else
        #define GET_REFLECTION_COLOR                                                    \
            float3 reflColor = 0;
    
        #define GET_REFRACTION_COLOR                                                    \
            float3 refrColor = 0;
    #endif
#else
    #if defined (DIRECTIONAL)
        #define GET_REFLECTION_COLOR float3 reflColor = _FresnelColor * NdotL;
    #else
        #define GET_REFLECTION_COLOR float3 reflColor = 0;
    #endif

    #if defined (REFRACTION)
        #define GET_REFRACTION_COLOR float3 refrColor = texCUBE(_RefractionTexture, refract(-viewDir, worldNormal, _RefractionEta)) * refractionIntensity;
    #else
        #define GET_REFRACTION_COLOR float3 refrColor = 0;
    #endif
#endif

#if defined (SUBSURFACE_SCATTERING) || defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
float3 SubsurfaceScattering(float3 worldPos, float3 worldNormal, float3 viewDir, float3 lightDir)
{
    float3 NplusL = -normalize(worldNormal * _SubsurfaceNormalInfluence + lightDir);
    float VdotNpL = max(0, (dot(NplusL, viewDir)));
    float ss = pow(VdotNpL, _SubsurfacePower) * _SubsurfaceIntensity;
    
    float generalLightIntensity = max(0, dot(normalize(worldPos - _PlanetOrigin), lightDir));
    // Mult by thickness map
    
    return ss * _SubsurfaceColor * generalLightIntensity;
}
#endif

// Basic lighting parameters
#define BASIC_LIGHTING_INPUT float4 col, float3 worldNormal, float3 viewDir, float shadow, float3 lightDir

// Subsurface macro
#define SUBSURFACE_SCATTERING_INPUT float3 worldPos, float thickness

// Refraction parameters
#define REFRACTION_INPUT float refractionIntensity

// Macro to combine parameter sets
#if defined(SUBSURFACE_SCATTERING) || defined(SUBSURFACE_USE_THICKNESS_TEXTURE)
    #if defined(REFRACTION)
        #define LIGHTING_INPUT BASIC_LIGHTING_INPUT, SUBSURFACE_SCATTERING_INPUT, REFRACTION_INPUT
    #else
        #define LIGHTING_INPUT BASIC_LIGHTING_INPUT, SUBSURFACE_SCATTERING_INPUT
    #endif
#else
    #if defined(REFRACTION)
        #define LIGHTING_INPUT BASIC_LIGHTING_INPUT, REFRACTION_INPUT
    #else
        #define LIGHTING_INPUT BASIC_LIGHTING_INPUT
    #endif
#endif

#if defined (EMISSION) && defined (DIRECTIONAL) && !defined (PARALLAX_DEFERRED_PASS)
    #define APPLY_EMISSION  result.rgb = result.rgb += _EmissionColor * (1 - finalNormal.a);
#else
    #define APPLY_EMISSION
#endif

#if defined (EMISSION) && defined (PARALLAX_DEFERRED_PASS)
    #define GET_EMISSION _EmissionColor * (1 - finalNormal.a)
#else
    #define GET_EMISSION 0
#endif

float3 CalculateLighting(LIGHTING_INPUT)
{
	// Main light
    float NdotL = saturate(dot(worldNormal, lightDir)) * shadow;
    NdotL = pow(NdotL, _Hapke);
    float3 H = normalize(lightDir + viewDir);
    float NdotH = saturate(dot(worldNormal, H));
    
	// Fresnel reflections
    GET_REFLECTION_COLOR
    GET_REFRACTION_COLOR
    float fresnel = FresnelEffect(worldNormal, viewDir, _FresnelPower);

    float spec = pow(NdotH, _SpecularPower) * _LightColor0.rgb * _SpecularIntensity * col.a * shadow;

    float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * col.rgb;
    float3 diffuse = _LightColor0.rgb * col.rgb * NdotL;
    float3 specular = spec * _LightColor0.rgb;
    float3 reflection = fresnel * reflColor * col.a * _EnvironmentMapFactor; // For refraction
    float3 refraction = (1 - fresnel) * refrColor * _RefractionIntensity;
    reflection *= shadow + UNITY_LIGHTMODEL_AMBIENT;
    float3 scattering = 0;
    
    #if defined (SUBSURFACE_SCATTERING) || defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
        scattering = SubsurfaceScattering(worldPos, worldNormal, viewDir, lightDir);
        #if defined (REFRACTION)
            scattering *= refraction;
        #endif
        #if defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
            scattering *= lerp(_SubsurfaceMax, _SubsurfaceMin, thickness);
        #endif
    #endif
    
    // We'll store refraction and subsurface scattering in the emission
    #if !defined (PARALLAX_DEFERRED_PASS)
    return diffuse + ambient + specular + reflection + refraction * NdotL + scattering;
    #else
    return refraction * saturate(NdotL * 7) + scattering;
    #endif
}

//
//  Helper Functions
//

// More efficient version of unity's UnityClipSpaceShadowCasterPos which avoids re-transforming to world space
float4 ParallaxClipSpaceShadowCasterPos(float3 wPos, float3 wNormal)
{
    if (unity_LightShadowBias.z != 0.0)
    {
        float3 wLight = normalize(UnityWorldSpaceLightDir(wPos.xyz));

        // apply normal offset bias (inset position along the normal)
        // bias needs to be scaled by sine between normal and light direction
        // (http://the-witness.net/news/2013/09/shadow-mapping-summary-part-1/)
        //
        // unity_LightShadowBias.z contains user-specified normal offset amount
        // scaled by world space texel size.

        float shadowCos = dot(wNormal, wLight);
        float shadowSine = sqrt(1 - shadowCos * shadowCos);
        float normalBias = unity_LightShadowBias.z * shadowSine;

        wPos.xyz -= wNormal * normalBias;
    }

    return mul(UNITY_MATRIX_VP, float4(wPos, 1));
}

// Light shadow casters

#ifdef SHADOWS_CUBE
    #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = worldPos.xyz - _LightPositionRange.xyz;
#endif

#if defined (SHADOWS_DEPTH) && defined (SPOT)
    #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = mul (unity_WorldToShadow[0], float4(worldPos, 1));
#endif

#if defined (SHADOWS_SCREEN)
    #if defined (UNITY_NO_SCREENSPACE_SHADOWS)
        #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = mul( unity_WorldToShadow[0], float4(worldPos, 1) );
    #else
        #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = ComputeScreenPos(a.pos);
    #endif
#endif

// No shadows
#if !defined (SHADOWS_SCREEN) && !defined (SHADOWS_DEPTH) && !defined (SHADOWS_CUBE)
    #define PARALLAX_TRANSFER_SHADOW(a)
#endif

// Fix unity's pointless re-transform to world space which doesn't work for custom o2w matrices anyway
// These are all identical however any additional functionality can go here in the future if required
#ifdef SPOT
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1));     PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef POINT
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef POINT_COOKIE
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef DIRECTIONAL_COOKIE
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef DIRECTIONAL
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) PARALLAX_TRANSFER_SHADOW(a)
#endif

//
// Deferred Functions
//

#if defined (PARALLAX_DEFERRED_PASS)

#define PARALLAX_DEFERRED_OUTPUT_BUFFERS out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3

#define blinnPhongShininessPower 0.215
#define ONE_OVER_PI 0.31830989161

// From blackrack's BlinnPhong conversion
void GetStandardSpecularPropertiesFromLegacy(float legacyShininess, float specularMap, float3 legacySpecularColor, out float smoothness, out float3 specular)
{
    legacySpecularColor = saturate(legacySpecularColor);
    
    smoothness = pow(legacyShininess, blinnPhongShininessPower) * specularMap;
    smoothness *= sqrt(length(legacySpecularColor));

    specular = legacySpecularColor * ONE_OVER_PI;
}

half4 LightingStandardSpecular_Deferred_Corrected(SurfaceOutputStandardSpecular s, float3 viewDir, UnityGI gi, out half4 outGBuffer0, out half4 outGBuffer1, out half4 outGBuffer2)
{
    // energy conservation
    half oneMinusReflectivity;
    s.Albedo = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, oneMinusReflectivity);

    UnityStandardData data;
    data.diffuseColor = s.Albedo;
    data.occlusion = s.Occlusion;
    data.specularColor = s.Specular;
    data.smoothness = s.Smoothness;
    data.normalWorld = s.Normal;

    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

    half4 emission = half4(s.Emission, 1);
    return emission;
}

#ifndef UNITY_HDR_ON
#define SET_OUT_EMISSION(emissionColor) outEmission = float4(exp2(-emissionColor.rgb), 0); 
#else
#define SET_OUT_EMISSION(emissionColor) outEmission = float4(emissionColor.rgb, 0);
#endif

SurfaceOutputStandardSpecular GetPBRStruct(float4 albedo, float3 emission, float3 normal, float3 worldPos)
{
    float smoothness = 0;
    float3 specular = 0;
    GetStandardSpecularPropertiesFromLegacy(_SpecularPower, albedo.a, saturate(_SpecularIntensity), smoothness, specular);
    SurfaceOutputStandardSpecular o; 
	o.Albedo = albedo.rgb; 
	o.Specular = specular; 
	o.Normal = normal.xyz;   
	o.Emission = emission;
	o.Smoothness = smoothness;
	o.Occlusion = 1;
	o.Alpha = 1;
    
    return o;
}
UnityGI GetUnityGI()
{
    UnityGI gi;
    gi.indirect.diffuse = 0;
    gi.indirect.specular = 0;
    gi.light.color = 0;
    gi.light.dir = half3(0, 1, 0);
    gi.light.ndotl = 0;
    return gi;
}
UnityGIInput GetGIInput(float3 worldPos, float3 viewDir)
{
    UnityGIInput giInput;
    giInput.light.color = 0;
    giInput.light.dir = half3(0, 1, 0);
    giInput.light.ndotl = 0;
    giInput.worldPos = worldPos;
    giInput.worldViewDir = viewDir;
    giInput.atten = 1;
    giInput.lightmapUV = 0.0f;
    giInput.ambient.rgb = 0;

    giInput.probeHDR[0] = unity_SpecCube0_HDR;
    giInput.probeHDR[1] = unity_SpecCube1_HDR;

    #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
    giInput.boxMin[0] = unity_SpecCube0_BoxMin;
    #endif

    #ifdef UNITY_SPECCUBE_BOX_PROJECTION
    giInput.boxMax[0] = unity_SpecCube0_BoxMax; \
    giInput.probePosition[0] = unity_SpecCube0_ProbePosition; \
    giInput.boxMax[1] = unity_SpecCube1_BoxMax; \
    giInput.boxMin[1] = unity_SpecCube1_BoxMin; \
    giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
    #endif

    return giInput;
    
}

#define OUTPUT_GBUFFERS(surfaceOutput, gi) \
    float3 emissionColor = LightingStandardSpecular_Deferred_Corrected(surfaceOutput, viewDir, gi, outGBuffer0, outGBuffer1, outGBuffer2);  \
    SET_OUT_EMISSION(emissionColor)

#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
    #define SET_OUT_SHADOWMASK outShadowMask(i) = UnityGetRawBakedOcclusions(i.lmap.xy, i.worldPos);
#else
    #define SET_OUT_SHADOWMASK(i)
#endif

#endif