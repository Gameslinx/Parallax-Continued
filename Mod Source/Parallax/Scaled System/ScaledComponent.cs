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
                    StartCoroutine(scaledBody.LoadAsync());
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
}
