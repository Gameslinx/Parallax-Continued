using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// Stores the property and buffer IDs for the scatter system so we don't need to fetch them every frame
    /// </summary>
    public class ParallaxScatterShaderProperties
    {
        // Init property IDs - Distribute
        public static int populationMultiplierID =          Shader.PropertyToID("_PopulationMultiplier");
        public static int spawnChanceID =                   Shader.PropertyToID("_SpawnChance");
        public static int alignToTerrainNormalID =          Shader.PropertyToID("_AlignToTerrainNormal");
        public static int maxNormalDevianceID =             Shader.PropertyToID("_MaxNormalDeviance");
        public static int seedID =                          Shader.PropertyToID("_Seed");
        public static int minScaleID =                      Shader.PropertyToID("_MinScale");
        public static int maxScaleID =                      Shader.PropertyToID("_MaxScale");
        public static int scaleRandomnessID =               Shader.PropertyToID("_ScaleRandomness");
        public static int invertNoiseID =                   Shader.PropertyToID("_InvertNoise");
        public static int noiseOctavesID =                  Shader.PropertyToID("_NoiseOctaves");
        public static int noiseFrequencyID =                Shader.PropertyToID("_NoiseFrequency");
        public static int noiseLacunarityID =               Shader.PropertyToID("_NoiseLacunarity");
        public static int noiseSeedID =                     Shader.PropertyToID("_NoiseSeed");
        public static int noiseCutoffThresholdID =          Shader.PropertyToID("_NoiseCutoffThreshold");
        public static int steepPowerID =                    Shader.PropertyToID("_SteepPower");
        public static int steepContrastID =                 Shader.PropertyToID("_SteepContrast");
        public static int steepMidpointID =                 Shader.PropertyToID("_SteepMidpoint");
        public static int minAltitudeID =                   Shader.PropertyToID("_MinAltitude");
        public static int maxAltitudeID =                   Shader.PropertyToID("_MaxAltitude");
        public static int altitudeFadeRangeID =             Shader.PropertyToID("_AltitudeFadeRange");
        public static int rangeFadeStartID =                Shader.PropertyToID("_RangeFadeStart");
        public static int distributeFixedAltitudeID =       Shader.PropertyToID("_DistributeFixedAltitude");
        public static int fixedAltitudeID =                 Shader.PropertyToID("_FixedAltitude");
        public static int planetRadiusID =                  Shader.PropertyToID("_PlanetRadius");
        public static int numberOfBiomesID =                Shader.PropertyToID("_NumberOfBiomes");
        public static int localPlanetNormalID =             Shader.PropertyToID("_LocalPlanetNormal");
        public static int planetOriginID =                  Shader.PropertyToID("_PlanetOrigin");
        public static int worldToObjectMatrixID =           Shader.PropertyToID("_WorldToObjectMatrix");

        // Init property IDs - Evaluate
        public static int objectToWorldMatrixID =           Shader.PropertyToID("_ObjectToWorldMatrix");
        public static int planetNormalID =                  Shader.PropertyToID("_PlanetNormal");
        public static int cameraFrustumPlanesID =           Shader.PropertyToID("_CameraFrustumPlanes");
        public static int worldSpaceCameraPositionID =      Shader.PropertyToID("_WorldSpaceCameraPosition");
        public static int worldSpaceReferencePositionID =   Shader.PropertyToID("_WorldSpaceReferencePosition");
        public static int maxCountID =                      Shader.PropertyToID("_MaxCount");
        public static int cullRadiusID =                    Shader.PropertyToID("_CullRadius");
        public static int cullLimitID =                     Shader.PropertyToID("_CullLimit");
        public static int maxRangeID =                      Shader.PropertyToID("_MaxRange");
        public static int lod01SplitID =                    Shader.PropertyToID("_Lod01Split");
        public static int lod12SplitID =                    Shader.PropertyToID("_Lod12Split");

        // Init buffer IDs
        public static int parentTrisBufferID =              Shader.PropertyToID("triangles");
        public static int parentVertsBufferID =             Shader.PropertyToID("vertices");
        public static int parentColorsBufferID =            Shader.PropertyToID("colors");
        public static int lod0BufferID =                    Shader.PropertyToID("instancingDataLOD0");
        public static int lod1BufferID =                    Shader.PropertyToID("instancingDataLOD1");
        public static int lod2BufferID =                    Shader.PropertyToID("instancingDataLOD2");
        public static int objectLimitsBufferID =            Shader.PropertyToID("objectLimits");
        public static int positionsBufferID =               Shader.PropertyToID("positions");
        public static int transformsBufferID =              Shader.PropertyToID("transforms");
        public static int parentNormalsBufferID =           Shader.PropertyToID("normals");
        public static int parentUVsBufferID =               Shader.PropertyToID("uvs");
        public static int parentDirsBufferID =              Shader.PropertyToID("directionsFromCenter");

        // Init texture IDs
        public static int biomeMapTextureID =               Shader.PropertyToID("biomeMap");
        public static int scatterBiomesTextureID =          Shader.PropertyToID("scatterBiomes");
    }
}
