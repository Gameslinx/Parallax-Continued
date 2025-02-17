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
using static DishController;

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

        // Size in screen pixels to load or unload based on the current camera FOV
        public static float loadScreenSizePixels = 3.0f;
        public static float unloadScreenSizePixels = 1.5f;
        public static float forceUnloadScreenSizePixels = 0.5f;

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
            float sizePixels = CalculateScreenSizePixels(celestialBody.scaledBody.gameObject.transform.position, ScaledCamera.Instance.cam.gameObject.transform.position, (float)celestialBody.Radius, ScaledCamera.Instance.cam.fieldOfView);
            if (sizePixels > loadScreenSizePixels)
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
            if ((sizePixels < unloadScreenSizePixels || sizePixels < forceUnloadScreenSizePixels) && FlightGlobals.currentMainBody != celestialBody)
            {
                if (scaledBody.Loaded)
                {
                    // Delay unload to prevent constant load/unload
                    if (!pendingUnload)
                    {
                        pendingUnload = true;
                        timeUnloadRequestedSeconds = Time.time;
                    }
                    if (Time.time - timeUnloadRequestedSeconds > unloadDelaySeconds || sizePixels < forceUnloadScreenSizePixels)
                    {
                        if (sizePixels < forceUnloadScreenSizePixels)
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
        float CalculateScreenSizePixels(Vector3 origin, Vector3 target, float radius, float fovDegrees)
        {
            float fov = fovDegrees * Mathf.Deg2Rad;
            float d = Vector3.Distance(origin, target);
            float r = radius * ScaledSpace.InverseScaleFactor;

            float projRadius = (1.0f / Mathf.Tan(fov * 0.5f)) * r / Mathf.Sqrt(d * d - r * r);
            float screenPixels = projRadius * Screen.height;

            return screenPixels;
        }
        void OnDisable()
        {
            ParallaxDebug.Log("Disabled " + celestialBody.bodyName);
            scaledBody?.Unload();
        }
    }
}
