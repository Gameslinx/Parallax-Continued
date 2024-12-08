#if !defined (SCALED)
    #include "../Includes/BiplanarFunctions.cginc"
#else
    #include "../Includes/ScaledBiplanarFunctions.cginc"
#endif

//
//  Tessellation Functions
//  Most as or adapted from, and credit to, https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e
//

// Clip space range
#if defined (SHADER_API_GLCORE)
#define FAR_CLIP_VALUE 0
#else
#define FAR_CLIP_VALUE 1
#endif

// Clip tolerances
#define BACKFACE_CLIP_TOLERANCE -0.25
#define FRUSTUM_CLIP_TOLERANCE   0.75

#define BARYCENTRIC_INTERPOLATE(fieldName) \
		patch[0].fieldName * barycentricCoordinates.x + \
		patch[1].fieldName * barycentricCoordinates.y + \
		patch[2].fieldName * barycentricCoordinates.z

#define MAX_ADVANCED_BLENDING_SMOOTHNESS 1.0
#define MIN_ADVANCED_BLENDING_SMOOTHNESS 0.15

// True if point is outside bounds defined by lower and higher
bool IsOutOfBounds(float3 p, float3 lower, float3 higher)
{
    return p.x < lower.x || p.x > higher.x || p.y < lower.y || p.y > higher.y || p.z < lower.z || p.z > higher.z;
}

// True if vertex is outside of camera frustum
// Inputs a clip space position
bool IsPointOutOfFrustum(float4 pos)
{
    float3 culling = pos.xyz;
    float w = pos.w + FRUSTUM_CLIP_TOLERANCE;
    // UNITY_RAW_FAR_CLIP_VALUE is either 0 or 1, depending on graphics API
    // Most use 0, however OpenGL uses 1
    float3 lowerBounds = float3(-w, -w, -w * FAR_CLIP_VALUE);
    float3 higherBounds = float3(w, w, w);
    return IsOutOfBounds(culling, lowerBounds, higherBounds);
}

// Does the triangle normal face the camera position?
bool ShouldBackFaceCull(float3 nrm1, float3 nrm2, float3 nrm3, float3 w1, float3 w2, float3 w3)
{
    float3 faceNormal = (nrm1 + nrm2 + nrm3) * 0.333f;
    float3 faceWorldPos = (w1 + w2 + w2) * 0.333f;
    
    // Can't backface cull the shadow caster pass
#if !defined (PARALLAX_SHADOW_CASTER_PASS)
    return dot(faceNormal, normalize(_WorldSpaceCameraPos - faceWorldPos)) < BACKFACE_CLIP_TOLERANCE;
#else
    return 0;
#endif
}

// True if should be clipped by frustum or winding cull
// Inputs are clip space positions, world space normals, world space positions
bool ShouldClipPatch(float4 cp0, float4 cp1, float4 cp2, float3 n0, float3 n1, float3 n2, float3 wp0, float3 wp1, float3 wp2)
{
    bool allOutside = IsPointOutOfFrustum(cp0) && IsPointOutOfFrustum(cp1) && IsPointOutOfFrustum(cp2);
    return allOutside || ShouldBackFaceCull(n0, n1, n2, wp0, wp1, wp2);
}

// Remap
float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
{
    value = saturate((value - fromMin) / (fromMax - fromMin));
    return lerp(toMin, toMax, value);
}

// Calculate factor from edge length
// Vector inputs are world space
float EdgeTessellationFactor(float scale, float bias, float3 p0World, float4 p0Clip, float3 p1World, float4 p1Clip)
{
    float screenSpaceDistance = distance(p0Clip.xyz / p0Clip.w, p1Clip.xyz / p1Clip.w);
    float factor = screenSpaceDistance * (float) _ScreenParams.y / scale;
    float worldSpaceDistance = distance(_WorldSpaceCameraPos, (p0World + p1World) * 0.5);
    float distanceScalingFactor = 1 - saturate(worldSpaceDistance / _MaxTessellationRange);

    return max(1, factor * distanceScalingFactor);
}

//
// Smoothing Functions
//

// Calculate Phong projection offset
float3 PhongProjectedPosition(float3 flatPositionWS, float3 cornerPositionWS, float3 normalWS)
{
    return flatPositionWS - dot(flatPositionWS - cornerPositionWS, normalWS) * normalWS;
}

// Apply Phong smoothing
float3 CalculatePhongPosition(float3 bary, float3 p0PositionWS, float3 p0NormalWS, float3 p1PositionWS, float3 p1NormalWS, float3 p2PositionWS, float3 p2NormalWS)
{
    float3 flatPositionWS = bary.x * p0PositionWS + bary.y * p1PositionWS + bary.z * p2PositionWS;
    float3 smoothedPositionWS =
        bary.x * PhongProjectedPosition(flatPositionWS, p0PositionWS, p0NormalWS) +
        bary.y * PhongProjectedPosition(flatPositionWS, p1PositionWS, p1NormalWS) +
        bary.z * PhongProjectedPosition(flatPositionWS, p2PositionWS, p2NormalWS);
    return lerp(flatPositionWS, smoothedPositionWS, 0.333);
}

