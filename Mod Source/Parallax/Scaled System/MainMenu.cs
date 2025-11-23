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
using UnityEngine.Rendering;
using static VehiclePhysics.Retarder;
using Unity.Mathematics;
using KSP.UI;

namespace Parallax.Scaled_System
{
     /*
     * I want to keep the main menu separate from the rest of the mod to ensure there's no undefined behaviour caused by going to/from/to the main menu
     * which would be very difficult to debug
     * 
     * So this file reuses some code from the rest of the mod to isolate it entirely
     */

    /// <summary>
    /// Main menu scene is different so we need to handle it differently.
    /// Inherits a lot from ScaledManager and RaymarchedShadows with adaptations
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class ScaledMainMenu : MonoBehaviour
    {
        int SkyboxRotationShaderParam = Shader.PropertyToID("_SkyboxRotation");
        GameObject mainMenuGalaxy;
        GameObject mainMenuPlanet;
        ParallaxScaledBody mainMenuBody;
        Material scaledMaterial;
        Texture2D blueNoiseTexture;
        Camera mainCamera;

        // For skybox
        Vector3 rotation = new Vector3(0, 180, 0);

        // For sun rotation on menu transition
        Vector3 firstMenuSunDirection = new Vector3(0.7f, -0.4f, -0.6f);
        Vector3 secondMenuSunDirection = new Vector3(0.822f, -0.521f, -0.229f);
        Vector3 targetSunDirection = new Vector3(0.822f, -0.521f, -0.229f);
        Vector3 initialSunDirection = new Vector3(0.7f, -0.4f, -0.6f);
        float sunRotationProgress = 0.0f;
        float sunRotationProgressTarget = 0.0f;
        float sunRotationVelocity = 0.0f;

        //
        //  Shadows
        //

        Material compositorMaterial;

        // Stores shadow attenuation, 0 or 1
        RenderTexture shadowAttenuationRT;

        // Stores ray distance for umbra/penumbra calcs, float
        RenderTexture shadowDistanceRT;

        // Stores depth for the meshes being rendered into it
        RenderTexture shadowObjectDepthRT;

        CommandBuffer shadowCommandBuffer;
        CommandBuffer lightCommandBuffer;

        MeshRenderer meshRenderer;

        // Original shadow distance
        float originalShadowDistance = 0;

        Light mainLight;
        Light planetLight;
        Light fillLight;

        /// <summary>
        /// Stores the scaled space planet meshes, rendered using DrawMesh for the shadows pass
        /// </summary>
        public static List<Mesh> scaledPlanetMeshes = new List<Mesh>();
        void Start()
        {
            mainMenuGalaxy = GameObject.Find("MainMenuGalaxy");
            //mainLight = RenderSettings.sun;
            originalShadowDistance = QualitySettings.shadowDistance;

            MainMenu mainMenu = FindObjectOfType<MainMenu>();
            mainMenu.startBtn.onTap += new Callback(MenuAdvanceForwards);
            mainMenu.backBtn.onTap += new Callback(MenuAdvanceBackwards);

            StartCoroutine(Init());
        }
        void MenuAdvanceForwards()
        {
            //targetSunDirection = secondMenuSunDirection;
            sunRotationProgressTarget = 1;
        }
        void MenuAdvanceBackwards()
        {
            //targetSunDirection = firstMenuSunDirection;
            //initialSunDirection = planetLight.transform.forward;
            sunRotationProgressTarget = 0;
        }
        IEnumerator Init()
        {
            int delay = 6;
            for (int i = 0; i < delay; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            planetLight = GameObject.Find("PlanetLight").GetComponent<Light>();
            fillLight = GameObject.Find("FillLight").GetComponent<Light>();
            mainCamera = Camera.main;

            MainMenu main = FindObjectOfType<MainMenu>();
            MainMenuEnvLogic logic = main.envLogic;

            // Get Space
            GameObject space = logic.areas[1];
            bool locatedScaledPlanet = false;

            foreach (Component go in space.transform)
            {
                if (go.gameObject != null)
                {
                    if (ConfigLoader.parallaxScaledBodies.ContainsKey(go.name) && go.gameObject.activeSelf)
                    {
                        mainMenuPlanet = go.gameObject;
                        locatedScaledPlanet = true;
                        mainMenuBody = ConfigLoader.parallaxScaledBodies[go.name];
                    }
                }
            }

            if (!locatedScaledPlanet)
            {
                ParallaxDebug.Log("Unable to find a Scaled planet in the main menu scene, bye!");
                yield break;
            }

            // Initial rotation
            mainMenuPlanet.transform.rotation = new Quaternion(0, 0.4f, 0, 0.9f);

            // Should really check against 'using stock mesh' but disable deformity will achieve the same thing
            if (!mainMenuBody.disableDeformity)
            {
                mainMenuPlanet.GetComponent<MeshFilter>().sharedMesh = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("ParallaxContinued/Models/ScaledMesh").GetComponent<MeshFilter>().mesh);
            }
            else
            {
                ParallaxDebug.Log("Main menu planet has deformity disabled");
            }

            StartupMainMenuScaledBody(mainMenuPlanet, mainMenuPlanet.name);

            // Sun

            // Really pleasing main menu sun direction
            Vector3 direction = new Vector3(0.7f, -0.4f, -0.6f);

            planetLight.transform.forward = direction;
            fillLight.transform.forward = direction;
            RenderSettings.sun.transform.forward = direction;

            //
            //  Shadows
            //

            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                yield break;
            }

            meshRenderer = mainMenuPlanet.GetComponent<MeshRenderer>();

            // Ensure the screenspace mask is generated
            fillLight.shadows = LightShadows.Soft;
            fillLight.shadowStrength = 1;
            fillLight.lightShadowCasterMode = LightShadowCasterMode.Everything;
            fillLight.shadowResolution = LightShadowResolution.VeryHigh;

            // Set up command buffers
            shadowCommandBuffer = new CommandBuffer { name = "Render Custom Shadows" };
            lightCommandBuffer = new CommandBuffer { name = "Composite Shadows" };

            // Render path
            if (mainCamera.renderingPath == RenderingPath.DeferredShading)
            {
                mainCamera.AddCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
            }
            else
            {
                mainCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, shadowCommandBuffer);
            }

