
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
#define BACKFACE_CLIP_TOLERANCE -0.05
#define FRUSTUM_CLIP_TOLERANCE   0.5

#define BARYCENTRIC_INTERPOLATE(fieldName) \
		patch[0].fieldName * barycentricCoordinates.x + \
		patch[1].fieldName * barycentricCoordinates.y + \
		patch[2].fieldName * barycentricCoordinates.z

#define TERRAIN_TEX_BLEND_FREQUENCY 0.2
#define TERRAIN_TEX_BLEND_OFFSET    0.4

#if defined (EMISSION) && defined (DIRECTIONAL)
    #define NORMAL_FLOAT float4
#else
    #define NORMAL_FLOAT float3
#endif

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

#define CALCULATE_VERTEX_DISPLACEMENT(o, landMask)                                                                                                                  \
    float dislacementRange = 1 - min(1, terrainDistance / _MaxTessellationRange);                                                                                   \
    float4 displacementTex = SampleBiplanarTextureLOD(_DisplacementMap, params, worldUVsLevel0, worldUVsLevel1, o.worldNormal, texLevelBlend);                      \
    float displacement = BLEND_CHANNELS_IN_TEX(landMask, displacementTex);                                                                                          \
    displacement = lerp(displacement, displacementTex.a, landMask.b);                                                                                               \
    displacement = lerp(displacement * exponent * 2, displacement * exponent * 4, texLevelBlend);                                                                   \
    float3 displacedWorldPos = o.worldPos + (displacement + _DisplacementOffset) * o.worldNormal * _DisplacementScale * dislacementRange;

//
//  Biplanar Mapping Functions
//

struct VertexBiplanarParams
{
    float3 absWorldNormal;
    int3 ma;
    int3 mi;
    int3 me;
    float blend;
};

struct PixelBiplanarParams
{
    float3 absWorldNormal;
    float3 dpdx0;
    float3 dpdy0;
    float3 dpdx1;
    float3 dpdy1;
    int3 ma;
    int3 mi;
    int3 me;
    float blend;
};

#define BIPLANAR_BLEND_FACTOR 4.0f

