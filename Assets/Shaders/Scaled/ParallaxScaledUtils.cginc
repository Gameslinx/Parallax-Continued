
#include "ScaledDisplacementUtils.cginc"

// Calculate the heightmap displacement
// Take into account planet radius and terrain min/max value
// Assume normalised heightmap input (0 min, 1 max)
#if defined (OCEAN) || defined (OCEAN_FROM_COLORMAP)
    #define CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement)                                                                                                    \
        if (!_DisableDisplacement)                                                                                                                                      \
        {                                                                                                                                                               \
            float altitude = lerp(_MinRadialAltitude, _MaxRadialAltitude, displacement);                                                                                \
            altitude = max(altitude, _OceanAltitude);                                                                                                                   \
            o.worldPos = o.worldPos + o.worldNormal * altitude;                                                                                                         \
        }
#else
    #define CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement)                                                                                                    \
        if (!_DisableDisplacement)                                                                                                                                      \
        {                                                                                                                                                               \
            o.worldPos = o.worldPos + o.worldNormal * lerp(_MinRadialAltitude, _MaxRadialAltitude, displacement);                                                       \
        }
        
#endif


#define BuildTBN(tangent, binormal, normal)          \
    transpose(float3x3(tangent, binormal, normal));

//
//  Per vertex scaled mask functions
//

// Per vertex
float GetSlopeScaled(float3 worldPos, float3 worldNormal, float3 planetOrigin)
{
    // We abs in the very strange case of overhangs
    float slope = abs(dot(normalize(worldPos - planetOrigin), worldNormal));
    slope = pow(slope, _SteepPower);
    slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
    return 1 - slope;
}

// Per vertex
float4 GetScaledAltitudeMask(float3 worldPos, float3 worldNormal, float altitude, float midpoint, float3 planetOrigin)
{
    float lowMidBlend = GetPercentageAltitudeBetween(altitude, _LowMidBlendStart, _LowMidBlendEnd);
    float midHighBlend = GetPercentageAltitudeBetween(altitude, _MidHighBlendStart, _MidHighBlendEnd);
    float slope = GetSlopeScaled(worldPos, worldNormal, planetOrigin);

    // Land mask - Low-mid blend in red, mid-high blend in green, steep in blue
    return float4(lowMidBlend, midHighBlend, slope, midpoint);
}

// Per vertex
float4 GetScaledLandMask(float3 worldPos, float3 worldNormal)
{
    // Planet origin is delayed in scaled space, instead we know it's just the planet center (mesh origin) in world space
    const float3 planetOrigin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    
    float altitude = length(worldPos - planetOrigin);
    float midpoint = altitude / (_MidHighBlendStart + _LowMidBlendEnd);
    return GetScaledAltitudeMask(worldPos, worldNormal, altitude, midpoint, planetOrigin);
}

//
//  Per pixel scaled mask functions
//

// Per pixel
float GetSlopeScaled(float3 flatWorldNormal, float3 worldNormal)
{
    // We abs in the very strange case of overhangs
    float slope = abs(dot(flatWorldNormal, worldNormal));
    slope = pow(slope, _SteepPower);
    slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
    return 1 - slope;
}

// Per pixel
float4 GetScaledAltitudeMask(float3 worldNormal, float3 flatWorldNormal, float altitude, float midpoint)
{
    float lowMidBlend = GetPercentageAltitudeBetween(altitude, _LowMidBlendStart, _LowMidBlendEnd);
    float midHighBlend = GetPercentageAltitudeBetween(altitude, _MidHighBlendStart, _MidHighBlendEnd);
    float slope = GetSlopeScaled(flatWorldNormal, worldNormal);

    // Land mask - Low-mid blend in red, mid-high blend in green, steep in blue
    return float4(lowMidBlend, midHighBlend, slope, midpoint);
}

// Per pixel
float4 GetScaledLandMask(float altitude, float3 flatWorldNormal, float3 worldNormal)
{
    altitude += _WorldPlanetRadius;
    float midpoint = altitude / (_MidHighBlendStart + _LowMidBlendEnd);
    return GetScaledAltitudeMask(worldNormal, flatWorldNormal, altitude, midpoint);
}

