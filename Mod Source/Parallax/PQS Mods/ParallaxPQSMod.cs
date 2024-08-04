using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.ConfigParser.Enumerations;
using Kopernicus.Configuration.ModLoader;
using Kopernicus.Configuration.NoiseLoader.Noise;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    // Parallax PQSMod - responsible for adding terrain shader quad data and scatter system quad data
    public class PQSMod_Parallax : PQSMod
    {
        public int subdivisionLevel = 1;
        public float subdivisionRadius = 100.0f;
        public static Dictionary<PQ, TerrainShaderQuadData> terrainQuadData = new Dictionary<PQ, TerrainShaderQuadData>();
        /// <summary>
        /// Contains the UVs that map the entire planet for sampling the biome map
        /// </summary>
        public static Dictionary<PQ, Vector3[]> quadPlanetUVs = new Dictionary<PQ, Vector3[]>();

        Vector3[] uvCache;

        bool hasTerrainShader = false;
        public override void OnSetup()
        {
            // remove?
            this.requirements = PQS.ModiferRequirements.MeshColorChannel;
            DetermineRequirements();
        }
        public void DetermineRequirements()
        {
            if (ConfigLoader.parallaxTerrainBodies.ContainsKey(sphere.name))
            {
                hasTerrainShader = true;
            }

            // If this planet has scatters, we need to set the subdivision requirements
            if (ConfigLoader.parallaxScatterBodies.ContainsKey(sphere.name))
            {
                // Detect quad enhance coast pqsmod, if any. This modifies the subdivision threshold on a per-quad basis, so we need to increase the subdivision range accordingly
                PQSMod_QuadEnhanceCoast qec = sphere.mods.Where((x) => x.GetType() == typeof(PQSMod_QuadEnhanceCoast)).FirstOrDefault() as PQSMod_QuadEnhanceCoast;
                double thresholdMultiplier = 1;
                if (qec != null)
                {
                    thresholdMultiplier = Math.Max(qec.oceanFactor, qec.coastFactor);
                }
                ConfigLoader.parallaxScatterBodies[sphere.name].SetSubdivisionRequirements(sphere.subdivisionThresholds, thresholdMultiplier, sphere.maxLevel);
            }
        }
        // Occurs before vertex build - Get quad data here
        public override void OnQuadPreBuild(PQ quad)
        {
            uvCache = new Vector3[225];
        }
        public override void OnQuadBuilt(PQ quad)
        {
            // Add the terrain shader
            if (hasTerrainShader)
            {
                TerrainShaderQuadData terrainData = new TerrainShaderQuadData(quad, subdivisionLevel, subdivisionRadius, quad.subdivision == quad.sphereRoot.maxLevel);
                terrainData.Initialize();
                terrainQuadData.Add(quad, terrainData);
            }

            quadPlanetUVs.Add(quad, uvCache);
        }
        public override void OnQuadUpdateNormals(PQ quad)
        {
            if (hasTerrainShader && terrainQuadData.TryGetValue(quad, out TerrainShaderQuadData data))
            {
                data.OnQuadNormalUpdate();
            }
        }
        public override void OnQuadDestroy(PQ quad)
        {
            // Clean up terrain shader
            if (hasTerrainShader && terrainQuadData.TryGetValue(quad, out TerrainShaderQuadData data))
            {
                data.Cleanup();
                terrainQuadData.Remove(quad);
            }

            quadPlanetUVs.Remove(quad);
        }
        // Generate the planet UVs used for sampling the biome map (scatter system)
        // Also contains whether the vertex allows scatters in Z component
        public override void OnVertexBuild(PQS.VertexBuildData data)
        {
            uvCache[data.vertIndex] = new Vector3((float)data.u, (float)data.v, data.allowScatter ? 1.0f : 0.0f);
        }
    }
    [RequireConfigType(ConfigType.Node)]
    public class Parallax : ModLoader<PQSMod_Parallax>
    {
        [ParserTarget("subdivisionLevel", Optional = false)]
        public NumericParser<int> subdivisionLevel
        {
            get { return Mod.subdivisionLevel; }
            set { Mod.subdivisionLevel = value; }
        }
        [ParserTarget("subdivisionRadius", Optional = false)]
        public NumericParser<float> subdivisionRadius
        {
            get { return Mod.subdivisionRadius; }
            set { Mod.subdivisionRadius = value; }
        }
        [ParserTarget("order", Optional = false)]
        public NumericParser<int> order
        {
            get { return Mod.order; }
            set { Mod.order = int.MaxValue - 2; }
        }
    }
}
