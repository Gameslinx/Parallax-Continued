#define INSTANCE_DATA _InstanceData[instanceID]
#define WIND_BLUR_KERNEL_SIZE 2

#define GET_TEXTURE_WIDTH(texture) texture##_TexelSize.x;

// Use worldnormal to calculate triplanar weights for wind
float3 GetTriplanarBlendWeights(float3 worldNormal)
{
    float3 absWnrm = abs(worldNormal);
    float3 blendWeights = absWnrm / (absWnrm.x + absWnrm.y + absWnrm.z);
    return blendWeights;
}

// Super simple box blur with kernel size 2
float3 SampleWeighted(sampler2D tex, float2 prevUV, float2 uv, float pixelSize)
{
    //float3 accumulation = 0;
    //for (float i = -WIND_BLUR_KERNEL_SIZE; i <= WIND_BLUR_KERNEL_SIZE; i++)
    //{
    //    for (float j = -WIND_BLUR_KERNEL_SIZE; j <= WIND_BLUR_KERNEL_SIZE; j++)
    //    {
    //        accumulation += tex2Dlod(_WindMap, float4(uv + float2(i * pixelSize, j * pixelSize), 0, 0)).xyz;
    //    }
    //}
    //float3 sum = accumulation / ((WIND_BLUR_KERNEL_SIZE * 2 + 1) * (WIND_BLUR_KERNEL_SIZE * 2 + 1));
    //return sum;
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
    // Realistically this should be better
    //float3 texYZ = tex2Dlod(_WindMap, float4(worldPos.yz * _WindScale + uvOffset, 0, 0)).xyz;
    //float3 texZX = tex2Dlod(_WindMap, float4(worldPos.zx * _WindScale + uvOffset, 0, 0)).xyz;
    //float3 texXY = tex2Dlod(_WindMap, float4(worldPos.xy * _WindScale + uvOffset, 0, 0)).xyz;
    
    float pixelSize = GET_TEXTURE_WIDTH(_WindMap);
    float3 texYZ = tex2Dlod(_WindMap, float4(worldPos.yz * _WindScale + uvOffset, 0, 0)).xyz;
    float3 texZX = tex2Dlod(_WindMap, float4(worldPos.zx * _WindScale + uvOffset, 0, 0)).xyz;
    float3 texXY = tex2Dlod(_WindMap, float4(worldPos.xy * _WindScale + uvOffset, 0, 0)).xyz;
    
    // Blend textures
    float3 tex = texYZ * blendWeights.x + texZX * blendWeights.y + texXY * blendWeights.z;
    // Mul this by wind speed
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
    #define PROCESS_WIND worldPos = Wind(i.vertex.xyz, worldPos, planetNormal, objectToWorld);
#else
    #define PROCESS_WIND
#endif

// Enable alpha clipping for foliage
#if defined (ALPHA_CUTOFF)
    #define ALPHA_CLIP(value)   clip(value - _Cutoff)
#else
    #define ALPHA_CLIP(value)
#endif

// If alpha clipping is enabled, most people will probably want to also enable this
// To get specular alpha from a non conflicting source
#if defined (ALTERNATE_SPECULAR_TEXTURE)
    #define GET_SPECULAR(resultColor, uv)   resultColor.a = tex2D(_SpecularTexture, uv).a;
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

// Billboard
void Billboard(inout float4 vertex, inout float3 normal, inout float4 tangent, float4x4 mat)
{
    float3 local = vertex.xyz;
                
    float3 upVector = float3(0, 1, 0);
    float3 forwardVector = mul(UNITY_MATRIX_IT_MV[2].xyz, mat);
    float3 rightVector = normalize(cross(forwardVector, upVector));
             
    float3 position = local.x * rightVector + local.y * upVector + local.z * forwardVector;
   
    float3x3 rotMat = float3x3(forwardVector, upVector, rightVector);
                
    vertex = float4(position, 1);
    
    #if !defined (BILLBOARD_USE_MESH_NORMALS)
    // Since we're in local space, we can set the y coordinate of the normal to 0 to prevent dependence on vertical viewing angle
    normal = normalize(float3(forwardVector.x, 0, forwardVector.z));
    tangent.xyz = rightVector;
    
    #else
    // At the moment, not sure how to do appropriate normal mapping for mesh normals where normals are pointing up
    #endif
    // Output local space position
}

#if defined (BILLBOARD) || defined (BILLBOARD_USE_MESH_NORMALS)
    #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)    Billboard(vertex, normal, tangent, objectToWorldMatrix);
#else
    #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)
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

// ADDITIONAL_LIGHTING_PARAMS must include every possible param that can be passed in, but only needs to pass in the required ones for this effect
#if defined (SUBSURFACE_SCATTERING) || defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
    // Only subsurface scattering defined
    #if defined (SUBSURFACE_USE_THICKNESS_TEXTURE)
        #define ADDITIONAL_LIGHTING_PARAMS(worldPos, thickness) , worldPos, thickness
    #else
        #define ADDITIONAL_LIGHTING_PARAMS(worldPos, thickness) , worldPos, 0
    #endif
    
#else
    #define ADDITIONAL_LIGHTING_PARAMS(worldPos, thickness)
#endif