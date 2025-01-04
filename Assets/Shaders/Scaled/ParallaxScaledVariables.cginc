// Samplers
sampler2D _ColorMap;
sampler2D _BumpMap;
sampler2D _HeightMap;

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