#define CALCULATE_VERTEX_DISPLACEMENT(o, landMask, displacementTex)                                                                                                 \
    float displacementRange = 1 - min(1, terrainDistance / _MaxTessellationRange);                                                                                  \
    float displacement = BLEND_CHANNELS_IN_TEX(landMask, displacementTex);                                                                                          \
    float displacementOffset = _DisplacementOffset;                                                                                                                 \
    displacement = lerp(displacement, displacementTex.a, landMask.b);                                                                                               \
    float displacementAndOffset = displacement + displacementOffset;                                                                                                \
    displacementAndOffset = lerp(displacementAndOffset * exponent * 2, displacementAndOffset * exponent * 4, texLevelBlend);                                        \
    float3 displacedWorldPos = o.worldPos + displacementAndOffset * o.worldNormal * _DisplacementScale * displacementRange;

//
//  Ingame Calcs
//

// Calculate Slope
// Vaue of 0 means flat land, 1 means slope
float GetSlope(float3 worldPos, float3 worldNormal)
{
    // We abs in the very strange case of overhangs
    float slope = abs(dot(normalize(worldPos - _PlanetOrigin), worldNormal));
    slope = pow(slope, _SteepPower);
    slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
    return 1 - slope;
}

// Get blend as a percentage between two altitudes
float GetPercentageAltitudeBetween(float altitude, float lowerLimit, float upperLimit)
{
    float percentage = (altitude - lowerLimit) / (upperLimit - lowerLimit);
    return saturate(percentage);
}

// Get Blend Factors
float4 GetAltitudeMask(float3 worldPos, float3 worldNormal, float altitude, float midpoint)
{
    float lowMidBlend = GetPercentageAltitudeBetween(altitude, _LowMidBlendStart, _LowMidBlendEnd);
    float midHighBlend = GetPercentageAltitudeBetween(altitude, _MidHighBlendStart, _MidHighBlendEnd);
    float slope = GetSlope(worldPos, worldNormal);

    // Land mask - Low-mid blend in red, mid-high blend in green, steep in blue
    return float4(lowMidBlend, midHighBlend, slope, midpoint);
}

// Get land mask macro
float4 GetLandMask(float3 worldPos, float3 worldNormal)
{
    float altitude = length(worldPos - _PlanetOrigin) - _PlanetRadius;
    float midpoint = altitude / (_MidHighBlendStart + _LowMidBlendEnd);
    return GetAltitudeMask(worldPos, worldNormal, altitude, midpoint);
}
// Displacement blending
float GetDisplacementLerpFactor(float heightLerp, float displacement1, float displacement2, float logDistance)
{
    float _Smoothness = lerp(MIN_ADVANCED_BLENDING_SMOOTHNESS, MAX_ADVANCED_BLENDING_SMOOTHNESS, saturate(logDistance * 0.15 - 0.5));
    
    // heightLerp * heightLerp not needed but balances out the blend (makes it more central)
    displacement2 += (heightLerp);
    displacement1 *= (1 - heightLerp);

    displacement2 = saturate(displacement2);
    displacement1 = saturate(displacement1);

    float diff = (displacement2 - displacement1) * heightLerp;

     
    diff /= _Smoothness;
    diff = saturate(diff);
    return diff;
}

#if defined (ADVANCED_BLENDING)
    #define CALCULATE_ADVANCED_BLENDING_FACTORS(landMask, displacement)                                                             \
    float lowMidDisplacementFactor = GetDisplacementLerpFactor(landMask.r, displacement.r, displacement.g, logDistance);            \
    float midHighDisplacementFactor = GetDisplacementLerpFactor(landMask.g, displacement.g, displacement.b, logDistance);           \
    float blendedDisplacements = BLEND_CHANNELS_IN_TEX(landMask, displacement);                                                     \
    float displacementSteepBlendFactor = GetDisplacementLerpFactor(landMask.b, blendedDisplacements, displacement.a, logDistance);  \
                                                                                                                                    \
    landMask.r = lowMidDisplacementFactor;                                                                                          \
    landMask.g = midHighDisplacementFactor;                                                                                         \
    landMask.b = displacementSteepBlendFactor;                                                                                      
#else
    #define CALCULATE_ADVANCED_BLENDING_FACTORS(landMask, displacement)
#endif


// Blend textures based on landmask
#define BLEND_ALL_TEXTURES(landMask, lowTex, midTex, highTex)                                                                    \
        lerp(lowTex, midTex, landMask.r) * (landMask.a < 0.5) + lerp(midTex, highTex, landMask.g) * (landMask.a >= 0.5);

#define BLEND_TWO_TEXTURES(blend, tex1, tex2)                                                                                    \
        lerp(tex1, tex2, blend);

//
// Texture Set Calcs
// When using lighter shader variations these aren't included
//

