// Samplers
sampler2D _ColorMap;
sampler2D _BumpMap;
sampler2D _HeightMap;

float _HeightDeformity;
float _MinRadialAltitude;
float _MaxRadialAltitude;

float _PlanetBumpScale;

float4x4 unity_ObjectToWorldNoTR;
textureCUBE _Skybox;
SamplerState sampler_Skybox;
half4 _Skybox_HDR;
float4x4 _SkyboxRotation;