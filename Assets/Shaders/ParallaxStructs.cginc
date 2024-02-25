
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
        float3 worldPos : INTERNALTESSPOS;          \
        float3 worldNormal : NORMAL;                \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_FACTORS       \
    struct TessellationFactors                      \
    {                                               \
        float edge[3] : SV_TessFactor;              \
        float inside : SV_InsideTessFactor;         \
    };

#define PARALLAX_SHADOW_CASTER_STRUCT_INTERP        \
    struct Interpolators                            \
    {                                               \
        float4 pos : SV_POSITION;                   \
        float3 worldPos : TEXCOORD0;                \
        float3 worldNormal : NORMAL;                \
    };
