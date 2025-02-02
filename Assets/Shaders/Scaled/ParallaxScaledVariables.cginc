
// We're at the sampler2D limit, so we need to group the terrain texture samplers into one
// Usually you'd be able to do sampler_linear_repeat_anisoX but that's not a thing in Unity 2019 (:
// What's worse is Unity locks down SamplerStates so you can't even define your own!
// Mickey mouse engine

// Rules for this workaround - all textures using another texture's samplerstate MUST contribute to the result, or Unity will error out

#if defined (SHADER_STAGE_DOMAIN)
#define PARALLAX_SAMPLER_STATE sampler_HeightMap
#endif


#if defined (SHADER_STAGE_FRAGMENT)

#if defined (PARALLAX_SINGLE_LOW)
#define PARALLAX_SAMPLER_STATE sampler_MainTexLow
#endif
#if defined (PARALLAX_SINGLE_MID)
#define PARALLAX_SAMPLER_STATE sampler_MainTexMid
#endif
#if defined (PARALLAX_SINGLE_HIGH)
#define PARALLAX_SAMPLER_STATE sampler_MainTexHigh
#endif
#if defined (PARALLAX_DOUBLE_LOWMID)
#define PARALLAX_SAMPLER_STATE sampler_MainTexMid
#endif
#if defined (PARALLAX_DOUBLE_MIDHIGH)
#define PARALLAX_SAMPLER_STATE sampler_MainTexMid
#endif
#if defined (PARALLAX_FULL)
#define PARALLAX_SAMPLER_STATE sampler_MainTexMid
#endif

#endif

#if !defined (PARALLAX_SAMPLER_STATE)
#define PARALLAX_SAMPLER_STAGE sampler_linear_repeat
#endif

SamplerState PARALLAX_SAMPLER_STATE;

// Samplers
sampler2D _ColorMap;
sampler2D _BumpMap;
sampler2D _HeightMap;

#if defined (SCALED_EMISSIVE_MAP)
sampler2D _EmissiveMap;
float _EmissiveIntensity;
#endif

// Default sampler used for most textures
SamplerState default_linear_repeat_sampler;

#if defined (ATMOSPHERE)
Texture2D _AtmosphereRimMap;
SamplerState point_clamp_sampler_AtmosphereRimMap;
#endif
//sampler2D _AtmosphereRimMap;

float4 _OceanColor;

float _OceanSpecularPower;
float _OceanSpecularIntensity;

float _MinRadialAltitude;
float _MaxRadialAltitude;
float _WorldPlanetRadius;

float _PlanetBumpScale;

float4x4 unity_ObjectToWorldNoTR;
textureCUBE _Skybox;
SamplerState sampler_Skybox;
half4 _Skybox_HDR;
float4x4 _SkyboxRotation;

float _AtmosphereThickness;

float _OceanAltitude;

// To save us on a multicompile
uint _DisableDisplacement;