using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class RuntimeOperations : MonoBehaviour
    {
        public delegate void FlightReflectionProbeReady(Transform t);
        public static event FlightReflectionProbeReady onFlightReflectionProbeReady;

        public static GameObject flightProbeObject;
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
            if (EventHandler.currentParallaxBody != null && FlightGlobals.currentMainBody != null)
            {
                SetParallaxMaterialVars(EventHandler.currentParallaxBody.parallaxMaterials);
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
