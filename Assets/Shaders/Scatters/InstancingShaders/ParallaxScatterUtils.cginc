#define INSTANCE_DATA _InstanceData[instanceID]

// The object to world transformation matrix (TRS matrix) uses every matrix value except for the first 3 values in the last row
// This row stores the colour of the terrain at this scatter position
// This macro separates the matrix into the colour and a clean local to world matrix
#define DECODE_INSTANCE_DATA(transformAndColorMatrix, outColor)                                                                          \
    float3 outColor = float3(transformAndColorMatrix[3][0], transformAndColorMatrix[3][1], transformAndColorMatrix[3][2]);               \
    transformAndColorMatrix[3] = float4(0, 0, 0, transformAndColorMatrix[3][3]);

#define DECODE_INSTANCE_DATA_SHADOW(transformAndColorMatrix)                                                                             \
    transformAndColorMatrix[3] = float4(0, 0, 0, transformAndColorMatrix[3][3]);

#define WIND_BLUR_KERNEL_SIZE 2
#define PI 3.1415926

#define GET_TEXTURE_WIDTH(texture) texture##_TexelSize.x;

// Use worldnormal to calculate triplanar weights for wind
float3 GetTriplanarBlendWeights(float3 worldNormal)
{
    float3 absWnrm = abs(worldNormal);
    float3 blendWeights = absWnrm / (absWnrm.x + absWnrm.y + absWnrm.z);
    return blendWeights;
}

// Precision seems fine, but potentially replace with a blur kernel instead (much more expensive though)
float3 SampleWeighted(sampler2D tex, float2 prevUV, float2 uv, float pixelSize)
{
    float3 currenttex = tex2Dlod(_WindMap, float4(uv, 0, 0)).xyz;
    float3 prevtex = tex2Dlod(_WindMap, float4(prevUV, 0, 0)).xyz;
    return (currenttex + prevtex) * 0.5;
}

// Get the wind offsets (r = x, g = y) and the wind speed modifier (b = speed)
// Use triplanar mapping, since this is planetary
float3 GetWindMap(float3 worldPos, float3 blendWeights)
{
    // Offset sample coords over time to give moving speed
    float2 uvOffset = frac(_Time.x * _WindSpeed);    //mult _Time.x by repetition factor
    float2 prevUVOffset = frac((_Time.x - unity_DeltaTime.x) * _WindSpeed);
    
    float pixelSize = GET_TEXTURE_WIDTH(_WindMap);
    float3 texYZ = tex2Dlod(_WindMap, float4(worldPos.yz * _WindScale + uvOffset, 0, 0)).xyz;
    float3 texZX = tex2Dlod(_WindMap, float4(worldPos.zx * _WindScale + uvOffset, 0, 0)).xyz;
    float3 texXY = tex2Dlod(_WindMap, float4(worldPos.xy * _WindScale + uvOffset, 0, 0)).xyz;
    
    // Blend textures
    float3 tex = texYZ * blendWeights.x + texZX * blendWeights.y + texXY * blendWeights.z;
    return tex;
}

float3 ProcessVertexSway(float3 localPos, float3 windMap, float3 windDirection)
{
    float heightFactor = pow(max(0, localPos.y - _WindHeightStart), _WindHeightFactor);
    
    localPos.xz += windMap.r * heightFactor * windDirection.xz * _WindSpeed;
    return localPos;
}

// Project vector a onto vector b
float3 Project(float3 a, float3 b)
{
    float scalar = dot(a, b) / dot(b, b);
    return float3(b.x * scalar, b.y * scalar, b.z * scalar);
}
            
// Function to get the components of vector b that don't align with vector a
// For example, the vector returned by this for (0, 1, 0) and (0, 1, 0) will be (0,0,0)
// However for (0, 1, 0) and (1, 0, 0) then (1, 0, 0) will be returned
float3 GetUnalignedVectorComponents(float3 a, float3 b)
{
    float3 projection = Project(a, b);
    return float3(a.x - projection.x, a.y - projection.y, a.z - projection.z);
}

