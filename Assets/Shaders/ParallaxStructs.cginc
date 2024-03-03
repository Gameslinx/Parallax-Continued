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
        float4 landMask : TEXCOORD3;                \
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
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_CONTROL       \
    struct TessellationControlPoint                 \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float4 vertex : TEXCOORD1;                  \
        float3 normal : TEXCOORD2;                  \
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
        float4 landMask : TEXCOORD3;                \
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
        float4 vertex : TEXCOORD4;                  \
        float4 landMask : TEXCOORD3;                \
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