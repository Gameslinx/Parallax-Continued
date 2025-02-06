using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Scaled_System
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ScaledManager : MonoBehaviour
    {
        public static int SkyboxRotationShaderParam = Shader.PropertyToID("_SkyboxRotation");
        public static int PlanetOriginShaderParam = Shader.PropertyToID("_PlanetOrigin");
        public static int PlanetRadiusParam = Shader.PropertyToID("_PlanetRadius");
        public static ScaledManager Instance;

        Vector3 rotation = new Vector3(0, 180, 0);

        /// <summary>
        /// Stores the scaled space planet meshes, rendered using DrawMesh for the shadows pass
        /// </summary>
        public static List<Mesh> scaledPlanetMeshes = new List<Mesh>();
        public void Awake()
        {
            if (!HighLogic.LoadedSceneIsFlight && !(HighLogic.LoadedScene == GameScenes.TRACKSTATION) && !(HighLogic.LoadedScene == GameScenes.MAINMENU))
            {
                Destroy(this);
            }
            Instance = this;
        }
        public void Start()
        {
            Debug.Log("Scaled light init");
            Light[] lights = (Light[])Light.FindObjectsOfType(typeof(Light));
            foreach (Light light in lights)
            {
                    Debug.Log("Light located: " + light.name);
                    Debug.Log("- Light colour: " + light.color.ToString("F2"));
                    Debug.Log("- Light color temp" + light.colorTemperature.ToString("F2"));
                    Debug.Log("- Light intensity" + light.intensity);
            }

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
            Shader.SetGlobalMatrix(SkyboxRotationShaderParam, Matrix4x4.Rotate(Quaternion.Euler(rotation - GalaxyCubeControl.Instance.transform.rotation.eulerAngles)));
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
