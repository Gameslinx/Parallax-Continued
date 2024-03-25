//
//  Parallax Scatter Structs
//  Contains definitions for input/output structs and instancing data
//

// Instancing data
#define DECLARE_INSTANCING_DATA                 \
    StructuredBuffer<TransformData> _InstanceData;

//
//  Structs
//

#define PARALLAX_FORWARDBASE_STRUCT_APPDATA     \
    struct appdata                              \
    {                                           \
        float4 vertex : POSITION;               \
        float2 uv : TEXCOORD0;                  \
    };


    
#define PARALLAX_FORWARDBASE_STRUCT_V2F         \
    struct v2f                                  \
    {                                           \
        float4 pos : SV_POSITION;               \
        float2 uv : TEXCOORD0;                  \
        float3 worldPos : TEXCOORD1;            \
        float3 color : COLOR;                   \
    };