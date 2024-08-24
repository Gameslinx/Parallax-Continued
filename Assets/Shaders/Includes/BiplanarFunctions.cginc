#if defined (EMISSION) && defined (DIRECTIONAL)
    #define NORMAL_FLOAT float4
#else
    #define NORMAL_FLOAT float3
#endif

#define TERRAIN_TEX_BLEND_FREQUENCY 0.2
#define TERRAIN_TEX_BLEND_OFFSET    0.4

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