#if defined (OCEAN)
    #define GET_OCEAN_DIFFUSE float4 oceanDiffuse = _OceanColor;
#elif defined (OCEAN_FROM_COLORMAP)
    #define GET_OCEAN_DIFFUSE float4 oceanDiffuse = diffuseColor;
#endif

#if defined (SCALED_EMISSIVE_MAP) && defined (DIRECTIONAL) && !defined (PARALLAX_DEFERRED_PASS)
    #define APPLY_SCALED_EMISSION result.rgb += tex2D(_EmissiveMap, i.uv).rgb * _EmissiveIntensity;
#else
    #define APPLY_SCALED_EMISSION
#endif

#if defined (SCALED_EMISSIVE_MAP) && defined (PARALLAX_DEFERRED_PASS)
    #define GET_SCALED_EMISSION tex2D(_EmissiveMap, i.uv).rgb * _EmissiveIntensity
#else
    #define GET_SCALED_EMISSION 0
#endif

#define PI 3.1415f

#if defined (ATMOSPHERE)
    float3 GetAtmosphereColor(float3 smoothWorldNormal, float3 viewDir)
    {
        // Just NdotL in a 0 to 1 range
        float textureCoord = dot(_WorldSpaceLightPos0, smoothWorldNormal) * 0.5f + 0.5f;
        textureCoord = saturate(textureCoord);
        
        // Some basic fresnel to give the atmosphere some perceived thickness
        float fresnelStrength = FresnelEffect(smoothWorldNormal, viewDir, _AtmosphereThickness);
        fresnelStrength += lerp(1 - fresnelStrength, 0, saturate(_AtmosphereThickness));
        
        uint width = 1;
        uint height = 1;
        _AtmosphereRimMap.GetDimensions(width, height);

        float texelSize = 1.0f / (float)width;
        float2 uv = float2(textureCoord, 0.5f);

        // Sample positions with 2-pixel and 4-pixel offsets
        float pos1 = saturate(uv.x - 5.25f * texelSize);
        float pos2 = saturate(uv.x - 2.75f * texelSize);
        float pos3 = uv.x;
        float pos4 = saturate(uv.x + 2.75f * texelSize);
        float pos5 = saturate(uv.x + 5.25f * texelSize);

        float3 atmoColor = 
        (
            _AtmosphereRimMap.Sample(point_clamp_sampler_AtmosphereRimMap, float2(pos1, uv.y)).rgb +
            _AtmosphereRimMap.Sample(point_clamp_sampler_AtmosphereRimMap, float2(pos2, uv.y)).rgb +
            _AtmosphereRimMap.Sample(point_clamp_sampler_AtmosphereRimMap, float2(pos3, uv.y)).rgb +
            _AtmosphereRimMap.Sample(point_clamp_sampler_AtmosphereRimMap, float2(pos4, uv.y)).rgb +
            _AtmosphereRimMap.Sample(point_clamp_sampler_AtmosphereRimMap, float2(pos5, uv.y)).rgb
        ) * (1.0f / 5.0f);

        return atmoColor * fresnelStrength;
    }
#else
    float3 GetAtmosphereColor(float3 smoothWorldNormal, float3 viewDir)
    {
        return 0;
    }
#endif


// Fake reflections as emission
inline half3 UnityGI_IndirectSpecularBasic(float4 probeHDR, half occlusion, Unity_GlossyEnvironmentData glossIn)
{
    half3 specular;

    half3 env0 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_Skybox), probeHDR, glossIn);
    specular = env0;

    return specular * occlusion;
}

