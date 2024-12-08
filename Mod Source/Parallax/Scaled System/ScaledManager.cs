using Kopernicus.Configuration;
using Parallax.Scaled_System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using static FinePrint.ContractDefs;

namespace Parallax
{
    public class ScaledOnDemandComponent : MonoBehaviour
    {
        public ParallaxScaledBody scaledBody;
        public CelestialBody celestialBody;

        public Camera scaledCamera;
        public static float loadAngle = 0.05f;
        public static float unloadAngle = 0.01f;

        void Start()
        {
            scaledCamera = ScaledCamera.Instance.cam;
        }
        void Update()
        {
            // Calculate "size on screen" (really the angle subtended by the sphere radius)
            float halfAngle = CalculateAngleBetweenSphereAndCamera(ScaledSpace.LocalToScaledSpace(celestialBody.gameObject.transform.position), scaledBody.worldSpaceMeshRadius, ScaledCamera.Instance.galaxyCamera);

            if (halfAngle > 0.05f)
            {
                if (!scaledBody.loaded)
                {
                    scaledBody.Load();
                }
            }
            if (halfAngle < 0.01f)
            {
                if (scaledBody.loaded)
                {
                    scaledBody.Unload();
                }
            }
        }
        float CalculateAngleBetweenSphereAndCamera(Vector3 center, float radius, Camera cam)
        {
            // Get the camera position and up vector in world space
            Vector3 cameraPos = cam.transform.position;
            Vector3 cameraUp = cam.transform.up;

            // Calculate the point on the sphere directly "above" the center
            Vector3 pointOnSphere = center + radius * cameraUp;

            // Calculate the vector from the camera to the sphere center
            Vector3 cameraToCenter = center - cameraPos;

            // Calculate the vector from the camera to the point on the sphere
            Vector3 cameraToPointOnSphere = pointOnSphere - cameraPos;

            // Calculate the angle between the two vectors
            float angleRadians = Vector3.Angle(cameraToCenter, cameraToPointOnSphere);

            // Return the angle in degrees
            return angleRadians;
        }
        void OnDisable()
        {
            ParallaxDebug.Log("Disabled " + celestialBody.bodyName);
            scaledBody?.Unload();
        }
    }
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ScaledManager : MonoBehaviour
    {
        public static int SkyboxRotationShaderParam = Shader.PropertyToID("_SkyboxRotation");
        public static int PlanetOriginShaderParam = Shader.PropertyToID("_PlanetOrigin");
        public static int PlanetRadiusParam = Shader.PropertyToID("_PlanetRadius");
        public void Awake()
        {
            if (!HighLogic.LoadedSceneIsFlight && !(HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                Destroy(this);
            }
        }
        public void Start()
        {
            foreach (ParallaxScaledBody body in ConfigLoader.parallaxScaledBodies.Values)
            {
                CelestialBody kspBody = FlightGlobals.GetBodyByName(body.planetName);
                MeshRenderer meshRenderer = kspBody.scaledBody.GetComponent<MeshRenderer>();

                // Set up on demand component for texture loading/unloading
                ScaledOnDemandComponent onDemandManager = kspBody.scaledBody.AddComponent<ScaledOnDemandComponent>();
                onDemandManager.scaledBody = body;
                onDemandManager.celestialBody = kspBody;

                Material scaledMaterial = body.scaledMaterial;

                float _PlanetRadius = (float)kspBody.Radius;
                float _MinAltitude = body.minTerrainAltitude;
                float _MaxAltitude = body.maxTerrainAltitude;

                float _MeshRadius = GetMeshRadiusScaledSpace(kspBody);
                body.worldSpaceMeshRadius = _MeshRadius;

                float scalingFactor = _MeshRadius / _PlanetRadius;

                // Set these for all shaders, they might need them
                scaledMaterial.SetVector(PlanetOriginShaderParam, kspBody.scaledBody.transform.position);
                scaledMaterial.SetFloat(PlanetRadiusParam, _MeshRadius);

                scaledMaterial.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
                scaledMaterial.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);

                // Terrain shader specific
                if (body.mode == ParallaxScaledBodyMode.FromTerrain)
                {
                    scaledMaterial.SetFloat("_LowMidBlendStart", (3.0f + _PlanetRadius) * scalingFactor);
                    scaledMaterial.SetFloat("_LowMidBlendEnd", (36.0f + _PlanetRadius) * scalingFactor);
                    scaledMaterial.SetFloat("_MidHighBlendStart", (1150.0f + _PlanetRadius) * scalingFactor);
                    scaledMaterial.SetFloat("_MidHighBlendEnd", (1500.0f + _PlanetRadius) * scalingFactor);
                }
                
                // Setup environment
                scaledMaterial.SetTexture("_Skybox", SkyboxControl.cubeMap);

                meshRenderer.material = body.scaledMaterial;
            }
        }
        // Averages all vert distances from the planet to get the radius in scaled space
        // Potential optimization here - just calculate the scaled space mesh radius manually
        public float GetMeshRadiusScaledSpace(CelestialBody celestialBody)
        {
            float localMeshRadius = 1000.0f;
            Vector3 meshCenter = Vector3.zero;
            Vector3 arbitraryMeshBound = Vector3.up * localMeshRadius;

            float radius = Vector3.Distance(celestialBody.scaledBody.transform.TransformPoint(meshCenter), celestialBody.scaledBody.transform.TransformPoint(arbitraryMeshBound));
            return radius;
        }
        public void Update()
        {
            // Send inverse of skybox rotation to shader
            Shader.SetGlobalMatrix(SkyboxRotationShaderParam, Matrix4x4.Rotate(Quaternion.Euler(new Vector3(0, 180, 0) -GalaxyCubeControl.Instance.transform.rotation.eulerAngles)));
        }
        void OnDisable()
        {
            ParallaxDebug.Log("Scaled component shutting down");
            foreach (ParallaxScaledBody body in ConfigLoader.parallaxScaledBodies.Values)
            {
                CelestialBody kspBody = FlightGlobals.GetBodyByName(body.planetName);

                // Body loaded OnDisable for this component
                Destroy(kspBody.GetComponent<ScaledOnDemandComponent>());
            }
        }
    }
}
