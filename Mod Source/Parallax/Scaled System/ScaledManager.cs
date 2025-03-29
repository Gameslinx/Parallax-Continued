using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static System.Net.Mime.MediaTypeNames;

namespace Parallax.Scaled_System
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ScaledManager : MonoBehaviour
    {
        public static int SkyboxRotationShaderParam = Shader.PropertyToID("_SkyboxRotation");
        public static ScaledManager Instance;

        Vector3 rotation = new Vector3(0, 180, 0);

        /// <summary>
        /// Stores the scaled space planet meshes, rendered using DrawMesh for the shadows pass
        /// </summary>
        public static List<Mesh> scaledPlanetMeshes = new List<Mesh>();
        public void Awake()
        {
            ParallaxDebug.Log("Scaled manager awake");
            if (!HighLogic.LoadedSceneIsFlight && !(HighLogic.LoadedScene == GameScenes.TRACKSTATION) && !(HighLogic.LoadedScene == GameScenes.MAINMENU))
            {
                ParallaxDebug.Log("Scaled manager destroyed - not in the right scene");
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

                // This returns a copy of the materials on the mesh
                Material[] mats = meshRenderer.sharedMaterials;

                // Get the vanilla material (filter out scatterer and eve)
                int materialIndex = GetScaledMaterialIndex(mats);

                mats[materialIndex] = body.scaledMaterial;

                meshRenderer.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// Returns the vanilla scaled space material index - scatterer and EVE both add materials to the mesh renderer
        /// </summary>
        /// <param name="meshRenderer"></param>
        /// <returns></returns>
        public int GetScaledMaterialIndex(Material[] sharedMaterials)
        {
            // Filter out "Scatterer/" and "Eve/" and return the first material index that is not those
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (!sharedMaterials[i].name.Contains("Scatterer/") && !sharedMaterials[i].name.Contains("Eve/"))
                {
                    return i;
                }
            }

            // We didn't find anything
            ParallaxDebug.LogCritical("Unable to locate the scaled space material for a planet! Please contact the mod author with your KSP.log file");
            foreach (Material mat in sharedMaterials)
            {
                ParallaxDebug.LogError("Dumping Material Name: " + mat.name);
            }

            // Hope and pray - return whatever the default material is
            return 0;
        }
        /// <summary>
        /// We need to preserve the reference to scatterer's eclipse material, which somehow is not possible using sharedMaterials...
        /// So do this the hard way
        /// </summary>
        /// <param name="planetName"></param>
        /// <param name="sharedMaterials"></param>
        /// <returns></returns>
        public int GetScattererEclipseMaterial(string planetName, Material[] sharedMaterials, out Material eclipseCasterMaterial)
        {
            // Use reflection to find the scatterer eclipse material

            // I need a reference to the class 'Scatterer' which has a static field of type 'ConfigReader' of name 'instance'
            // Gets type Scatterer from namespace Scatterer in assembly Scatterer
            Type scattererType = Type.GetType("Scatterer.Scatterer, scatterer");

            // Scatterer is not installed
            if (scattererType == null)
            {
                eclipseCasterMaterial = null;
                return -1;
            }

            // Now get the field 'instance' from the type 'scatterer'
            object scattererInstance = scattererType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);

            if (scattererInstance == null)
            {
                ParallaxDebug.LogCritical("Scatterer instance is null! Ensure Scatterer is initialized.");
            }

            // Now get the 'planetsConfigsReader' of type 'ConfigReader' from the instance of 'scatterer'
            object planetsConfigsReader = scattererType.GetField("planetsConfigsReader").GetValue(scattererInstance);

            if (planetsConfigsReader == null)
            {
                ParallaxDebug.LogCritical("Unable to locate the scatterer planets config reader! Please contact the mod author with your KSP.log file");
            }

            // Now get the public list 'scattererCelestialBodies' from the config reader
            // The list contains objects of type 'ScattererCelestialBody'

            IEnumerable scattererCelestialBodiesEnumerable = (IEnumerable)planetsConfigsReader.GetType().GetField("scattererCelestialBodies").GetValue(planetsConfigsReader);

            List<object> scattererCelestialBodies = scattererCelestialBodiesEnumerable.Cast<object>().ToList();

            object scattererBody = null;
            foreach (object scattererCelestialBody in scattererCelestialBodies)
            {
                string celestialBodyName = (string)scattererCelestialBody.GetType().GetField("celestialBodyName").GetValue(scattererCelestialBody);
                Debug.Log("CBN: " + celestialBodyName + ", searching for: " + planetName);
                if (celestialBodyName == planetName)
                {
                    Debug.Log("Found scatterer body");
                    scattererBody = scattererCelestialBody;
                }
            }

            // Sanity check
            if (scattererBody == null)
            {
                Debug.Log("Inactive");
                Debug.Log("Scatterer body is null? " + (scattererBody == null));
                eclipseCasterMaterial = null;
                return -1;
            }

            // Now get the public 'prolandManager' of type 'ProlandManager' from the scatterer body
            Debug.Log("scatterer eclipse debug, type: " + scattererBody.GetType().Name);
            object prolandManager = scattererBody.GetType().GetField("prolandManager").GetValue(scattererBody);

            if (prolandManager == null)
            {
                // Inactive
                Debug.Log("Proland manager is null");
                eclipseCasterMaterial = null;
                return -1;
                //ParallaxDebug.LogCritical("Unable to locate the proland manager for the planet " + planetName + "! Please contact the mod author with your KSP.log file");
            }

            // Now get the 'skyNode' of type 'SkyNode' from the proland manager
            object skyNode = prolandManager.GetType().GetField("skyNode").GetValue(prolandManager);

            // Finally get the public scaledEclipseMaterial of type 'Material' from the sky node, and cry after this whole ordeal
            Material scaledEclipseMaterial = (Material)skyNode.GetType().GetField("scaledEclipseMaterial").GetValue(skyNode);

            if (scaledEclipseMaterial == null)
            {
                ParallaxDebug.LogCritical("Unable to locate the scatterer eclipse material for the planet " + planetName + "! Please contact the mod author with your KSP.log file");
            }

            eclipseCasterMaterial = scaledEclipseMaterial;

            // Find the index using the shader name, because the references might not match
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Debug.Log("Checking materials: " + sharedMaterials[i].shader.name + ", compared with eclipse caster shader name: " + eclipseCasterMaterial.shader.name);
                Material mat = sharedMaterials[i];
                if (mat.shader.name == eclipseCasterMaterial.shader.name)
                {
                    return i;
                }
            }

            return -1;
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
