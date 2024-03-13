using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.ConfigParser.Enumerations;
using Kopernicus.Configuration.ModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallax
{
    public class PQSMod_Parallax : PQSMod
    {
        public int subdivisionLevel = 1;
        public float subdivisionRadius = 100.0f;
        public static Dictionary<PQ, TerrainShaderQuadData> quadData = new Dictionary<PQ, TerrainShaderQuadData>();
        public override void OnQuadBuilt(PQ quad)
        {
            // Add the terrain shader
            TerrainShaderQuadData data = new TerrainShaderQuadData(quad, subdivisionLevel, subdivisionRadius, quad.subdivision == quad.sphereRoot.maxLevel);
            data.Initialize();
            quadData.Add(quad, data);
        }
        public override void OnQuadDestroy(PQ quad)
        {
            if (quadData.ContainsKey(quad))
            {
                quadData[quad].Cleanup();
                quadData.Remove(quad);
            }
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
