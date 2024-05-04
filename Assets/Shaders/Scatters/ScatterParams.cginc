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

// Chance for an object to spawn
float _SpawnChance;

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