float3 Wind(float3 localPos, float3 worldPos, float3 planetNormal, float4x4 objectToWorld)
{
    // 0 to 1
    float3 triplanarWeights = GetTriplanarBlendWeights(planetNormal);
    float3 windMap = GetWindMap(worldPos, triplanarWeights);
    
    //
    //  Get wind direction
    //
    
    // The actual direction of texture coordinate movement. Might as well make it +1x, +1y, +1z for ease
    float3 worldDirection = 1;
    
    // World space up is not the planet normal all the time (align to terrain normal)
    // Wind IRL will hit an angled surface and deflect along it, so we do the same here which covers most cases
    float3 worldSpaceUp = normalize(mul(objectToWorld, float4(0, 1, 0, 0)).xyz);
    
    // The direction of movement negating the vector aligned with the planet normal (Only magnitude of XZ in local space)
    float3 xzDirection = -normalize(GetUnalignedVectorComponents(worldDirection, worldSpaceUp));
    
    float3 offset = xzDirection * windMap.xyz * pow(localPos.y, _WindHeightFactor) * (localPos.y > _WindHeightStart) * _WindIntensity;
    float3 yOffset = worldSpaceUp * windMap.xyz * pow(localPos.y, _WindHeightFactor) * (localPos.y > _WindHeightStart) * _WindIntensity * 0.4;
    
    offset += yOffset;
    
    // Effect breaks down with negative local space y (-y to +y edges disappear)
    // Negative y will be in the ground anyway, so stop the effect from applying
    if (localPos.y < 0)
    {
        offset = 0;
    }
    
    return worldPos + offset;
}

// Get the vector components that don't align with the planet-to-vertex vector and flip them
// Essentially the same as flipping horizontal components only
// For grass/trees with mostly upward-facing normals, this is useful
#if defined (TWO_SIDED)
    #define CORRECT_TWOSIDED_WORLDNORMAL                                                                       \
        float3 unaligned = GetUnalignedVectorComponents(normalize(i.worldNormal), i.planetNormal);             \
        i.worldNormal = normalize(i.worldNormal - unaligned * 2.0f * (facing < 0));
#else
    #define CORRECT_TWOSIDED_WORLDNORMAL
#endif

// Process wind sway and offset local vertex
// Requires a re-transform back to world space, which we avoid if wind is disabled
#if defined (WIND)
    #define PROCESS_WIND(i) worldPos = Wind(i.vertex.xyz, worldPos, planetNormal, objectToWorld);
#else
    #define PROCESS_WIND(i)
#endif

// Enable alpha clipping for foliage
#if defined (ALPHA_CUTOFF)
    #define ALPHA_CLIP(value)   clip(value - _Cutoff)
#else
    #define ALPHA_CLIP(value)
#endif

// If alpha clipping is enabled, most people will probably want to also enable this
// To get specular alpha from a non conflicting source

// If refraction is enabled, refraction intensity is stored in the main tex alpha so we need to assign it to its own variable before overwriting from specular tex
#if defined (ALTERNATE_SPECULAR_TEXTURE) || defined (REFRACTION)
    #if defined (REFRACTION)
        #define GET_SPECULAR(resultColor, uv)                                       \
            float refractionIntensity = resultColor.a;                              \
            resultColor.a = tex2D(_SpecularTexture, uv).a;
    #else
        #define GET_SPECULAR(resultColor, uv)   resultColor.a = tex2D(_SpecularTexture, uv).a;
    #endif
#else
    #define GET_SPECULAR(resultColor, uv)
#endif

#if defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
    #define GET_THICKNESS(uv)               float normalisedThickness = tex2D(_ThicknessMap, uv).r;
    #define THICKNESS                       normalisedThickness
#else
    #define GET_THICKNESS(uv)
    #define THICKNESS                       0
#endif

#if defined (REFRACTION)
    #define REFRACTION_INTENSITY refractionIntensity
#else
    #define REFRACTION_INTENSITY 0
#endif

//
//  Billboarding
//

#if !defined (PARALLAX_SHADOW_CASTER_PASS)
    #define BILLBOARD_INPUT inout float4 vertex, inout float3 normal, inout float4 tangent, float4x4 mat
#else
    #define BILLBOARD_INPUT inout float4 vertex, float4x4 mat
#endif

// Billboard
void Billboard(BILLBOARD_INPUT)
{
    float3 local = vertex.xyz;
                
    float3 upVector = float3(0, 1, 0);
    float3 forwardVector = mul(UNITY_MATRIX_IT_MV[2].xyz, mat);
    float3 rightVector = normalize(cross(forwardVector, upVector));
             
    float3 position = local.x * rightVector + local.y * upVector + local.z * forwardVector;
                
    vertex = float4(position, 1);
    
    #if !defined (BILLBOARD_USE_MESH_NORMALS) && !defined (PARALLAX_SHADOW_CASTER_PASS)
    // Since we're in local space, we can set the y coordinate of the normal to 0 to prevent dependence on vertical viewing angle
    normal = normalize(float3(forwardVector.x, 0, forwardVector.z));
    tangent.xyz = rightVector;
    #endif
}

