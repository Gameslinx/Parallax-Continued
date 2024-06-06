using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct OptimizationParams
{
    public float frustumCullingIgnoreRadius;
    public float frustumCullingSafetyMargin;
    public int maxRenderableObjects;
}
public enum SubdivisionMode
{
    noSubdivision,
    nearestQuads
}
public enum NoiseType
{
    simplexPerlin,
    simplexCellular,
    simplexPolkaDot,

    // Maybe implement
    cubist,
    sparseConvolution,
    hermite
}
public struct SubdivisionParams
{
    // If the quad needs subdividing
    public SubdivisionMode subdivisionMode;
    public int maxSubdivisionLevel;
}
public struct NoiseParams
{
    public NoiseType noiseType;
    public bool inverted;
    public int octaves;
    public float lacunarity;
    public float frequency;
    public int seed;
}
public struct DistributionParams
{
    public float seed;
    public float spawnChance;
    public float range;
    public int populationMultiplier;
    public Vector3 minScale;
    public Vector3 maxScale;
    public float scaleRandomness;
    public float noiseCutoff;
    public float steepPower;
    public float steepContrast;
    public float steepMidpoint;
    public float maxNormalDeviance;
    public float minAltitude;
    public float maxAltitude;
    public float altitudeFadeRange;
    public int alignToTerrainNormal;
    public LOD lod1;
    public LOD lod2;
    public HashSet<string> biomeBlacklist;
}
public struct LOD
{
    public string modelPathOverride;
    public MaterialParams materialOverride;
    public float range;
}
// Holds shader and its variations - rest is processed at load time from shaderbank
public struct MaterialParams
{
    public string shader;
    public List<string> shaderKeywords;
    //public ShaderProperties shaderProperties;
}
public struct BiomeBlacklistParams
{
    // The name of each biome and the colours they correspond to where this scatter can appear - Max 8
    public List<string> blacklistedBiomes;
    public HashSet<string> fastBlacklistedBiomes;
}
// Stores scatter information
public class ParallaxScatterBody
{
    public string planetName;
    public int nearestQuadSubdivisionLevel = 1;
    public float nearestQuadSubdivisionRange = 1.0f;

    /// <summary>
    /// Contains scatters and shared scatters
    /// </summary>
    public Dictionary<string, Scatter> scatters = new Dictionary<string, Scatter>();

    // Shared textures across the planet
    public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

    /// <summary>
    /// Contains all scatters for fast iteration, but not all sharedScatters
    /// </summary>
    public Scatter[] fastScatters;
    public ParallaxScatterBody(string planetName)
    {
        this.planetName = planetName;
    }
    public void UnloadTextures()
    {
        foreach (KeyValuePair<string, Texture2D> texturePair in loadedTextures)
        {
            UnityEngine.Object.Destroy(texturePair.Value);
        }
        loadedTextures.Clear();
    }
}

// Stores Scatter information
public class Scatter
{
    public string scatterName;
    public string modelPath;

    public OptimizationParams optimizationParams;
    public SubdivisionParams subdivisionParams;
    public NoiseParams noiseParams;
    public DistributionParams distributionParams;
    public MaterialParams materialParams;
    public BiomeBlacklistParams biomeBlacklistParams;

    public Texture2D biomeControlMap;
    public int biomeCount = 0;

    public bool isShared = false;

    public Scatter(string scatterName)
    {
        this.scatterName = scatterName;
    }
}
// Scatter that can have a unique material but shares its distribution with its parent to avoid generating it twice
public class SharedScatter : Scatter
{
    public Scatter parent;
    public SharedScatter(string scatterName, Scatter parent) : base(scatterName)
    {
        this.parent = parent;
        this.isShared = true;
    }
}
