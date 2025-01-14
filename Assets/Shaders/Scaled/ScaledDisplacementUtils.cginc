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


//
// Texture Set Calcs
// When using lighter shader variations these aren't included
//

// Displacement blending
float GetDisplacementLerpFactorScaled(float heightLerp, float displacement1, float displacement2)
{
    // heightLerp * heightLerp not needed but balances out the blend (makes it more central)
    displacement2 += (heightLerp);
    displacement1 *= (1 - heightLerp);

    displacement2 = saturate(displacement2);
    displacement1 = saturate(displacement1);

    float diff = (displacement2 - displacement1) * heightLerp;

    diff = saturate(diff);
    return diff;
}

#if defined (ADVANCED_BLENDING)
#define CALCULATE_ADVANCED_BLENDING_FACTORS_SCALED(landMask, displacement)                                                              \
    float lowMidDisplacementFactor = GetDisplacementLerpFactorScaled(landMask.r, displacement.r, displacement.g);                       \
    float midHighDisplacementFactor = GetDisplacementLerpFactorScaled(landMask.g, displacement.g, displacement.b);                      \
    float blendedDisplacements = BLEND_CHANNELS_IN_TEX(landMask, displacement);                                                         \
    float displacementSteepBlendFactor = GetDisplacementLerpFactorScaled(landMask.b, blendedDisplacements, displacement.a);             \
                                                                                                                                        \
    landMask.r = lowMidDisplacementFactor;                                                                                              \
    landMask.g = midHighDisplacementFactor;                                                                                             \
    landMask.b = displacementSteepBlendFactor;                                                                                      
#else
#define CALCULATE_ADVANCED_BLENDING_FACTORS_SCALED(landMask, displacement)
#endif

float2 DirectionToEquirectangularUV(float3 direction)
{
    // Normalize the direction to ensure it's a unit vector
    direction = normalize(direction);

    // Compute the azimuthal angle (longitude) in the range [-PI, PI]
    float phi = atan2(direction.z, direction.x);

    // Compute the polar angle (latitude) in the range [0, PI]
    float theta = acos(direction.y);

    // Map phi and theta to UV coordinates
    float u = (phi / (2.0 * 3.14159265359)) + 0.5; // [0, 1] range
    float v = theta / 3.14159265359; // [0, 1] range

    return float2(u + 0.75, 1 - v);
}