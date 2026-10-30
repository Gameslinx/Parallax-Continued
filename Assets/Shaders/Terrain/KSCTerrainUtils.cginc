//
//  KSC ground shader replacement
//  UV0 holds the tarmac and marking UVs, UV1 holds the blend mask layout when the mesh has one
//

sampler2D _TarmacTexture;
sampler2D _TarmacNormal;
sampler2D _BlendMaskTexture;

float4 _TarmacTexture_ST;
float4 _TarmacNormal_ST;
float4 _BlendMaskTexture_ST;

float4 _TarmacColor;

// PQS colour at the structure, stands in for the vertex colour that terrain quads get from the PQS
float4 _TerrainColor;

// Mouse-over highlight that KSP sets on KSC facilities
float4 _RimColor;
float _RimFalloff;

// Rim term from stockEmit in Kerbal Konstructs' KSP-include.cginc (https://github.com/GER-Space/Kerbal-Konstructs)
// The MIT License (MIT)
// Copyright (c) 2015-2017 Matt "medsouz" Souza, Ashley "AlphaAsh" Hall, Christian "GER-Space" Bronk, Nikita "whale_2" Makeev
float3 GetRimHighlight(float3 viewDir, float3 normal)
{
    float rim = 1.0 - saturate(dot(viewDir, normal));
    return _RimColor.rgb * pow(rim, _RimFalloff) * _RimColor.a;
}

// Set on meshes without UV1, where the blend mask shares the tarmac UVs
float _BlendMaskUsesUV0;

float2 GetBlendMaskUV(float2 uv0, float2 uv1)
{
    return _BlendMaskUsesUV0 > 0.5 ? uv0 : uv1;
}

// 1 = tarmac, 0 = terrain
float GetTarmacBlend(float2 uv1)
{
    return tex2D(_BlendMaskTexture, TRANSFORM_TEX(uv1, _BlendMaskTexture)).r * _TarmacColor.a;
}

fixed4 GetTarmacDiffuse(float2 uv0)
{
    fixed4 tarmac = tex2D(_TarmacTexture, TRANSFORM_TEX(uv0, _TarmacTexture));
    tarmac.rgb *= _TarmacColor.rgb;

    // Alpha is the specular mask, and the stock tarmac has no specular
    tarmac.a = 0;
    return tarmac;
}

// Tangent frame from screen space derivatives to avoid the tarmac normal map ddepending on mesh tangents
float3 TangentToWorldNormal(float3 tangentNormal, float2 uv, float3 worldPos, float3 worldNormal)
{
    float3 dpdx = ddx(worldPos);
    float3 dpdy = ddy(worldPos);
    float2 duvdx = ddx(uv);
    float2 duvdy = ddy(uv);

    float3 dpdyPerp = cross(dpdy, worldNormal);
    float3 dpdxPerp = cross(worldNormal, dpdx);
    float3 tangent = dpdyPerp * duvdx.x + dpdxPerp * duvdy.x;
    float3 bitangent = dpdyPerp * duvdx.y + dpdxPerp * duvdy.y;

    float invScale = rsqrt(max(max(dot(tangent, tangent), dot(bitangent, bitangent)), 1e-12));
    return normalize(tangent * invScale * tangentNormal.x + bitangent * invScale * tangentNormal.y + worldNormal * tangentNormal.z);
}

NORMAL_FLOAT GetTarmacNormal(float2 uv0, float3 worldPos, float3 worldNormal)
{
    float2 uv = TRANSFORM_TEX(uv0, _TarmacNormal);
    float3 tangentNormal = UnpackNormal(tex2D(_TarmacNormal, uv));

    // Alpha of 1 means no emission
    NORMAL_FLOAT normal = 1;
    normal.xyz = TangentToWorldNormal(tangentNormal, uv, worldPos, worldNormal);
    return normal;
}
