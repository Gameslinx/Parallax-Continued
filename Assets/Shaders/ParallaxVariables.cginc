#define eta 0.7519

// Samplers
sampler2D _MainTex;
sampler2D _BumpMap;
sampler2D _DisplacementMap;

float2 _MainTex_ST;

float _BiplanarBlendFactor;
float _Tiling;

float _DisplacementScale;

// Tessellation params
float _MaxTessellation;
float _TessellationEdgeLength;

// Lighting params
float _FresnelPower;
float _SpecularPower;
float _SpecularIntensity;
float _EnvironmentMapFactor;
float _RefractionIntensity;

// Other / game params
float3 _TerrainShaderOffset;