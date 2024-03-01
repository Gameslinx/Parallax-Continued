#define eta 0.7519

// Samplers
sampler2D _MainTexLow;
sampler2D _BumpMapLow;

sampler2D _MainTexMid;
sampler2D _BumpMapMid;

sampler2D _MainTexHigh;
sampler2D _BumpMapHigh;

sampler2D _MainTexSteep;
sampler2D _BumpMapSteep;

sampler2D _DisplacementMap;
sampler2D _InfluenceMap;

float2 _MainTex_ST;

float _BiplanarBlendFactor;
float _Tiling;

float _DisplacementScale;

// Slope params
float _SteepPower;
float _SteepContrast;
float _SteepMidpoint;

// Tessellation params
float _MaxTessellation;
float _TessellationEdgeLength;

// Lighting params
float _FresnelPower;
float _SpecularPower;
float _SpecularIntensity;
float _EnvironmentMapFactor;
float _RefractionIntensity;

//
// Other / game params
//

float3 _TerrainShaderOffset;
float3 _PlanetOrigin;
float _PlanetRadius;

// Conditional params

float _LowMidBlendStart;
float _LowMidBlendEnd;

float _MidHighBlendStart;
float _MidHighBlendEnd;
