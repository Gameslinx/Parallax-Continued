﻿//
//  ScatterUtils
//  Used in the TerrainScatters compute shader
//

#define DEG2RAD 0.01745329251f
#define ALLOWED_COLOR_RANGE 0.2f

#if defined (OUTPUT_TERRAIN_COLOR)
    #define ENCODE_INSTANCING_DATA(transformationMatrix)    transformationMatrix[3] = float4(avgColor, 1);
#else
    #define ENCODE_INSTANCING_DATA(transformationMatrix)    transformationMatrix[3] = float4(1, 1, 1, 1);
#endif

float DegToRad(float deg)
{
    return DEG2RAD * deg;
}

// Quaternion multiplication
float3 QuaternionMul(float4 rotation, float3 position)
{
    float num = rotation.x * 2.0f;
    float num2 = rotation.y * 2.0f;
    float num3 = rotation.z * 2.0f;
    float num4 = rotation.x * num;
    float num5 = rotation.y * num2;
    float num6 = rotation.z * num3;
    float num7 = rotation.x * num2;
    float num8 = rotation.x * num3;
    float num9 = rotation.y * num3;
    float num10 = rotation.w * num;
    float num11 = rotation.w * num2;
    float num12 = rotation.w * num3;
    float3 result = 0;
    result.x = (1.0f - (num5 + num6)) * position.x + (num7 - num12) * position.y + (num8 + num11) * position.z;
    result.y = (num7 + num12) * position.x + (1.0f - (num4 + num6)) * position.y + (num9 - num10) * position.z;
    result.z = (num8 - num11) * position.x + (num9 + num10) * position.y + (1.0f - (num4 + num5)) * position.z;
    return result;
}

//float3 QuaternionMul(float4 q, float3 v)
//{
//    float3 t = 2.0 * cross(q.xyz, v);
//    return v + q.w * t + cross(q.xyz, t);
//}

// Get point/direction at center of triangle
float3 TriCenter(float3 x, float3 y, float3 z)
{
    return (x + y + z) * 0.333333f;
}

// Get random float from input float3
// From: https://www.shadertoy.com/view/4djSRW
float Rand(float3 p3)
{
    p3 = frac(p3 * 0.1031f);
    p3 += dot(p3, p3.zyx + 31.32f);
    return frac((p3.x + p3.y) * p3.z);
}

// Returns a uniformly distributed point at a random position on a triangle
float3 RandomPoint(float3 p1, float3 p2, float3 p3, float r1, float r2)
{
    // r1 must be a sqrt random number
    return ((1 - r1) * p1) + ((r1 * (1 - r2)) * p2) + ((r2 * r1) * p3);
}

// Returns a uniformly distributed UV coordinate at a random position on a triangle - probably too expensive to be used if we're sampling biome maps all the time
float2 RandomUV(float2 uv1, float2 uv2, float2 uv3, float r1, float r2)
{
    // r1 must be a sqrt random number
    return ((1 - r1) * uv1) + ((r1 * (1 - r2)) * uv2) + ((r2 * r1) * uv3);
}

// Returns a uniformly distributed UV coordinate at a random position on a triangle - probably too expensive to be used if we're sampling biome maps all the time
float RandomFloat(float f1, float f2, float f3, float r1, float r2)
{
    // r1 must be a sqrt random number
    return ((1 - r1) * f1) + ((r1 * (1 - r2)) * f2) + ((r2 * r1) * f3);
}

bool BiomeEligible(float3 biomeColor, float3 scatterColor)
{
    float deviation = distance(biomeColor, scatterColor);
    return 1 * (deviation < ALLOWED_COLOR_RANGE);
}

// Calculate the slope of the mesh
float CalculateSlope(float3 normal)
{
    // Get gradient of slope
    float slope = abs(dot(normalize(-_LocalPlanetNormal), normal));
    
    slope = pow(slope, _SteepPower);
    slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
    
    return slope;
}

// Obtain a basic estimate for the curvature of the mesh based on how much the normals deviate from one another
float GetNormalDeviance(float3 normal1, float3 normal2, float3 normal3)
{
    float nrmDev1 = dot(normal2, normal3);
    float nrmDev2 = dot(normal3, normal1);
    float nrmDev3 = dot(normal1, normal2);
    float normalDeviance = min(nrmDev1, min(nrmDev2, nrmDev3));
    return normalDeviance;
}