#if defined (INFLUENCE_MAPPING)
    #define BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)                                                                                                        \
        fixed4 diffuseName = SampleBiplanarTexture(diffuseSampler, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);                                                       \
        NORMAL_FLOAT normalName = SampleBiplanarNormal(normalSampler, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);                                                          \
        float diffuseName##Lum = (diffuseName.r * 0.21f + diffuseName.g * 0.72f + diffuseName.b * 0.07f) + 0.5f;                                                                                \
        diffuseName.rgb = lerp(vertexColor * diffuseName##Lum, diffuseName.rgb, diffuseName##InfluenceValue);
#else
    #define BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)                                                                                                        \
        fixed4 diffuseName = SampleBiplanarTexture(diffuseSampler, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);                                                       \
        NORMAL_FLOAT normalName = SampleBiplanarNormal(normalSampler, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);
#endif

#define BIPLANAR_TEXTURE(texName, texSampler)   fixed4 texName = SampleBiplanarTexture(texSampler, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

#define UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)  \
    fixed4 diffuseName = 0;                                                         \
    float3 normalName = 0;

#define BLEND_DIFFUSE_INPUT_PARAMS landMask, lowDiffuse, midDiffuse, highDiffuse

// If we are sampling a low texture, else declare nothing
#if defined (PARALLAX_SINGLE_LOW) || defined (PARALLAX_DOUBLE_LOWMID) || defined (PARALLAX_FULL)
    #define DECLARE_LOW_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#else
    #define DECLARE_LOW_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

// If we are sampling a mid texture, else declare nothing
#if defined (PARALLAX_SINGLE_MID) || defined (PARALLAX_DOUBLE_LOWMID) || defined (PARALLAX_DOUBLE_MIDHIGH) || defined (PARALLAX_FULL)
    #define DECLARE_MID_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#else
    #define DECLARE_MID_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

// If we are sampling a high texture, else declare nothing
#if defined (PARALLAX_SINGLE_HIGH) || defined (PARALLAX_DOUBLE_MIDHIGH) || defined (PARALLAX_FULL)
    #define DECLARE_HIGH_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#else
    #define DECLARE_HIGH_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) UNUSED_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)
#endif

#if defined (ADVANCED_BLENDING)
    #define DECLARE_DISPLACEMENT_TEXTURE(displacementTexName, displacementSampler) BIPLANAR_TEXTURE(displacementTexName, displacementSampler)
#else
    #define DECLARE_DISPLACEMENT_TEXTURE(displacementTexName, displacementSampler)
#endif

#if defined (AMBIENT_OCCLUSION)
    #define DECLARE_AMBIENT_OCCLUSION_TEXTURE(occlusionTexName, occlusionSampler) BIPLANAR_TEXTURE(occlusionTexName, occlusionSampler)
#else
    #define DECLARE_AMBIENT_OCCLUSION_TEXTURE(occlusionTexName, occlusionSampler)
#endif

// We are always sampling the slope texture
#define DECLARE_STEEP_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler) BIPLANAR_TEXTURE_SET(diffuseName, normalName, diffuseSampler, normalSampler)

// Get global influence texture and values, otherwise declare nothing
#if defined (INFLUENCE_MAPPING)
    #define DECLARE_INFLUENCE_TEXTURE float4 globalInfluence = SampleBiplanarTexture(_InfluenceMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);
    #define DECLARE_INFLUENCE_VALUES                                \
        float lowDiffuseInfluenceValue = globalInfluence.r;         \
        float midDiffuseInfluenceValue = globalInfluence.g;         \
        float highDiffuseInfluenceValue = globalInfluence.b;        \
        float steepDiffuseInfluenceValue = globalInfluence.a;
#else
    #define DECLARE_INFLUENCE_TEXTURE
    #define DECLARE_INFLUENCE_VALUES
#endif

//
//  Texture Blend Calcs
//  When some textures aren't being blended, their values are unused and anything is optimized out at compile time
//

#if defined (PARALLAX_SINGLE_LOW)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          lowTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       tex.r

#elif defined (PARALLAX_SINGLE_MID)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          midTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       tex.g

#elif defined (PARALLAX_SINGLE_HIGH)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          highTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       tex.b

#elif defined (PARALLAX_DOUBLE_LOWMID)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          BLEND_TWO_TEXTURES(landMask.r, lowTex, midTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       BLEND_TWO_TEXTURES(landMask.r, tex.r, tex.g)

#elif defined (PARALLAX_DOUBLE_MIDHIGH)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          BLEND_TWO_TEXTURES(landMask.g, midTex, highTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       BLEND_TWO_TEXTURES(landMask.g, tex.g, tex.b)

#elif defined (PARALLAX_FULL)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          BLEND_ALL_TEXTURES(landMask, lowTex, midTex, highTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       BLEND_ALL_TEXTURES(landMask, tex.r, tex.g, tex.b)
#else
    // No keywords defined, fallback to low - But because of unused texture set, this will be black
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)          lowTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                       tex.r
#endif

#if defined (AMBIENT_OCCLUSION)
    #define BLEND_OCCLUSION(landMask, occlusion)                                    \
        fixed4 altitudeOcclusion = BLEND_CHANNELS_IN_TEX(landMask, occlusion);      \
        fixed4 finalOcclusion = lerp(altitudeOcclusion, occlusion.a, landMask.b);
#else
    #define BLEND_OCCLUSION(landMask, occlusion)
#endif