//
//  Parallax Scatter Structs
//  Contains definitions for input/output structs and instancing data
//

// Instancing data
#define DECLARE_INSTANCING_DATA                 \
    StructuredBuffer<TransformData> _InstanceData;

// Unity doesn't pass reflection probe data to instanced shaders
// So we'll approximate it with a fresnel colour instead
#define DONT_SAMPLE_REFLECTIONS

//
//  Structs
//

#if defined (TWO_SIDED) || defined (DEBUG_FACE_ORIENTATION)
    #define PIXEL_SHADER_INPUT(inputStructName) inputStructName i, float facing : VFACE
#else
    #define PIXEL_SHADER_INPUT(inputStructName) inputStructName i
#endif

#define PARALLAX_FORWARDBASE_STRUCT_APPDATA     \
    struct appdata                              \
    {                                           \
        float4 vertex : POSITION;               \
        float2 uv : TEXCOORD0;                  \
        float3 normal : NORMAL;                 \
        float4 tangent : TANGENT;               \
    };

#define PARALLAX_FORWARDBASE_STRUCT_V2F         \
    struct v2f                                  \
    {                                           \
        float4 pos : SV_POSITION;               \
        float3 color : COLOR;                   \
        float3 worldNormal : NORMAL;            \
        float3 worldTangent : TANGENT;          \
        float3 worldBinormal : BINORMAL;        \
        float2 uv : TEXCOORD0;                  \
        float3 worldPos : TEXCOORD1;            \
        float3 viewDir : TEXCOORD2;             \
        float3 planetNormal : TEXCOORD3;        \
        LIGHTING_COORDS(3,4)                    \
    };

//  Shadow caster struct

#define PARALLAX_SHADOW_CASTER_STRUCT_APPDATA   \
    struct appdata                              \
    {                                           \
        float4 vertex : POSITION;               \
        float2 uv : TEXCOORD0;                  \
        float3 normal : NORMAL;                 \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_V2F       \
    struct v2f                                  \
    {                                           \
        float4 pos : SV_POSITION;               \
        float2 uv : TEXCOORD0;                  \
    };

// Forward Add struct

#define PARALLAX_FORWARDADD_STRUCT_APPDATA      \
    struct appdata                              \
    {                                           \
        float4 vertex : POSITION;               \
        float2 uv : TEXCOORD0;                  \
        float3 normal : NORMAL;                 \
        float4 tangent : TANGENT;               \
    };

#define PARALLAX_FORWARDADD_STRUCT_V2F          \
    struct v2f                                  \
    {                                           \
        float4 pos : SV_POSITION;               \
        float3 color : COLOR;                   \
        float3 worldNormal : NORMAL;            \
        float3 worldTangent : TANGENT;          \
        float3 worldBinormal : BINORMAL;        \
        float2 uv : TEXCOORD0;                  \
        float3 worldPos : TEXCOORD1;            \
        float3 viewDir : TEXCOORD2;             \
        float3 planetNormal : TEXCOORD3;        \
        float3 lightDir : TEXCOORD4;            \
        LIGHTING_COORDS(5,6)                    \
    };