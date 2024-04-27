using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.ConfigParser.Enumerations;
using Kopernicus.Configuration.ModLoader;
using Kopernicus.Configuration.NoiseLoader.Noise;
using System;
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
        public static Dictionary<PQ, ScatterSystemQuadData> scatterQuadData = new Dictionary<PQ, ScatterSystemQuadData>();
        public override void OnSetup()
        {
            this.requirements = PQS.ModiferRequirements.MeshColorChannel;
        }
        public override void OnQuadBuilt(PQ quad)
        {
            Debug.Log("Quad is building NOW on " + quad.sphereRoot.name);
            // Add the terrain shader
            TerrainShaderQuadData terrainData = new TerrainShaderQuadData(quad, subdivisionLevel, subdivisionRadius, quad.subdivision == quad.sphereRoot.maxLevel);
            terrainData.Initialize();
            terrainQuadData.Add(quad, terrainData);

            // Add the scatter system
            ScatterSystemQuadData scatterData = new ScatterSystemQuadData(quad, subdivisionLevel, subdivisionRadius);
            scatterData.Initialize();
            scatterQuadData.Add(quad, scatterData);
        }
        public override void OnQuadDestroy(PQ quad)
        {
            // Clean up terrain shader
            if (terrainQuadData.ContainsKey(quad))
            {
                terrainQuadData[quad].Cleanup();
                terrainQuadData.Remove(quad);
            }

            // Clean up scatter system
            if (scatterQuadData.ContainsKey(quad))
            {
                scatterQuadData[quad].Cleanup();
                scatterQuadData.Remove(quad);
            }
        }
        public override void OnVertexBuild(PQS.VertexBuildData data)
        {
            // Direction from center stuff here
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
