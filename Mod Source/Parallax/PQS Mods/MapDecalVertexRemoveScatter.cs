using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.ConfigParser.Enumerations;
using Kopernicus.Configuration.ModLoader;
using Kopernicus.Configuration.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.PQS_Mods
{
    /// <summary>
    /// PQSMod that disables scatters per-vertex based on a mask map
    /// </summary>
    public class PQSMod_MapDecalVertexRemoveScatter : PQSMod
    {
        /// <summary>
        /// Not allowed to have multiple block maps on the same quad - But we need to get this data to the scatter system
        /// </summary>
        public static Dictionary<PQ, PQSMod_MapDecalVertexRemoveScatter> maskedScattersPerQuad = new Dictionary<PQ, PQSMod_MapDecalVertexRemoveScatter>();
        public List<string> blockedScatters;

        public double radius;
        public Vector3 position;
        public float angle;
        public Texture2D colorMap;
        public MapSO debugColorMap;
        public bool debugShowDecal;
        private double inclusionAngle;
        private bool quadActive;
        public Vector3d normalisedPosition;
        private double quadAngle;
        private float maskValue;
        private Vector3d vertRot;
        public Quaternion rot;
        private float u;
        private float v;
        private void Reset()
        {
            radius = 100.0;
            position = Vector3.forward;
            angle = 0f;
            vertRot = Vector3.forward;
        }

        public override void OnSetup()
        {
            if (blockedScatters == null)
            {
                ParallaxDebug.LogCritical("PQSMod_MapDecalVertexRemoveScatter: No blocked scatters specified on planet: " + sphere.name);
            }

            requirements = (PQS.ModiferRequirements.MeshColorChannel);
            normalisedPosition = position.normalized;
            inclusionAngle = Math.Atan(radius / sphere.radius) * 4.0;
            rot = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.FromToRotation(normalisedPosition, Vector3.up);

            if (colorMap == null)
            {
                ParallaxDebug.LogCritical("MapDecalVertexRemoveScatter: No color map specified, planet: " + sphere.name);
                modEnabled = false;
            }
        }

        // Skip this quad if it isn't in the inclusion angle - Avoid processing quads that aren't likely to include the decal
        public override void OnQuadPreBuild(PQ quad)
        {
            quadAngle = Math.Acos(Vector3d.Dot(quad.positionPlanetRelative, normalisedPosition));
            if (quadAngle < inclusionAngle)
            {
                if (maskedScattersPerQuad.ContainsKey(quad))
                {
                    ParallaxDebug.LogCritical("PQSMod_MapDecalVertexRemoveScatter: Multiple MapDecalVertexRemoveScatter mods on the same quad! You must combine these decals into one if possible");
                    return;
                }
                maskedScattersPerQuad.Add(quad, this);
            }
        }
        public override void OnQuadDestroy(PQ quad)
        {
            quadAngle = Math.Acos(Vector3d.Dot(quad.positionPlanetRelative, normalisedPosition));
            if (quadAngle < inclusionAngle)
            {
                maskedScattersPerQuad.Remove(quad);
            }
        }
        public override void OnVertexBuild(PQS.VertexBuildData vertexBuildData)
        {
            if (!debugShowDecal)
            {
                return;
            }
            if (!quadActive)
            {
                if (debugShowDecal)
                {
                    vertexBuildData.vertColor = Color.black;
                }
                return;
            }
        
            if (sphere.isBuildingMaps)
            {
            	quadAngle = Math.Acos(Vector3d.Dot(vertexBuildData.directionFromCenter, normalisedPosition));
            	if (quadAngle > inclusionAngle)
            	{
            		return;
            	}
            }
        
            vertRot = rot * vertexBuildData.directionFromCenter;
            u = (float)((vertRot.x * sphere.radius / radius + 1.0) * 0.5);
            v = (float)((vertRot.z * sphere.radius / radius + 1.0) * 0.5);
        
            if (u > 1 || v > 1 || u < 0 || v < 0)
            {
                return;
            }

            if (debugColorMap != null)
            {
                maskValue = debugColorMap.GetPixelColor(u, v).g;
            }
            else
            {
                return;
            }
                
            vertexBuildData.vertColor = maskValue > 0.01f ? Color.green : Color.red;
        }

        public override void OnQuadBuilt(PQ quad)
        {
            quadActive = true;
        }
    }

    [RequireConfigType(ConfigType.Node)]
    public class MapDecalVertexRemoveScatter : ModLoader<PQSMod_MapDecalVertexRemoveScatter>
    {
        // Vec3 position
        [ParserTarget("position")]
        public Vector3Parser Position
        {
            get { return Mod.position; }
            set { Mod.position = value; }
        }
        // Lat Lon position
        [ParserTarget("Position")]
        public PositionParser Position2
        {
            set { Mod.position = value; }
        }
        [ParserTarget("debugShowDecal", Optional = true)]
        public NumericParser<Boolean> DebugShowDecal
        {
            get { return Mod.debugShowDecal; }
            set { Mod.debugShowDecal = value; }
        }
        [ParserTarget("colorMap")]
        public Texture2DParser ColorMap
        {
            get { return Mod.colorMap; }
            set { Mod.colorMap = value; }
        }
        [ParserTarget("debugColorMap", Optional = true)]
        public MapSOParserRGB<MapSO> DebugColorMap
        {
            get { return Mod.debugColorMap; }
            set { Mod.debugColorMap = value; }
        }
        [ParserTarget("angle")]
        public NumericParser<Single> Angle
        {
            get { return Mod.angle; }
            set { Mod.angle = value; }
        }
        [ParserTarget("radius", Optional = false)]
        public NumericParser<double> Radius
        {
            get { return Mod.radius; }
            set { Mod.radius = value; }
        }
        [ParserTarget("order", Optional = false)]
        public NumericParser<int> Order
        {
            get { return Mod.order; }
            set { Mod.order = value; }
        }
        [ParserTargetCollection("BlockedScatters", Key = "name", NameSignificance = NameSignificance.Key, Optional = false)]
        public List<StringCollectionParser> BlockedScatters
        { 
            get
            {
                return new List<StringCollectionParser>
                {
                    Mod.blockedScatters
                };
            }
            set
            {
                Mod.blockedScatters = value.SelectMany(v => v.Value).ToList();
                for (int i = 0; i < Mod.blockedScatters.Count; i++)
                {
                    Mod.blockedScatters[i] = Mod.sphere.name + "-" + Mod.blockedScatters[i];
                }
            }
        }
    }
}