// Construct translation matrix from a position
float4x4 GetTranslationMatrix(float3 pos)
{
    return float4x4(float4(1, 0, 0, pos.x), float4(0, 1, 0, pos.y), float4(0, 0, 1, pos.z), float4(0, 0, 0, 1)); //
}

// Construct rotation matrix from euler angles in degrees
float4x4 GetRotationMatrix(float3 anglesDeg)
{
    anglesDeg = float3(DegToRad(anglesDeg.x), DegToRad(anglesDeg.y), DegToRad(anglesDeg.z));

    float4x4 rotationX = 
        float4x4(float4(1, 0, 0, 0),
        float4(0, cos(anglesDeg.x), -sin(anglesDeg.x), 0),
        float4(0, sin(anglesDeg.x), cos(anglesDeg.x), 0),
        float4(0, 0, 0, 1));

    float4x4 rotationY = 
        float4x4(float4(cos(anglesDeg.y), 0, sin(anglesDeg.y), 0),
        float4(0, 1, 0, 0),
        float4(-sin(anglesDeg.y), 0, cos(anglesDeg.y), 0),
        float4(0, 0, 0, 1));

    float4x4 rotationZ = 
        float4x4(float4(cos(anglesDeg.z), -sin(anglesDeg.z), 0, 0),
        float4(sin(anglesDeg.z), cos(anglesDeg.z), 0, 0),
        float4(0, 0, 1, 0),
        float4(0, 0, 0, 1));

    return mul(rotationY, mul(rotationX, rotationZ));
}

// Construct scale matrix from non uniform float3 scale
float4x4 GetScaleMatrix(float3 scale)
{
    return float4x4(float4(scale.x, 0, 0, 0),
            float4(0, scale.y, 0, 0),
            float4(0, 0, scale.z, 0),
            float4(0, 0, 0, 1));
}

// Given a normalized vector a, construct the matrix that will transform vector a to normalized vector b
// In context, transform a vector pointing upwards in local space to the vector from the planet to the quad, such that scatters point "upwards" (out from the planet)
// From: https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
float4x4 TransformToPlanetNormal(float3 a, float3 b)
{
    float3 v = (cross(a, b));
    float v1 = v.x;
    float v2 = v.y;
    float v3 = v.z;
    
    float c = dot(a, b);
    float4x4 V = float4x4(
        float4(0,       v3,     -v2,    0),
        float4(-v3,     0,      v1,     0),
        float4(v2,      -v1,    0,      0),
        float4(0,       0,      0,      1)
        );
    
    float4x4 VPlusI = float4x4(
        float4(1,       v3,     -v2,    0),
        float4(-v3,     1,      v1,     0),
        float4(v2,      -v1,    1,      0),
        float4(0,       0,      0,      1)
        );
    
    float4x4 VSquared = mul(V, V);
    float lastPart = (1 / (1 + c));
    
    float4x4 halfMat = VSquared * lastPart;
    float4x4 full = transpose(halfMat + VPlusI);
    
    // Set remaining components to 0
    full[0].w = 0;
    full[1].w = 0;
    full[2].w = 0;
    
    full[3].w = 1;
    
    full[3].x = 0;
    full[3].y = 0;
    full[3].z = 0;
    
    return full;
}

// Construct the TRS matrix that transforms us from object space to world space
float4x4 GetTRSMatrix(float3 position, float3 rotationAngles, float3 scale, float3 terrainNormal)
{
    float3 nrm;
    if (_AlignToTerrainNormal == 0)
    {
        nrm = normalize(_PlanetNormal);
    }
    else
    {
        nrm = normalize(terrainNormal);
    }
    float3 up = float3(0, 1, 0);
    float4x4 mat = TransformToPlanetNormal(up, nrm);
    mat = mul(mat, GetRotationMatrix(rotationAngles));
    return mul(GetTranslationMatrix(position), mul(mat, GetScaleMatrix(scale)));
}

