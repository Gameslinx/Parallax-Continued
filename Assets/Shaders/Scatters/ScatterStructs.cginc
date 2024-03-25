//
//  ScatterStructs
//  Used in the TerrainScatters compute shader
//

// Output from the distribute points kernel
struct PositionData
{
    float3 localPos;
    float3 localScale;
    float rotation;
    uint index;
};

// Output from the evaluate points kernel
// And sent to shader for rendering
struct TransformData
{
    float4x4 objectToWorld;
};