#define GET_VERTEX_BIPLANAR_PARAMS(params, worldPos, normal)                            \
    params.absWorldNormal = abs(normal);                                                \
    params.ma = (params.absWorldNormal.x > params.absWorldNormal.y && params.absWorldNormal.x > params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y > params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.mi = (params.absWorldNormal.x < params.absWorldNormal.y && params.absWorldNormal.x < params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y < params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.me = 3 - params.mi - params.ma;                                              \
    params.blend = BIPLANAR_BLEND_FACTOR;

// We can't calculate ddx and ddy for worldUVsLevel0 and worldUVsLevel1 because it results in a 1 pixel band around the texture transition
// So we instead calculate ddx and ddy for the original world coords and transform them the same way we do with the world coords themselves
// Which visually is slightly inaccurate but i'll take a little blurring over artifacting

// (ddx(worldPos0) / distFromTerrain) * (_Tiling / scale0) * distFromTerrain

// Get pixel shader biplanar params, and transform partial derivs by the world coord transform
#define GET_PIXEL_BIPLANAR_PARAMS(params, worldPos, worldPos0, worldPos1, normal, scale0, scale1)                                 \
    params.absWorldNormal = abs(normal);                                                                    \
    params.dpdx0 = (ddx(worldPos)) * (_Tiling / scale0) ;                                                      \
    params.dpdy0 = (ddy(worldPos)) * (_Tiling / scale0) ;                                                      \
    params.dpdx1 = (ddx(worldPos)) * (_Tiling / scale1) ;                                                      \
    params.dpdy1 = (ddy(worldPos)) * (_Tiling / scale1) ;                                                      \
    params.ma = (params.absWorldNormal.x > params.absWorldNormal.y && params.absWorldNormal.x > params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y > params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.mi = (params.absWorldNormal.x < params.absWorldNormal.y && params.absWorldNormal.x < params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y < params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.me = 3 - params.mi - params.ma;                                                                  \
    params.blend = BIPLANAR_BLEND_FACTOR;

#define TEX2D_GRAD_COORDS_LEVEL0(axis, params, coords) float2(coords[axis.y], coords[axis.z]), float2(params.dpdx0[axis.y], params.dpdx0[axis.z]), float2(params.dpdy0[axis.y], params.dpdy0[axis.z])
#define TEX2D_GRAD_COORDS_LEVEL1(axis, params, coords) float2(coords[axis.y], coords[axis.z]), float2(params.dpdx1[axis.y], params.dpdx1[axis.z]), float2(params.dpdy1[axis.y], params.dpdy1[axis.z])

#define TEX2D_LOD_COORDS(axis, coords, level) float4(coords[axis.y], coords[axis.z], 0, level)

// Get the transformed world space coords for texture levels 0 and 1
// that define the zoom levels for the terrain to reduce tiling

// exponent * 0.5 is the same as pow(2, floorLogDistance - 1) to select the previous zoom level. It just avoids the use of pow twice
#define DO_WORLD_UV_CALCULATIONS(terrainDistance, worldPos)                                                                             \
    float logDistance = log2(terrainDistance * TERRAIN_TEX_BLEND_FREQUENCY + TERRAIN_TEX_BLEND_OFFSET);                                 \
    float floorLogDistance = floor(logDistance);                                                                                        \
    float exponent = pow(2, floorLogDistance);                                                                                          \
    float texScale0 = exponent * 0.5;                                                                                                   \
    float texScale1 = exponent;                                                                                                         \
    float3 worldUVsLevel0 = (worldPos + _TerrainShaderOffset) * _Tiling / texScale0;                                                    \
    float3 worldUVsLevel1 = (worldPos + _TerrainShaderOffset) * _Tiling / texScale1;                                                    \
    float texLevelBlend = saturate((logDistance - floorLogDistance) * 1);

float GetMipLevel(float3 texCoord, float3 dpdx, float3 dpdy)
{
    float md = max(dot(dpdx, dpdx), dot(dpdy, dpdy));
    return 0.5f * log2(md);
}

float4 SampleBiplanarTexture(sampler2D tex, PixelBiplanarParams params, float3 worldPos0, float3 worldPos1, float3 worldNormal, float blend)
{
    // Sample zoom level 0
    float4 x0 = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL0(params.ma, params, worldPos0));
    float4 y0 = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL0(params.me, params, worldPos0));
    
    // Sample zoom level 1
    float4 x1 = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL1(params.ma, params, worldPos1));
    float4 y1 = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL1(params.me, params, worldPos1));
    
    // Blend zoom levels
    float4 x = lerp(x0, x1, blend);
    float4 y = lerp(y0, y1, blend);
    
    // Compute blend weights
    float2 w = float2(params.absWorldNormal[params.ma.x], params.absWorldNormal[params.me.x]);
    
    // Blend
    w = saturate(w * 2.365744f - 1.365744f);
    w = pow(w, params.blend * 0.125f);
    
    // Blend
    return (x * w.x + y * w.y) / (w.x + w.y);
}

float4 SampleBiplanarTextureLOD(sampler2D tex, VertexBiplanarParams params, float3 worldPos0, float3 worldPos1, float3 worldNormal, float blend)
{
    // Project and fetch
    float4 x0 = tex2Dlod(tex, TEX2D_LOD_COORDS(params.ma, worldPos0, 0));
    float4 y0 = tex2Dlod(tex, TEX2D_LOD_COORDS(params.me, worldPos0, 0));
    
    float4 x1 = tex2Dlod(tex, TEX2D_LOD_COORDS(params.ma, worldPos1, 0));
    float4 y1 = tex2Dlod(tex, TEX2D_LOD_COORDS(params.me, worldPos1, 0));
    
    float4 x = lerp(x0, x1, blend);
    float4 y = lerp(y0, y1, blend);
    
    // Compute blend weights
    float2 w = float2(params.absWorldNormal[params.ma.x], params.absWorldNormal[params.me.x]);
    
    // Blend
    w = saturate(w * 2.365744f - 1.365744f);
    w = pow(w, params.blend * 0.125f);
    
    // Blend
    return (x * w.x + y * w.y) / (w.x + w.y);
}