// If we're in the shadow caster pass and have billboard enabled, we don't care about the normal or tangent
#if defined (BILLBOARD) || defined (BILLBOARD_USE_MESH_NORMALS)
    #if !defined (PARALLAX_SHADOW_CASTER_PASS)
        #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)      Billboard(vertex, normal, tangent, objectToWorldMatrix);
    #else
        #define BILLBOARD_IF_ENABLED(vertex, objectToWorldMatrix)                       Billboard(vertex, objectToWorldMatrix);
    #endif
#else
    #if !defined (PARALLAX_SHADOW_CASTER_PASS)
        #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)
    #else
        #define BILLBOARD_IF_ENABLED(vertex, objectToWorldMatrix)
    #endif
#endif

#if defined (DEBUG_FACE_ORIENTATION)
    #define DEBUG_IF_ENABLED                                                 \
        float3 frontFaceColor = float3(0.345, 0.345, 0.898);                 \
        float3 backFaceColor = float3(0.898, 0.345, 0.345);                  \
        float3 faceColor = lerp(frontFaceColor, backFaceColor, -facing);     \
        result.rgb = faceColor; 
#else
    #define DEBUG_IF_ENABLED
#endif

//
// Lighting 
//

#define BASIC_LIGHTING_PARAMS mainTex, worldNormal, viewDir, GET_SHADOW, lightDir

// Subsurface scattering parameters
#if defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
    #define SUBSURFACE_THICKNESS_PARAM THICKNESS
#else
    #define SUBSURFACE_THICKNESS_PARAM 0
#endif

// Subsurface macro
#if defined (SUBSURFACE_SCATTERING) || defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
    #define SUBSURFACE_SCATTERING_PARAMS i.worldPos, SUBSURFACE_THICKNESS_PARAM
#else
    #define SUBSURFACE_SCATTERING_PARAMS
#endif

// Refraction parameters
#if defined (REFRACTION)
    #define REFRACTION_PARAMS refractionIntensity
#else
    #define REFRACTION_PARAMS
#endif

// ADDITIONAL_LIGHTING_PARAMS must include every possible param that can be passed in, but only needs to pass in the required ones for this effect
// For the record, it's not the prettiest. I would like this to be cleaner
#if defined(SUBSURFACE_SCATTERING) || defined(SUBSURFACE_USE_THICKNESS_TEXTURE)
    #if defined(REFRACTION)
        #define ADDITIONAL_LIGHTING_PARAMS , SUBSURFACE_SCATTERING_PARAMS,  REFRACTION_PARAMS
    #else
        #define ADDITIONAL_LIGHTING_PARAMS , SUBSURFACE_SCATTERING_PARAMS
    #endif
#else
    #if defined(REFRACTION)
        #define ADDITIONAL_LIGHTING_PARAMS , REFRACTION_PARAMS
    #else
        #define ADDITIONAL_LIGHTING_PARAMS
    #endif
#endif

// Alan Zucconi's method of approximating diffraction
// https://www.alanzucconi.com/tag/diffraction-grating/

float3 bump3y(float3 x, float3 yoffset)
{
    float3 y = 1 - x * x;
    y = saturate(y - yoffset);
    return y;
}
            
float3 spectral_zucconi6(float wavelength)
{
    // w: [400, 700]
    // x: [0,   1]
    float x = saturate((wavelength - 400.0) / 300.0);
            
    const float3 c1 = float3(3.54585104, 2.93225262, 2.41593945);
    const float3 x1 = float3(0.69549072, 0.49228336, 0.27699880);
    const float3 y1 = float3(0.02312639, 0.15225084, 0.52607955);
            
    const float3 c2 = float3(3.90307140, 3.21182957, 3.96587128);
    const float3 x2 = float3(0.11748627, 0.86755042, 0.66077860);
    const float3 y2 = float3(0.84897130, 0.88445281, 0.73949448);
            
    return bump3y(c1 * (x - x1), y1) + bump3y(c2 * (x - x2), y2);
}