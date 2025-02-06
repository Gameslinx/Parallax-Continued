// Upgrade NOTE: replaced 'defined TESSELLATION' with 'defined (TESSELLATION)'

//
//  Structs for parallax shader stages
//

#define HULL_SHADER_ATTRIBUTES                          \
    [domain("tri")]                                     \
    [outputcontrolpoints(3)]                            \
    [outputtopology("triangle_cw")]                     \
    [patchconstantfunc("PatchConstantFunction")]        \
    [partitioning("fractional_odd")]                    


//
//  Patch Constant Struct
//

#define PARALLAX_STRUCT_PATCH_CONSTANT              \
    struct TessellationFactors                      \
    {                                               \
        float edge[3] : SV_TessFactor;              \
        float inside : SV_InsideTessFactor;         \
    };

//
//  PARALLAX SCALED
//

//
//  Forward Lighting Structs
//

#ifndef BAKED

#define PARALLAX_FORWARDBASE_STRUCT_APPDATA         \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDBASE_STRUCT_CONTROL         \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
        float3 biplanarTextureCoords : TEXCOORD2;   \
    };

#define PARALLAX_FORWARDBASE_STRUCT_INTERP          \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD2;                \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float2 uv : TEXCOORD0;                      \
        float3 biplanarTextureCoords : TEXCOORD3;   \
        LIGHTING_COORDS(4, 5)                       \
        UNITY_FOG_COORDS(6)                         \
    };

//
//  Shadow Caster Structs
//

#define PARALLAX_SHADOW_CASTER_STRUCT_APPDATA       \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

// We need TBN in the shadow caster to produce proper mesh normals as we're typically displacing a sphere, and don't have the normals for the planet itself
#define PARALLAX_SHADOW_CASTER_STRUCT_CONTROL       \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                  \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_INTERP        \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                  \
        float3 worldPos : TEXCOORD3;                \
        float3 worldNormal : NORMAL;                \
    };

//
//  Forward Add Structs
//

// Must also define vertex and lightDir here

#define PARALLAX_FORWARDADD_STRUCT_APPDATA          \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDADD_STRUCT_CONTROL          \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float3 lightDir : TEXCOORD2;                \
        float4 vertex : TEXCOORD4;                  \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
        float3 biplanarTextureCoords : TEXCOORD5;   \
    };

#define PARALLAX_FORWARDADD_STRUCT_INTERP           \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD1;                \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD2;                 \
        float3 lightDir : TEXCOORD3;                \
        float4 vertex : TEXCOORD4;                  \
        float2 uv : TEXCOORD0;                      \
        float3 biplanarTextureCoords : TEXCOORD5;   \
        LIGHTING_COORDS(6, 7)                       \
    };


//
//  PARALLAX SCALED BAKED
//

#else

#define PARALLAX_FORWARDBASE_STRUCT_APPDATA         \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDBASE_STRUCT_CONTROL         \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDBASE_STRUCT_INTERP          \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD2;                \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float2 uv : TEXCOORD0;                      \
        LIGHTING_COORDS(3, 4)                       \
        UNITY_FOG_COORDS(5)                         \
    };

//
//  Shadow Caster Structs
//

#define PARALLAX_SHADOW_CASTER_STRUCT_APPDATA       \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

// We need TBN in the shadow caster to produce proper mesh normals as we're typically displacing a sphere, and don't have the normals for the planet itself
#define PARALLAX_SHADOW_CASTER_STRUCT_CONTROL       \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                  \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_INTERP        \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                  \
        float3 worldPos : TEXCOORD3;                \
        float3 worldNormal : NORMAL;                \
    };

//
//  Forward Add Structs
//

// Must also define vertex and lightDir here

#define PARALLAX_FORWARDADD_STRUCT_APPDATA          \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float4 tangent : TANGENT;                   \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDADD_STRUCT_CONTROL          \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD1;                 \
        float3 lightDir : TEXCOORD2;                \
        float4 vertex : TEXCOORD4;                  \
        float4 landMask : TEXCOORD3;                \
        float2 uv : TEXCOORD0;                      \
    };

#define PARALLAX_FORWARDADD_STRUCT_INTERP           \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD1;                \
        float3 worldNormal : NORMAL;                \
        float3 worldTangent : TANGENT;              \
        float3 worldBinormal : BINORMAL;            \
        float3 viewDir : TEXCOORD2;                 \
        float3 lightDir : TEXCOORD3;                \
        float4 vertex : TEXCOORD4;                  \
        float2 uv : TEXCOORD0;                      \
        LIGHTING_COORDS(5, 6)                       \
    };

#endif