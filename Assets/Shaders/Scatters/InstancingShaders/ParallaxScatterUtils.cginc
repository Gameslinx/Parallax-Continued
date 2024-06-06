#define INSTANCE_DATA _InstanceData[instanceID]

// Convert cartesian coordinate to spherical polar coordinate
float3 CartesianToSpherical(float3 cartesianPos)
{
    float3 relativeSpherePos = cartesianPos - _PlanetOrigin;
    
    
    float rho2 = dot(relativeSpherePos, relativeSpherePos);
    float theta = atan2(relativeSpherePos.y, relativeSpherePos.x);
    float phi = acos(relativeSpherePos.z / sqrt(rho2));
    
    return normalize(float3(rho2, theta, phi));
}

// Use worldnormal to calculate triplanar weights for wind
float3 GetTriplanarBlendWeights(float3 worldNormal)
{
    float3 absWnrm = abs(worldNormal);
    float3 blendWeights = absWnrm / (absWnrm.x + absWnrm.y + absWnrm.z);
    return blendWeights;
}

// Get the wind offsets (r = x, g = y) and the wind speed modifier (b = speed)
// Use triplanar mapping, since this is planetary
float3 GetWindMap(float3 worldPos, float3 blendWeights)
{
    // Offset sample coords over time to give moving speed
    float2 uvOffset = frac(_Time.x * 0.5f);    //mult _Time.x by repetition factor
                
    // Realistically this should be better
    float3 tex = tex2Dlod(_WindMap, float4(worldPos.xz * _WindScale + uvOffset, 0, 2)).xyz;
                
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

// Get the vector components that don't align with the planet-to-vertex vector and flip them
// Essentially the same as flipping horizontal components only
// For grass/trees with mostly upward-facing normals, this is useful
#if defined (TWO_SIDED)
    #define CORRECT_TWOSIDED_WORLDNORMAL                                                                                                \
        float3 unaligned = GetUnalignedVectorComponents(normalize(i.worldNormal), normalize(i.worldPos - _PlanetOrigin));               \
        i.worldNormal = normalize(i.worldNormal - unaligned * 2.0f * (facing < 0));
#else
    #define CORRECT_TWOSIDED_WORLDNORMAL
#endif

// Process wind sway and offset local vertex
// Requires a re-transform back to world space, which we avoid if wind is disabled
#if defined (WIND)
    #define PROCESS_WIND(worldPos, worldNormal)                                     \
        float3 blendWeights = GetTriplanarBlendWeights(worldNormal);                \
        float3 windMask = GetWindMap(worldPos, blendWeights);                       \
        float3 windDirection = normalize(float3(worldPos.x, 0, worldPos.z));        \
        windDirection = mul(windDirection, objectToWorld);                          \
        i.vertex.xyz = ProcessVertexSway(i.vertex.xyz, windMask, windDirection);
#else
    #define PROCESS_WIND(worldPos, worldNormal)
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
#if defined (BILLBOARD)
    #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)    Billboard(vertex, normal, tangent, objectToWorldMatrix);
#else
    #define BILLBOARD_IF_ENABLED(vertex, normal, tangent, objectToWorldMatrix)
#endif
