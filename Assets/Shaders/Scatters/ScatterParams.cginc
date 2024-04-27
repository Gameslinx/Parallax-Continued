// Max number of positions per triangle
int _PopulationMultiplier;

// 1 = align scatter up vector to the terrain normal
// 0 = align scatter up to the planet normal
int _AlignToTerrainNormal;

// Vector from planet to quad
float3 _PlanetNormal;

// Max distribution count
int _MaxCount;

float _NoiseScale;

//
//  Evaluate params
//

float4x4 _ObjectToWorldMatrix;