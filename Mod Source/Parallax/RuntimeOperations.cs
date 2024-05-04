using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class RuntimeOperations : MonoBehaviour
    {
        public delegate void FlightReflectionProbeReady(Transform t);
        public static event FlightReflectionProbeReady onFlightReflectionProbeReady;

        public static GameObject flightProbeObject;

        public delegate void DominantBodyChanged(CelestialBody body);
        public static event DominantBodyChanged onDominantBodyChanged;

        // Used in mesh subdivision frustum culling
        public static ParallaxPlane[] cameraFrustumPlanes = new ParallaxPlane[6];

        // Used in scatter system frustum culling - Shader does NOT want to accept ParallaxPlane[] so we'll use the slightly faster float version
        public static float[] floatCameraFrustumPlanes = new float[24];
        public static float3 cameraPos = float3.zero;
        public static Vector3 vectorCameraPos = Vector3.zero;
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
            if (EventHandler.currentParallaxBody != null && FlightGlobals.currentMainBody != null)
            {
                SetParallaxMaterialVars(EventHandler.currentParallaxBody.parallaxMaterials);
                if (onDominantBodyChanged != null)
                {
                    onDominantBodyChanged(FlightGlobals.currentMainBody);
                }
            }

            // Get the camera position
            Camera cam = Camera.allCameras.FirstOrDefault(_cam => _cam.name == "Camera 00");
            if (cam != null)
            {
                cameraPos = cam.gameObject.transform.position;
                vectorCameraPos = cam.gameObject.transform.position;
            }
            else
            {
                return;
            }

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
        Vector3 terrainShaderOffset;
        Vector3 bodyPosition;
        float bodyRadius;
        public void SetParallaxMaterialVars(ParallaxMaterials materialSet)
        {
            terrainShaderOffset = FloatingOrigin.TerrainShaderOffset;
            bodyPosition = FlightGlobals.currentMainBody.gameObject.transform.position;
            bodyRadius = (float)FlightGlobals.currentMainBody.Radius;

            materialSet.parallaxLow.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxLow.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxLow.SetFloat("_PlanetRadius", bodyRadius);

            materialSet.parallaxMid.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxMid.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxMid.SetFloat("_PlanetRadius", bodyRadius);

            materialSet.parallaxHigh.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxHigh.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxHigh.SetFloat("_PlanetRadius", bodyRadius);

            materialSet.parallaxLowMid.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxLowMid.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxLowMid.SetFloat("_PlanetRadius", bodyRadius);

            materialSet.parallaxMidHigh.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxMidHigh.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxMidHigh.SetFloat("_PlanetRadius", bodyRadius);

            materialSet.parallaxFull.SetVector("_PlanetOrigin", bodyPosition);
            materialSet.parallaxFull.SetVector("_ShaderOffset", terrainShaderOffset);
            materialSet.parallaxFull.SetFloat("_PlanetRadius", bodyRadius);
        }
    }
}
