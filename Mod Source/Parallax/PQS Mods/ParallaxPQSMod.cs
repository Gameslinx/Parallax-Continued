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
        
        public ParallaxScatterBody scatterBody;

        bool hasScatters = false;
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
            //if (ConfigLoader.parallaxScatterBodies.ContainsKey(sphere.name))
            //{
            //    hasScatters = true;
            //    scatterBody = ConfigLoader.parallaxScatterBodies[sphere.name];
            //}
        }
        // Occurs before vertex build - Get quad data here
        public override void OnQuadPreBuild(PQ quad)
        {
            // Add the scatter system
            //if (hasScatters)
            //{
            //    ScatterSystemQuadData scatterData = new ScatterSystemQuadData(scatterBody, quad, subdivisionLevel, subdivisionRadius);
            //    scatterQuadData.Add(quad, scatterData);
            //}
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

            // Initialise the scatter system
            //if (hasScatters)
            //{
            //    ScatterSystemQuadData scatterData = scatterQuadData[quad];
            //    scatterData.Initialize();
            //}
        }
        public override void OnQuadDestroy(PQ quad)
        {
            // Clean up terrain shader
            if (hasTerrainShader && terrainQuadData.ContainsKey(quad))
            {
                terrainQuadData[quad].Cleanup();
                terrainQuadData.Remove(quad);
            }

            // Clean up scatter system
            //if (hasScatters && scatterQuadData.ContainsKey(quad))
            //{
            //    scatterQuadData[quad].Cleanup();
            //    scatterQuadData.Remove(quad);
            //}
        }
        public override void OnVertexBuild(PQS.VertexBuildData data)
        {
            //if (!hasScatters) { return; }

            // This check should not be needed, as the data is always added if we have scatters
            // Unless of course, quad subdivision is too low
            //ScatterSystemQuadData scatterData = scatterQuadData[data.buildQuad];
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
