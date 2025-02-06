using Kopernicus.OnDemand;
using Kopernicus.RuntimeUtility;
using Kopernicus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections;
using Kopernicus.Configuration;

namespace Parallax.Scaled_System
{
    /// <summary>
    /// Main menu scene is different so we need to handle it differently.
    /// Inherits a lot from ScaledManager with adaptations
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class ScaledMainMenu : MonoBehaviour
    {
        int SkyboxRotationShaderParam = Shader.PropertyToID("_SkyboxRotation");
        int PlanetOriginShaderParam = Shader.PropertyToID("_PlanetOrigin");
        int PlanetRadiusParam = Shader.PropertyToID("_PlanetRadius");

        Vector3 rotation = new Vector3(0, 180, 0);

        /// <summary>
        /// Stores the scaled space planet meshes, rendered using DrawMesh for the shadows pass
        /// </summary>
        public static List<Mesh> scaledPlanetMeshes = new List<Mesh>();
        void Start()
        {
            StartCoroutine(Init());
        }
        IEnumerator Init()
        {
            Debug.Log("Scaled main menu init");

            // light
            Light[] lights = (Light[])Light.FindObjectsOfType(typeof(Light));
            foreach (Light light in lights)
            {
                Debug.Log("Light: " + light.name);
                Debug.Log("- Light colour: " + light.color.ToString("F2"));
                Debug.Log("- Light color temp" + light.colorTemperature.ToString("F2"));
                Debug.Log("- Light intensity" + light.intensity);
                Debug.Log("- Light range" + light.range);
                //if (light.gameObject.name.Contains("FillLight") || light.gameObject.name.Contains("PlanetLight"))
                //{
                //    light.intensity = 0.9f;
                //    light.range = 0.0001f;
                //}
            }
            

            int delay = 6;
            for (int i = 0; i < delay; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            MainMenu main = FindObjectOfType<MainMenu>();
            MainMenuEnvLogic logic = main.envLogic;

            // Get Space
            GameObject space = logic.areas[1];
            GameObject mainMenuPlanet = null;

            foreach (Component go in space.transform)
            {
                if (go.gameObject != null)
                {
                    if (go.gameObject.name == "Kerbin" && go.gameObject.activeSelf)
                    {
                        mainMenuPlanet = go.gameObject;
                    }
                }
            }

            mainMenuPlanet.GetComponent<MeshFilter>().sharedMesh = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("ParallaxContinued/Models/ScaledMesh").GetComponent<MeshFilter>().mesh);
            StartupMainMenuScaledBody(mainMenuPlanet, mainMenuPlanet.name);
        }
        public void StartupMainMenuScaledBody(GameObject go, string name)
        {
            if (ConfigLoader.parallaxScaledBodies.ContainsKey(name))
            {
                ParallaxScaledBody body = ConfigLoader.parallaxScaledBodies[name];
                CelestialBody kspBody = FlightGlobals.GetBodyByName(name);
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();

                // Set up on demand component for texture loading/unloading
                ScaledOnDemandComponent onDemandManager = go.AddComponent<ScaledOnDemandComponent>();
                onDemandManager.scaledBody = body;
                onDemandManager.celestialBody = kspBody;

                body.SetScaledMaterialParams(kspBody);

                meshRenderer.sharedMaterial = body.scaledMaterial;
                meshRenderer.sharedMaterial.SetShaderPassEnabled("ForwardAdd", false); 
                meshRenderer.sharedMaterial.SetShaderPassEnabled("ForwardBase", true); 
            }
        }
        public void Update()
        {
            // Send inverse of skybox rotation to shader
            Shader.SetGlobalMatrix(SkyboxRotationShaderParam, Matrix4x4.Rotate(Quaternion.Euler(rotation - GalaxyCubeControl.Instance.transform.rotation.eulerAngles)));
        }
    }
}