// Adjustment of Unity's BRDF1_Unity_PBS, just stripped down to remove unused calculations
half3 BRDF_EnvironmentReflection(half3 specColor, half smoothness, float3 normal, float3 viewDir, float3 envColor)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
        roughness = max(roughness, 0.002); // Prevents division errors
    
    half nv = abs(dot(normal, viewDir));
    
    #ifdef UNITY_COLORSPACE_GAMMA
            half surfaceReduction = 1.0 - 0.28 * roughness * perceptualRoughness;
    #else
    half surfaceReduction = 1.0 / (roughness * roughness + 1.0);
    #endif
    
    half grazingTerm = saturate(smoothness + (1 - max(specColor.r, max(specColor.g, specColor.b))));
    return surfaceReduction * envColor * FresnelLerp(specColor, grazingTerm, nv);
}

//
// Texture Set Calcs
// When using lighter shader variations these aren't included
//

#if defined (INFLUENCE_MAPPING)
#define BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)                                                 \
        fixed4 diffuseName = SampleBiplanarTexture(diffuseSampler, params, worldUVs);                                                       \
        NORMAL_FLOAT normalName = SampleBiplanarNormal(normalSampler, params, worldUVs, localPlanetNormal);                                 \
        float diffuseName##Lum = (diffuseName.r * 0.21f + diffuseName.g * 0.72f + diffuseName.b * 0.07f) + 0.5f;                            \
        diffuseName.rgb = lerp(vertexColor * diffuseName##Lum, diffuseName.rgb, diffuseName##InfluenceValue);                               \
        //diffuseName.a = lerp(diffuseColor.a, diffuseName.a, diffuseName##InfluenceValue);
#else
#define BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)                                                 \
        fixed4 diffuseName = SampleBiplanarTexture(diffuseSampler, params, worldUVs);                                                       \
        NORMAL_FLOAT normalName = SampleBiplanarNormal(normalSampler, params, worldUVs, localPlanetNormal);
#endif

#define BIPLANAR_TEXTURE_SCALED(texName, texSampler)   fixed4 texName = SampleBiplanarTexture(texSampler, params, worldUVs);

// If we are sampling a low texture, else declare nothing
#if defined (PARALLAX_SINGLE_LOW) || defined (PARALLAX_DOUBLE_LOWMID) || defined (PARALLAX_FULL)
#define DECLARE_LOW_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)
#else
#define DECLARE_LOW_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

// If we are sampling a mid texture, else declare nothing
#if defined (PARALLAX_SINGLE_MID) || defined (PARALLAX_DOUBLE_LOWMID) || defined (PARALLAX_DOUBLE_MIDHIGH) || defined (PARALLAX_FULL)
#define DECLARE_MID_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)
#else
#define DECLARE_MID_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

// If we are sampling a high texture, else declare nothing
#if defined (PARALLAX_SINGLE_HIGH) || defined (PARALLAX_DOUBLE_MIDHIGH) || defined (PARALLAX_FULL)
#define DECLARE_HIGH_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)
#else
#define DECLARE_HIGH_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

#if defined (ADVANCED_BLENDING)
#define DECLARE_DISPLACEMENT_TEXTURE_SCALED(displacementTexName, displacementSampler) BIPLANAR_TEXTURE_SCALED(displacementTexName, displacementSampler)
#else
#define DECLARE_DISPLACEMENT_TEXTURE_SCALED(displacementTexName, displacementSampler)
#endif

#if defined (AMBIENT_OCCLUSION)
#define DECLARE_AMBIENT_OCCLUSION_TEXTURE_SCALED(occlusionTexName, occlusionSampler) BIPLANAR_TEXTURE_SCALED(occlusionTexName, occlusionSampler)
#else
#define DECLARE_AMBIENT_OCCLUSION_TEXTURE_SCALED(occlusionTexName, occlusionSampler)
#endif

// We are always sampling the slope texture
#define DECLARE_STEEP_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET_SCALED(diffuseName, normalName, diffuseSampler, normalSampler)

// Get global influence texture and values, otherwise declare nothing
#if defined (INFLUENCE_MAPPING)
#define DECLARE_INFLUENCE_TEXTURE_SCALED float4 globalInfluence = SampleBiplanarTexture(_InfluenceMap, params, worldUVs);
#else
#define DECLARE_INFLUENCE_TEXTURE_SCALED
#endif