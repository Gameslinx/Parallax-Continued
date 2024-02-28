//
//  Structs for parallax shader stages
//

// Single:  One surface texture
// Double:  Blend between two altitude based textures
// Full:    Blend between three altitude based textures
// All have slope based textures

#pragma multi_compile PARALLAX_SINGLE PARALLAX_DOUBLE PARALLAX_FULL

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
//  Forward Lighting Structs
//

#define PARALLAX_FORWARDBASE_STRUCT_APPDATA         \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
        float3 color : COLOR;                       \
    };

#define PARALLAX_FORWARDBASE_STRUCT_CONTROL         \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 viewDir : TEXCOORD1;                 \
        float3 color : COLOR;                       \
    };

#define PARALLAX_FORWARDBASE_STRUCT_INTERP          \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD0;                \
        float3 worldNormal : NORMAL;                \
        float3 viewDir : TEXCOORD1;                 \
        float3 color : COLOR;                       \
        LIGHTING_COORDS(4, 5)                       \
    };

//
//  Shadow Caster Structs
//

#define PARALLAX_SHADOW_CASTER_STRUCT_APPDATA       \
    struct appdata                                  \
    {                                               \
        float4 vertex : POSITION;                   \
        float3 normal : NORMAL;                     \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_CONTROL       \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                     \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_INTERP        \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                     \
        float3 worldPos : TEXCOORD0;                \
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
        float3 color : COLOR;                       \
    };

#define PARALLAX_FORWARDADD_STRUCT_CONTROL          \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float3 viewDir : TEXCOORD1;                 \
        float3 lightDir : TEXCOORD2;                \
        float4 vertex : TEXCOORD3;                  \
        float3 color : COLOR;                       \
    };

#define PARALLAX_FORWARDADD_STRUCT_INTERP           \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD0;                \
        float3 worldNormal : NORMAL;                \
        float3 viewDir : TEXCOORD1;                 \
        float3 lightDir : TEXCOORD2;                \
        float3 color : COLOR;                       \
        float4 vertex : TEXCOORD3;                  \
        LIGHTING_COORDS(4, 5)                       \
    };