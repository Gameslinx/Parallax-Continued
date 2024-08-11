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
        // Init property IDs
        public static int objectToWorldMatrixPropID =       Shader.PropertyToID("_ObjectToWorldMatrix");
        public static int planetNormalPropID =              Shader.PropertyToID("_PlanetNormal");
        public static int cameraFrustumPlanesPropID =       Shader.PropertyToID("_CameraFrustumPlanes");
        public static int worldSpaceCameraPositionPropID =  Shader.PropertyToID("_WorldSpaceCameraPosition");
        public static int maxCountPropID =                  Shader.PropertyToID("_MaxCount");

        // Init buffer IDs
        public static int parentTrisBufferID =              Shader.PropertyToID("triangles");
        public static int parentVertsBufferID =             Shader.PropertyToID("vertices");
        public static int lod0BufferID =                    Shader.PropertyToID("instancingDataLOD0");
        public static int lod1BufferID =                    Shader.PropertyToID("instancingDataLOD1");
        public static int lod2BufferID =                    Shader.PropertyToID("instancingDataLOD2");
        public static int objectLimitsBufferID =            Shader.PropertyToID("objectLimits");
        public static int positionsBufferID =               Shader.PropertyToID("positions");
    }
}
