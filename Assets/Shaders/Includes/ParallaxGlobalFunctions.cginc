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

#if defined (EMISSION) && defined (DIRECTIONAL)
    #define APPLY_EMISSION  result.rgb = result.rgb += _EmissionColor * (1 - finalNormal.a);
#else
    #define APPLY_EMISSION
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
    float3 reflection = fresnel * reflColor * col.a * _EnvironmentMapFactor + (1 - fresnel) * refrColor * _RefractionIntensity * NdotL; // For refraction
    reflection *= shadow + UNITY_LIGHTMODEL_AMBIENT;
    float3 scattering = 0;
    
    #if defined (SUBSURFACE_SCATTERING) || defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
        scattering = SubsurfaceScattering(worldPos, worldNormal, viewDir, lightDir);
        #if defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
            scattering *= lerp(_SubsurfaceMax, _SubsurfaceMin, thickness);
        #endif
    #endif
    
    return diffuse + ambient + specular + reflection + scattering;
}