NORMAL_FLOAT ParallaxUnpackNormalEmission(float4 normalTexture)
{
    // Normal in XYZ, emission in A
    #if defined (EMISSION) && defined (DIRECTIONAL)
        return float4(UnpackNormal(float4(normalTexture.xyz, 1)), normalTexture.a);
    #else
        return UnpackNormal(normalTexture);
    #endif
}

NORMAL_FLOAT SampleBiplanarNormal(sampler2D tex, PixelBiplanarParams params, float3 worldPos0, float3 worldPos1, float3 worldNormal, float blend)
{
    // Sample zoom level 0
    float4 texLevel0x = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL0(params.ma, params, worldPos0));
    float4 texLevel0y = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL0(params.me, params, worldPos0));
    
    // Sample zoom level 1
    float4 texLevel1x = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL1(params.ma, params, worldPos1));
    float4 texLevel1y = tex2Dgrad(tex, TEX2D_GRAD_COORDS_LEVEL1(params.me, params, worldPos1));

    // Unpack normals
    NORMAL_FLOAT x0 = ParallaxUnpackNormalEmission(texLevel0x);
    NORMAL_FLOAT y0 = ParallaxUnpackNormalEmission(texLevel0y);
    NORMAL_FLOAT x1 = ParallaxUnpackNormalEmission(texLevel1x);
    NORMAL_FLOAT y1 = ParallaxUnpackNormalEmission(texLevel1y);
    
    // Blend zoom levels
    NORMAL_FLOAT x = lerp(x0, x1, blend);
    NORMAL_FLOAT y = lerp(y0, y1, blend);
    
    // Scale normal by bump scale, but not emission if enabled
    x.xyz *= _BumpScale;
    y.xyz *= _BumpScale;
    
    // Swizzle axes depending on plane
    x.xyz = normalize(float3(x.y + worldNormal[params.ma.z], x.x + worldNormal[params.ma.y], worldNormal[params.ma.x]));
    y.xyz = normalize(float3(y.y + worldNormal[params.me.z], y.x + worldNormal[params.me.y], worldNormal[params.me.x]));
    
    // Swizzle back to world space
    x.xyz = float3(x[params.ma.z], x[params.ma.y], x[params.ma.x]);
    y.xyz = float3(y[params.me.z], y[params.me.y], y[params.me.x]);
    
    // Compute blend weights
    float2 w = float2(params.absWorldNormal[params.ma.x], params.absWorldNormal[params.me.x]);
    
    // Blend
    w = saturate(w * 2.365744f - 1.365744f);
    w = pow(w, params.blend * 0.125f);
    
    NORMAL_FLOAT result = (x * w.x + y * w.y) / (w.x + w.y);
    
    return result;
}

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

    

// Blend textures based on landmask
#define BLEND_ALL_TEXTURES(landMask, lowTex, midTex, highTex)                                                           \
    lerp(lowTex, midTex, landMask.r) * (landMask.a < 0.5) + lerp(midTex, highTex, landMask.g) * (landMask.a >= 0.5);

#define BLEND_TWO_TEXTURES(blend, tex1, tex2)                                                                           \
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
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   lowTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                tex.r

#elif defined (PARALLAX_SINGLE_MID)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   midTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                tex.g

#elif defined (PARALLAX_SINGLE_HIGH)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   highTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                tex.b

#elif defined (PARALLAX_DOUBLE_LOWMID)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   BLEND_TWO_TEXTURES(landMask.r, lowTex, midTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                BLEND_TWO_TEXTURES(landMask.r, tex.r, tex.g)

#elif defined (PARALLAX_DOUBLE_MIDHIGH)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   BLEND_TWO_TEXTURES(landMask.g, midTex, highTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                BLEND_TWO_TEXTURES(landMask.g, tex.g, tex.b)

#elif defined (PARALLAX_FULL)
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   BLEND_ALL_TEXTURES(landMask, lowTex, midTex, highTex)
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                BLEND_ALL_TEXTURES(landMask, tex.r, tex.g, tex.b)
#else
    // No keywords defined, fallback to low - But because of unused texture set, this will be black
    #define BLEND_TEXTURES(landMask, lowTex, midTex, highTex)   lowTex
    #define BLEND_CHANNELS_IN_TEX(landMask, tex)                tex.r
#endif