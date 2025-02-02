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
    float3 dpdx;
    float3 dpdy;
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
#define GET_PIXEL_BIPLANAR_PARAMS(params, worldPos, normal, scale)                                          \
    params.absWorldNormal = abs(normal);                                                                    \
    params.dpdx = (ddx(worldPos)) * (_Tiling / scale);                                                      \
    params.dpdy = (ddy(worldPos)) * (_Tiling / scale);                                                      \
    params.ma = (params.absWorldNormal.x > params.absWorldNormal.y && params.absWorldNormal.x > params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y > params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.mi = (params.absWorldNormal.x < params.absWorldNormal.y && params.absWorldNormal.x < params.absWorldNormal.z) ? int3(0, 1, 2) : (params.absWorldNormal.y < params.absWorldNormal.z) ? int3(1, 2, 0) : int3(2, 0, 1);   \
    params.me = 3 - params.mi - params.ma;                                                                  \
    params.blend = BIPLANAR_BLEND_FACTOR;

#define TEX2D_GRAD_COORDS(axis, params, coords) float2(coords[axis.y], coords[axis.z]), float2(params.dpdx[axis.y], params.dpdx[axis.z]), float2(params.dpdy[axis.y], params.dpdy[axis.z])
#define TEX2D_LOD_COORDS(axis, coords, level) float4(coords[axis.y], coords[axis.z], 0, level)

// Get the transformed world space coords

#define DO_WORLD_UV_CALCULATIONS(worldPos)                                                                  \
    float texScale = 0.25f;                                                                                 \
    float3 worldUVs = worldPos * _Tiling / texScale;

float GetMipLevel(float3 texCoord, float3 dpdx, float3 dpdy)
{
    float md = max(dot(dpdx, dpdx), dot(dpdy, dpdy));
    return 0.5f * log2(md);
}

float4 SampleBiplanarTexture(Texture2D tex, PixelBiplanarParams params, float3 worldPos)
{
    // Sample zoom level 0
    float4 x = tex.SampleGrad(PARALLAX_SAMPLER_STATE, TEX2D_GRAD_COORDS(params.ma, params, worldPos)); //TEX2DGRAD(tex, PARALLAX_SAMPLER_STATE, TEX2D_GRAD_COORDS(params.ma, params, worldPos));
    float4 y = tex.SampleGrad(PARALLAX_SAMPLER_STATE, TEX2D_GRAD_COORDS(params.me, params, worldPos));
    
    // Compute blend weights
    float2 w = float2(params.absWorldNormal[params.ma.x], params.absWorldNormal[params.me.x]);
    
    // Blend
    w = saturate(w * 2.365744f - 1.365744f);
    w = pow(w, params.blend * 0.125f);
    
    // Blend
    return (x * w.x + y * w.y) / (w.x + w.y);
}

float4 SampleBiplanarTextureLOD(Texture2D tex, VertexBiplanarParams params, float3 worldPos)
{
    // Project and fetch
    float4 x = TEX2DLOD(tex, PARALLAX_SAMPLER_STATE, TEX2D_LOD_COORDS(params.ma, worldPos, 0));
    float4 y = TEX2DLOD(tex, PARALLAX_SAMPLER_STATE, TEX2D_LOD_COORDS(params.me, worldPos, 0));
    
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

NORMAL_FLOAT SampleBiplanarNormal(Texture2D tex, PixelBiplanarParams params, float3 worldPos, float3 worldNormal)
{
    // Sample textures
    float4 texLevelx = tex.SampleGrad(PARALLAX_SAMPLER_STATE, TEX2D_GRAD_COORDS(params.ma, params, worldPos));
    float4 texLevely = tex.SampleGrad(PARALLAX_SAMPLER_STATE, TEX2D_GRAD_COORDS(params.me, params, worldPos));

    // Unpack normals
    NORMAL_FLOAT x = ParallaxUnpackNormalEmission(texLevelx);
    NORMAL_FLOAT y = ParallaxUnpackNormalEmission(texLevely);
    
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