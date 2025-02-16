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
    /// <summary>
    /// Handles texture loading and planet specific runtime material params
    /// </summary>
    public class ScaledOnDemandComponent : MonoBehaviour
    {
        // Set immediately from component initialisation
        public ParallaxScaledBody scaledBody;
        public CelestialBody celestialBody;
        public CelestialBody parentStar;

        public static float loadAngle = 0.008f;
        public static float unloadAngle = 0.003f;
        public static float forceUnloadAngle = 0.00005f;

        public bool pendingUnload = false;
        public static float unloadDelaySeconds = 10.0f;
        public float timeUnloadRequestedSeconds = -1;
        

        public Plane plane;

        void Start()
        {
            // Locate the parent star for shadows
            ParallaxDebug.Log("Searching for parent star");
            CelestialBody parentBody = celestialBody.referenceBody;
            while (parentBody != null && !parentBody.isStar && parentBody != parentBody.referenceBody)
            {
                // Step up
                parentBody = parentBody.referenceBody;
            }

            if (parentBody != null)
            {
                parentStar = parentBody;
                Debug.Log("Located parent star: " + parentStar.name);
            }
            else
            {
                ParallaxDebug.Log("Warning: Unable to find the parent star for: " + celestialBody.name);
                ParallaxDebug.Log("Shadow penumbra angle will be defaulted");
            }

            // Kick off immediately
            Update();
        }
        void Update()
        {
            UpdateVisibility();
            UpdateShadows();
        }
        void UpdateVisibility()
        {
            // Calculate "size on screen" (really the angle subtended by the sphere radius)
            float angle = CalculateSubtendedAngle(ScaledSpace.LocalToScaledSpace(celestialBody.gameObject.transform.position), ScaledCamera.Instance.cam.transform.position, scaledBody.worldSpaceMeshRadius, ScaledCamera.Instance.cam.fieldOfView);
            if (angle > loadAngle)
            {
                // Prevent an unload if we just dipped into it and back out
                pendingUnload = false;
                if (!scaledBody.Loaded && !scaledBody.IsLoading)
                {
                    // We don't need to wait for this
                    if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.loadTexturesImmediately && !(HighLogic.LoadedScene == GameScenes.MAINMENU))
                    {
                        StartCoroutine(scaledBody.LoadAsync());
                    }
                    else
                    {
                        scaledBody.Load();
                    }
                }
            }
            if ((angle < unloadAngle || angle < forceUnloadAngle) && FlightGlobals.currentMainBody != celestialBody)
            {
                if (scaledBody.Loaded)
                {
                    // Delay unload to prevent constant load/unload
                    if (!pendingUnload)
                    {
                        pendingUnload = true;
                        timeUnloadRequestedSeconds = Time.time;
                    }
                    if (Time.time - timeUnloadRequestedSeconds > unloadDelaySeconds || angle < forceUnloadAngle)
                    {
                        if (angle < forceUnloadAngle)
                        {
                            ParallaxDebug.Log("Force unloading " + scaledBody.planetName);
                        }
                        pendingUnload = false;
                        scaledBody.Unload();
                    }
                }
            }
        }
        void UpdateShadows()
        {
            if (!scaledBody.Loaded)
            {
                return;
            }

            float penumbraAngle = CalculateSubtendedAngle(celestialBody.transform.position, parentStar.transform.position, (float)parentStar.Radius);

            scaledBody.shadowCasterMaterial.SetFloat("_LightWidth", penumbraAngle);
        }
        /// <summary>
        /// Calculates the angle between origin-to-target and target-to-targetradius
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <param name="targetRadius"></param>
        /// <returns></returns>
        float CalculateSubtendedAngle(Vector3 origin, Vector3 target, float targetRadius)
        {
            return Mathf.Atan2(targetRadius, (origin - target).magnitude) * 2.0f;
        }
        float CalculateSubtendedAngle(Vector3 origin, Vector3 target, float targetRadius, float fov)
        {
            float angle = Mathf.Atan2(targetRadius, (origin - target).magnitude) * 2.0f;

            // 90 degree FOV baseline
            // Scale inversely
            float fovScalingFactor = 90.0f / fov;

            // Naively scale the angle depending on fov
            // Not an accurate or perfect calculation but gets the job done
            return angle * fovScalingFactor;
        }
        void OnDisable()
        {
            ParallaxDebug.Log("Disabled " + celestialBody.bodyName);
            scaledBody?.Unload();
        }
    }
}
