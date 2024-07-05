using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class RuntimeOperations : MonoBehaviour
    {
        public delegate void FlightReflectionProbeReady(Transform t);
        public static event FlightReflectionProbeReady onFlightReflectionProbeReady;

        public static GameObject flightProbeObject;

        // Used in mesh subdivision frustum culling
        public static ParallaxPlane[] cameraFrustumPlanes = new ParallaxPlane[6];

        // Used in scatter system frustum culling - Shader does NOT want to accept ParallaxPlane[] so we'll use the slightly faster float version
        public static float[] floatCameraFrustumPlanes = new float[24];
        public static float3 cameraPos = float3.zero;
        public static Vector3 vectorCameraPos = Vector3.zero;

        int planetOpacityID = Shader.PropertyToID("_PlanetOpacity");
        int planetOriginID = Shader.PropertyToID("_PlanetOrigin");
        int shaderOffsetID = Shader.PropertyToID("_ShaderOffset");
        int planetRadiusID = Shader.PropertyToID("_PlanetRadius");

        // Used in most shaders
        /// <summary>
        /// The current world space position of the current main body. If the current main body is null, this is set to 0.
        /// </summary>
        public static Vector3 currentPlanetOrigin = Vector3.zero;
        Plane[] planes;
        public void Start()
        {
            flightProbeObject = GameObject.Find("Reflection Probe");
            FlightReflectionProbe probeComponent = flightProbeObject.GetComponent<FlightReflectionProbe>();
            probeComponent.probeComponent.size = Vector3.one * 5000;
            if (onFlightReflectionProbeReady != null)
            { 
                onFlightReflectionProbeReady(flightProbeObject.transform);
            }
        }
        public void Update()
        {
            // Determine a celestial body change
            Profiler.BeginSample("Parallax Runtime Op Main");
            if (EventHandler.currentParallaxBody != null && FlightGlobals.currentMainBody != null)
            {
                // Required global params
                Shader.SetGlobalVector(planetOriginID, FlightGlobals.currentMainBody.transform.position);
                Shader.SetGlobalVector(shaderOffsetID, (Vector3)FloatingOrigin.TerrainShaderOffset);
                Shader.SetGlobalFloat(planetRadiusID, (float)FlightGlobals.currentMainBody.Radius);
                Shader.SetGlobalFloat(planetOpacityID, FlightGlobals.currentMainBody.pqsController.surfaceMaterial.GetFloat(planetOpacityID));
            }
            if (FlightGlobals.currentMainBody != null)
            {
                currentPlanetOrigin = FlightGlobals.currentMainBody.transform.position;
            }
            else
            {
                currentPlanetOrigin = Vector3.zero;
            }

            // Get the camera position and frustum planes
            Camera cam = FlightCamera.fetch?.mainCamera;
            if (cam != null)
            {
                cameraPos = cam.gameObject.transform.position;
                vectorCameraPos = cam.gameObject.transform.position;
                SetCameraFrustumPlanes(cam);
            }
            else
            {
                vectorCameraPos = Vector3.zero;
                cameraPos = float3.zero;
            }
            Profiler.EndSample();
            // Update quad-camera distances
            //Profiler.BeginSample("Parallax Runtime Op Distances");
            //Dictionary<PQ, ScatterSystemQuadData>.ValueCollection quadData = ScatterComponent.scatterQuadData.Values;
            Profiler.BeginSample("Actually calculate distances");

            foreach (var data in ScatterComponent.scatterQuadData)
            {
                if (data.Value.quad.isVisible)
                {
                    data.Value.UpdateQuadCameraDistance(ref vectorCameraPos);
                }
            }

            //Profiler.EndSample();
            Profiler.EndSample();
        }

        void SetCameraFrustumPlanes(Camera cam)
        {
            // Calculate camera frustum planes for frustum culling
            planes = GeometryUtility.CalculateFrustumPlanes(cam);
            for (int i = 0; i < planes.Length; i++)
            {
                // Convert to ParallaxPlane
                cameraFrustumPlanes[i] = planes[i];
            }

            floatCameraFrustumPlanes = new float[planes.Length * 4];
            for (int i = 0; i < planes.Length; ++i)
            {
                floatCameraFrustumPlanes[i * 4 + 0] = planes[i].normal.x;
                floatCameraFrustumPlanes[i * 4 + 1] = planes[i].normal.y;
                floatCameraFrustumPlanes[i * 4 + 2] = planes[i].normal.z;
                floatCameraFrustumPlanes[i * 4 + 3] = planes[i].distance;
            }
        }
    }
}
