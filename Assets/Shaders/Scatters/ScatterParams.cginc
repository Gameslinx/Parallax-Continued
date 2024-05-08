﻿// Max number of positions per triangle
int _PopulationMultiplier;
float _Seed;

// 1 = align scatter up vector to the terrain normal
// 0 = align scatter up to the planet normal
int _AlignToTerrainNormal;

// Vector from planet to quad
float3 _PlanetNormal;
float3 _LocalPlanetNormal;
float3 _PlanetOrigin;
float _PlanetRadius;

// Slope calculation params
float _SteepPower;
float _SteepContrast;
float _SteepMidpoint;

// Min and max scatter size
float3 _MinScale;
float3 _MaxScale;
float _ScaleRandomness;

float _MinAltitude;
float _MaxAltitude;
float _AltitudeFadeRange;

// Max distribution count
int _MaxCount;

float _NoiseScale;

// Chance for an object to spawn
float _SpawnChance;

// The maximum amount the normals can deviate on a triangle
float _MaxNormalDeviance;

// The colours of the biomes which this scatter can appear in
// Max 8
int _NumberOfBiomes;

//
//  Noise params
//

float _NoiseFrequency;
int _NoiseSeed;
float _NoiseLacunarity;
int _NoiseOctaves;

float _NoiseCutoffThreshold;

//
//  Evaluate params
//

// Camera frustum planes for frustum culling
float4 _CameraFrustumPlanes[6];
float3 _WorldSpaceCameraPosition;

// Define cull radius as the radius before frustum culling can begin
float _CullRadius;

// Define cull limit as the extents to the camera frustum before culling can begin (trees, for example, are tall. We want to be able to see any part of the tree without it disappearing)
float _CullLimit;

// Range before cutting off entirely
float _MaxRange;

float4x4 _ObjectToWorldMatrix;

// Lod splits - normalised
float _Lod01Split;
float _Lod12Split;