// Get distances to camera frustum planes
float4 CameraDistances0(float3 worldPos)
{
    return float4(
	    dot(_CameraFrustumPlanes[0].xyz, worldPos) + _CameraFrustumPlanes[0].w,
		dot(_CameraFrustumPlanes[1].xyz, worldPos) + _CameraFrustumPlanes[1].w,
		dot(_CameraFrustumPlanes[2].xyz, worldPos) + _CameraFrustumPlanes[2].w,
		dot(_CameraFrustumPlanes[3].xyz, worldPos) + _CameraFrustumPlanes[3].w
	);
}
// Get distances to camera frustum near and far plane
float4 CameraDistances1(float3 worldPos)
{
    return float4(
		dot(_CameraFrustumPlanes[4].xyz, worldPos) + _CameraFrustumPlanes[4].w,
		dot(_CameraFrustumPlanes[5].xyz, worldPos) + _CameraFrustumPlanes[5].w,
		0.00001f,
		0.00001f
	);
}

// Get blend as a percentage between two values
float GetPercentageBetween(float t, float lowerLimit, float upperLimit)
{
    float percentage = (t - lowerLimit) / (upperLimit - lowerLimit);
    return saturate(percentage);
}

// Get altitude fade range strength
float GetAltitudeScalar(float altitude, float3 worldPosition)
{
    float minAltitudeFadeStart = _MinAltitude + _AltitudeFadeRange * 0.5f;
    float minAltitudeFadeEnd = _MinAltitude - _AltitudeFadeRange * 0.5f;
    
    float maxAltitudeFadeStart = _MaxAltitude + _AltitudeFadeRange * 0.5f;
    float maxAltitudeFadeEnd = _MaxAltitude - _AltitudeFadeRange * 0.5f;
    
    // 1 at fadeStart, 0 at fadeEnd
    float rangeFadeMin = 1 - GetPercentageBetween(altitude, minAltitudeFadeStart, minAltitudeFadeEnd);
    
    // 0 at fadeStart, 1 at fadeEnd
    float rangeFadeMax = GetPercentageBetween(altitude, maxAltitudeFadeStart, maxAltitudeFadeEnd);
    
    float midpoint = (maxAltitudeFadeStart + minAltitudeFadeEnd) * 0.5f;
    
    // 0 when outside the altitude range, lerps to 1 when inside the altitude range
    
    return rangeFadeMin * (altitude < midpoint) + rangeFadeMax * (altitude > midpoint);
}

// Get random chance to cull before max range
bool IsOutOfRangeEarly(float normalisedRange, float3 localPosition)
{
    // 0 when at _RangeFadeStart, 1 at cutoff
    float despawnWeight = GetPercentageBetween(normalisedRange, _RangeFadeStart, 1);
    
    // Use local positions as the random seed for despawning
    float despawnChance = Rand(localPosition);
    
    // Despawn if the chance is lower than the weight
    return despawnChance < despawnWeight;
}

// Lerp between two points in a step - 0 at a, 1 at b
float LerpStep(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

//
//  Get noise values
//

#if defined (NOISEMODE_CELLULAR)

float GetNoiseValue(float3 worldPos)
{
    return Cellular3D(worldPos) * 2;
}

#elif defined (NOISEMODE_POLKADOT)

float GetNoiseValue(float3 worldPos)
{
    return SimplexPolkaDot3D(worldPos, 0.3, 1);
}
    
#else

// Default to Perlin noise
float GetNoiseValue(float3 worldPos)
{
    return SimplexPerlin3D(worldPos) * 1.5f;
}

#endif

// Process FBM noise by layering noises on top of each other
float Noise(float3 dirFromCenter)
{
    // Offset the noise to establish a different noise pattern per seed
    dirFromCenter += _NoiseSeed * -3.0f;
    
    float noiseValue = 0;
    float frequency = _NoiseFrequency;
    float amplitude = 1;
    float perOctaveOffset = 2;
    
    for (int i = 0; i < _NoiseOctaves; i++)
    {
        // Offset dir from center by entire phase
        dirFromCenter += perOctaveOffset;

        frequency *= _NoiseLacunarity;
        amplitude *= 0.5;
        
        noiseValue += GetNoiseValue(dirFromCenter * frequency) * amplitude;
    }
    
    if (_InvertNoise == 1)
    {
        noiseValue = 1 - abs(noiseValue);
    }
    else
    {
        noiseValue = noiseValue * 0.5f + 0.5f;
    }
    
    return noiseValue;
}