            // Set up RT descs
            RenderTextureDescriptor attenuationDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.R8, 16);
            attenuationDesc.useMipMap = true;
            attenuationDesc.autoGenerateMips = true;

            RenderTextureDescriptor distancesDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.RFloat, 0);
            distancesDesc.useMipMap = true;
            distancesDesc.autoGenerateMips = true;

            RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Depth, 16);
            depthDesc.useMipMap = false;

            // Setup RTs
            shadowAttenuationRT = new RenderTexture(attenuationDesc);
            shadowDistanceRT = new RenderTexture(distancesDesc);
            shadowObjectDepthRT = new RenderTexture(depthDesc);

            shadowAttenuationRT.Create();
            shadowDistanceRT.Create();
            shadowObjectDepthRT.Create();

            // Setup materials
            compositorMaterial = new Material(AssetBundleLoader.parallaxScaledShaders["Custom/PostProcessShadows"]);

            compositorMaterial.SetTexture("_MainTex", shadowAttenuationRT);
            compositorMaterial.SetTexture("_ShadowDistances", shadowDistanceRT);
            compositorMaterial.SetTexture("_ShadowDepth", shadowObjectDepthRT);

            lightCommandBuffer.Blit(shadowAttenuationRT, BuiltinRenderTextureType.CurrentActive, compositorMaterial);

            fillLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);

            QualitySettings.shadowDistance = 10000.0f;

            mainMenuPlanet.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            mainMenuPlanet.GetComponent<MeshRenderer>().receiveShadows = true;
        }
        public void StartupMainMenuScaledBody(GameObject go, string name)
        {
            if (ConfigLoader.parallaxScaledBodies.ContainsKey(name))
            {
                ParallaxScaledBody body = ConfigLoader.parallaxScaledBodies[name];
                CelestialBody kspBody = FlightGlobals.GetBodyByName(name);
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();

                body.Load();

                SetScaledMaterialParams(body, kspBody, go);

                meshRenderer.sharedMaterial = scaledMaterial;
                meshRenderer.sharedMaterial.SetShaderPassEnabled("ForwardAdd", false); 
                meshRenderer.sharedMaterial.SetShaderPassEnabled("ForwardBase", true); 
                meshRenderer.sharedMaterial.SetShaderPassEnabled("ShadowCaster", true); 
                meshRenderer.sharedMaterial.SetShaderPassEnabled("Deferred", false); 
            }
        }
        // Adapted from ParallaxScaledBody.SetScaledMaterialParams
        // Easier to copy it here than change the existing function to avoid introducing inaccuracies. I prefer the main menu scene to remain isolated from everything else
        // Anything set by this function is overwritten later
        public void SetScaledMaterialParams(ParallaxScaledBody body, CelestialBody kspBody, GameObject go)
        {
            scaledMaterial = Instantiate(body.scaledMaterial);

            float _PlanetRadius = (float)kspBody.Radius;
            float _MinAltitude = body.minTerrainAltitude;
            float _MaxAltitude = body.maxTerrainAltitude;

            float _MeshRadius = GetMeshRadiusScaledSpace(go);

            float scalingFactor = _MeshRadius / _PlanetRadius;

            scaledMaterial.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
            scaledMaterial.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);

            // Terrain shader specific
            if (body.mode == ParallaxScaledBodyMode.FromTerrain || body.mode == ParallaxScaledBodyMode.CustomRequiresTerrain)
            {
                scaledMaterial.SetFloat("_LowMidBlendStart", (body.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendStart"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_LowMidBlendEnd", (body.scaledMaterialParams.shaderProperties.shaderFloats["_LowMidBlendEnd"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_MidHighBlendStart", (body.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendStart"] + _PlanetRadius) * scalingFactor);
                scaledMaterial.SetFloat("_MidHighBlendEnd", (body.scaledMaterialParams.shaderProperties.shaderFloats["_MidHighBlendEnd"] + _PlanetRadius) * scalingFactor);

                // Required for land mask
                scaledMaterial.SetFloat("_WorldPlanetRadius", _MeshRadius);
            }

            if (body.disableDeformity)
            {
                scaledMaterial.SetInt("_MaxTessellation", 1);
                scaledMaterial.SetFloat("_TessellationEdgeLength", 50.0f);
                scaledMaterial.SetInt("_DisableDisplacement", 1);
            }
            else
            {
                scaledMaterial.SetInt("_DisableDisplacement", 0);
            }

            // Setup shadow caster - must set this manually
            body.shadowCasterMaterial.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
            body.shadowCasterMaterial.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);
            body.shadowCasterMaterial.SetFloat("_WorldPlanetRadius", _MeshRadius);
            body.shadowCasterMaterial.SetFloat("_ScaleFactor", scalingFactor);
            body.shadowCasterMaterial.SetInt("_DisableDisplacement", body.disableDeformity ? 1 : 0);

            if (body.scaledMaterialParams.shaderKeywords.Contains("OCEAN") || body.scaledMaterialParams.shaderKeywords.Contains("OCEAN_FROM_COLORMAP"))
            {
                // Colormap doesn't matter for the shadow caster, just enable one of them
                body.shadowCasterMaterial.EnableKeyword("OCEAN");
                body.shadowCasterMaterial.SetFloat("_OceanAltitude", body.scaledMaterialParams.shaderProperties.shaderFloats["_OceanAltitude"]);
            }

            if (ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.smoothScaledSpaceShadows)
            {
                body.shadowCasterMaterial.EnableKeyword("BLUE_NOISE");
            }
            else
            {
                body.shadowCasterMaterial.DisableKeyword("BLUE_NOISE");
            }

            // Load the blue noise texture - uncompressed DDS Luminance 8, linear
            var options = new TextureLoadOptions
            {
                linear = true,
                unreadable = true
            };
            blueNoiseTexture = TextureLoadManager
                .LoadTexture("ParallaxContinued/Textures/PluginData/blueNoise.dds", options)
                .Leak();
            body.shadowCasterMaterial.SetTexture("_BlueNoise", blueNoiseTexture);

            // Computed the max shadow ray distance
            float worldDistance = body.maxTerrainAltitude * scalingFactor + _MeshRadius;
            float theta = Mathf.Asin(_MeshRadius / worldDistance);
            float tangentDist = _MeshRadius / Mathf.Tan(theta);

            body.shadowCasterMaterial.SetFloat("_MaxRayDistance", tangentDist);

            // Computed at runtime, but the default is computed from Kerbin's SMA around the Sun
            body.shadowCasterMaterial.SetFloat("_LightWidth", 0.0384f);

            // Setup environment
            scaledMaterial.SetTexture("_Skybox", SkyboxControl.cubeMap);
        }
        public float GetMeshRadiusScaledSpace(GameObject go)
        {
            float localMeshRadius = 1000.0f;
            Vector3 meshCenter = Vector3.zero;
            Vector3 arbitraryMeshBound = Vector3.up * localMeshRadius;

            float radius = Vector3.Distance(go.transform.TransformPoint(meshCenter), go.transform.TransformPoint(arbitraryMeshBound));
            return radius;
        }
        public void RenderShadows()
        {
            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                return;
            }

            shadowCommandBuffer.Clear();

            // Setup RTs
            shadowCommandBuffer.SetRenderTarget(new RenderTargetIdentifier[] { shadowAttenuationRT, shadowDistanceRT }, shadowObjectDepthRT);

            // Clear target attenuation to 1 and depth to 1
            shadowCommandBuffer.ClearRenderTarget(true, true, Color.white);

            // Only render what we can see and is loaded
            shadowCommandBuffer.SetGlobalMatrix("_WorldRotation", Matrix4x4.Inverse(Matrix4x4.Rotate(meshRenderer.localToWorldMatrix.rotation)));
            shadowCommandBuffer.DrawRenderer(meshRenderer, mainMenuBody.shadowCasterMaterial, 0);

            // Set planet opacity
            shadowCommandBuffer.SetGlobalFloat("_ScaledPlanetOpacity", 1.0f);
        }
        public void Update()
        {
            if (mainMenuPlanet == null)
            {
                return;
            }

            // Planet controls
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.RightArrow))
            {
                mainMenuPlanet.transform.Rotate(Vector3.up, 5.0f);
                Debug.Log("Main planet rotation: " + mainMenuPlanet.transform.rotation.ToString("F3"));
            }
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                mainMenuPlanet.transform.Rotate(Vector3.up, -5.0f);
                Debug.Log("Main planet rotation: " + mainMenuPlanet.transform.rotation.ToString("F3"));
            }

            // Uncomment for light controls

            //if (Input.GetKeyDown(KeyCode.I))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward - new Vector3(0, 0.05f, 0));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward - new Vector3(0, 0.05f, 0));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward - new Vector3(0, 0.05f, 0));
            //}
            //if (Input.GetKeyDown(KeyCode.K))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward + new Vector3(0, 0.05f, 0));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward + new Vector3(0, 0.05f, 0));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward + new Vector3(0, 0.05f, 0));
            //}
            //
            //if (Input.GetKeyDown(KeyCode.J))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward + new Vector3(0.05f, 0, 0));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward + new Vector3(0.05f, 0, 0));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward + new Vector3(0.05f, 0, 0));
            //}
            //if (Input.GetKeyDown(KeyCode.L))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward - new Vector3(0.05f,0, 0));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward - new Vector3(0.05f,0, 0));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward - new Vector3(0.05f,0, 0));
            //}
            //
            //if (Input.GetKeyDown(KeyCode.A))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward + new Vector3(0, 0, 0.05f));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward + new Vector3(0, 0, 0.05f));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward + new Vector3(0, 0, 0.05f));
            //}
            //if (Input.GetKeyDown(KeyCode.D))
            //{
            //    planetLight.transform.forward = Vector3.Normalize(planetLight.transform.forward - new Vector3(0, 0, 0.05f));
            //    fillLight.transform.forward = Vector3.Normalize(fillLight.transform.forward - new Vector3(0, 0, 0.05f));
            //    RenderSettings.sun.transform.forward = Vector3.Normalize(RenderSettings.sun.transform.forward - new Vector3(0, 0, 0.05f));
            //}

            // Send inverse of skybox rotation to shader
            Shader.SetGlobalMatrix(SkyboxRotationShaderParam, Matrix4x4.Rotate(Quaternion.Euler(rotation - mainMenuGalaxy.transform.rotation.eulerAngles)));

            sunRotationProgress = Mathf.SmoothDamp(sunRotationProgress, sunRotationProgressTarget, ref sunRotationVelocity, 2.2f, 1.0f, Time.deltaTime);

            planetLight.transform.forward = Vector3.Slerp(initialSunDirection, targetSunDirection, sunRotationProgress);
            fillLight.transform.forward = Vector3.Slerp(initialSunDirection, targetSunDirection, sunRotationProgress);
            RenderSettings.sun.transform.forward = Vector3.Slerp(initialSunDirection, targetSunDirection, sunRotationProgress);

            RenderShadows();
            DebugShadows();
        }
        bool showShadowAttenuation = false;
        bool addedShadowDebug = false;
        void DebugShadows()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha7))
            {
                showShadowAttenuation = !showShadowAttenuation;
            }
            if (showShadowAttenuation)
            {
                if (!addedShadowDebug)
                {
                    addedShadowDebug = true;
                    ShadowDebug shadowDebug = mainCamera.gameObject.AddComponent<ShadowDebug>();
                    shadowDebug.shadowTex = shadowAttenuationRT;
                }
            }
            if (!showShadowAttenuation)
            {
                if (addedShadowDebug)
                {
                    Destroy(mainCamera.gameObject.GetComponent<ShadowDebug>());
                    addedShadowDebug = false;
                }
            }
        }
        void OnDisable()
        {
            QualitySettings.shadowDistance = originalShadowDistance;
            if (mainMenuBody != null)
            {
                mainMenuBody.Unload();
            }

            UnityEngine.Object.Destroy(compositorMaterial);
            UnityEngine.Object.Destroy(scaledMaterial);
            UnityEngine.Object.Destroy(blueNoiseTexture);

            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                return;
            }

            if (mainCamera != null && mainCamera.renderingPath == RenderingPath.DeferredShading)
            {
                mainCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
            }
            else if (mainCamera != null)
            {
                mainCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, shadowCommandBuffer);
            }

            fillLight?.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);

            lightCommandBuffer.Dispose();
            shadowCommandBuffer.Dispose();
            shadowAttenuationRT.Release();
            shadowDistanceRT.Release();
            shadowObjectDepthRT.Release();
        }
    }
}
