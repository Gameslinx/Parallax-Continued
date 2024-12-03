// Samplers
#if defined (AMBIENT_OCCLUSION)

sampler2D _OcclusionMap;

#endif

float2 _MainTex_ST;

float _BiplanarBlendFactor;
float _Tiling;

float _DisplacementScale;
float _DisplacementOffset;

float _BumpScale;

// Slope params
float _SteepPower;
float _SteepContrast;
float _SteepMidpoint;

// Tessellation params
float _MaxTessellation;
float _TessellationEdgeLength;
float _MaxTessellationRange;

// Emission
float3 _EmissionColor;

//
// Other / game params
//

float3 _TerrainShaderOffset;
float3 _PlanetOrigin;
float _PlanetRadius;
float _PlanetOpacity;

// Conditional params

float _LowMidBlendStart;
float _LowMidBlendEnd;

float _MidHighBlendStart;
float _MidHighBlendEnd;
