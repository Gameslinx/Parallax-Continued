using Kopernicus.Configuration;
using Parallax.Scaled_System;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parallax.Scaled_System
{
    public class ScaledOnDemandComponent : MonoBehaviour
    {
        public ParallaxScaledBody scaledBody;
        public CelestialBody celestialBody;

        public static float loadAngle = 0.02f;
        public static float unloadAngle = 0.005f;

        public bool pendingUnload = false;
        public static float unloadDelaySeconds = 10.0f;
        public float timeUnloadRequestedSeconds = -1;
        

        public Plane plane;

        void Start()
        {
            // Kick off immediately
            Update();
        }
        void Update()
        {
            // Calculate "size on screen" (really the angle subtended by the sphere radius)
            float halfAngle = CalculateAngleBetweenSphereAndCamera(ScaledSpace.LocalToScaledSpace(celestialBody.gameObject.transform.position), scaledBody.worldSpaceMeshRadius, ScaledCamera.Instance.cam);

            if (halfAngle > loadAngle)
            {
                // Prevent an unload if we just dipped into it and back out
                pendingUnload = false;
                if (!scaledBody.Loaded && !scaledBody.IsLoading)
                {
                    // We don't need to wait for this
                    scaledBody.Load();
                }
            }
            if (halfAngle < unloadAngle && FlightGlobals.currentMainBody != celestialBody)
            {
                if (scaledBody.Loaded)
                {
                    // Delay unload to prevent constant load/unload
                    if (!pendingUnload)
                    {
                        pendingUnload = true;
                        timeUnloadRequestedSeconds = Time.time;
                    }
                    if (Time.time - timeUnloadRequestedSeconds > unloadDelaySeconds)
                    {
                        pendingUnload = false;
                        scaledBody.Unload();
                    }
                }
            }
        }
        float CalculateAngleBetweenSphereAndCamera(Vector3 center, float radius, Camera cam)
        {

            // Get the camera position and up vector in world space
            Vector3 cameraPos = cam.transform.position;
            Vector3 worldUp = Vector3.up;

            Vector3 cameraToSphere = (center - cameraPos).normalized;

            // Handle edge case where we're looking down / up and cross product will fail
            if (Mathf.Abs(Vector3.Dot(worldUp, cameraToSphere)) > 0.95f)
            {
                worldUp = Vector3.right;
            }
            Vector3 orthogonalVector = Vector3.Normalize(Vector3.Cross(cameraToSphere, worldUp));

            // Calculate the point on the sphere directly "above" the center
            Vector3 pointOnSphere = center + radius * orthogonalVector;

            // Calculate the vector from the camera to the point on the sphere
            Vector3 cameraToPointOnSphere = Vector3.Normalize(pointOnSphere - cameraPos);

            // Calculate the angle between the two vectors
            float angleRadians = Vector3.Angle(cameraToSphere, cameraToPointOnSphere);

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
        public static ScaledManager Instance;
        public void Awake()
        {
            if (!HighLogic.LoadedSceneIsFlight && !(HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                Destroy(this);
            }
            Instance = this;
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

                body.SetScaledMaterialParams(kspBody);

                meshRenderer.material = body.scaledMaterial;
            }
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
