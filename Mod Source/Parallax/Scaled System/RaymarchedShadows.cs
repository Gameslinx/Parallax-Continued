using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parallax.Scaled_System
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class RaymarchedShadowsRenderer : MonoBehaviour
    {
        /// <summary>
        /// The raymarched shadows manager. Can be null if scaledGlobalSettings.scaledSpaceShadows is false.
        /// </summary>
        public static RaymarchedShadowsRenderer Instance;

        // Meshes to draw
        public Mesh scaledMesh;

        Light mainLight;
        Material compositorMaterial;

        // Stores shadow attenuation, 0 or 1
        RenderTexture shadowAttenuationRT;

        // Stores ray distance for umbra/penumbra calcs, float
        RenderTexture shadowDistanceRT;

        // Stores depth for the meshes being rendered into it
        RenderTexture shadowObjectDepthRT;

        CommandBuffer shadowCommandBuffer;
        CommandBuffer lightCommandBuffer;

        public List<ParallaxScaledBody> scaledBodies = new List<ParallaxScaledBody>();
        public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        void Awake()
        {
            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            GameObject.DontDestroyOnLoad(this);
        }
        public void Start()
        {
            // Setup
            foreach (KeyValuePair<string, ParallaxScaledBody> pair in ConfigLoader.parallaxScaledBodies)
            {
                scaledBodies.Add(pair.Value);
                meshRenderers.Add(FlightGlobals.GetBodyByName(pair.Value.planetName).scaledBody.GetComponent<MeshRenderer>());
            }

            // Ensure the screenspace mask is generated
            mainLight = Sun.Instance.scaledSunLight;
            mainLight.shadows = LightShadows.Soft;
            mainLight.shadowStrength = 1;
            mainLight.lightShadowCasterMode = LightShadowCasterMode.Everything;
            mainLight.shadowResolution = LightShadowResolution.VeryHigh;

            // Get the scaled mesh and material for rendering
            scaledMesh = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("ParallaxContinued/Models/ScaledMesh").GetComponent<MeshFilter>().mesh);
            if (scaledMesh == null)
            {
                ParallaxDebug.LogError("Parallax Scaled mesh couldn't be located, it should be in ParallaxContinued/Models/ScaledMesh");
            }

            // First time buffer init
            shadowCommandBuffer = new CommandBuffer { name = "Render Custom Shadows" };
            lightCommandBuffer = new CommandBuffer { name = "Composite Shadows" };

            if (ScaledCamera.Instance.cam.renderingPath == RenderingPath.DeferredShading)
            {
                ScaledCamera.Instance.cam.AddCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
            }
            else
            {
                ScaledCamera.Instance.cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, shadowCommandBuffer);
            }

            SetupRenderTextures();
            SetupMaterials();
            SetupLightCommandBuffer(compositorMaterial);
        }
        void SetupRenderTextures()
        {
            RenderTextureDescriptor attenuationDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.R8, 16);
            attenuationDesc.useMipMap = true;
            attenuationDesc.autoGenerateMips = true;

            RenderTextureDescriptor distancesDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.RFloat, 0);
            distancesDesc.useMipMap = true;
            distancesDesc.autoGenerateMips = true;

            RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Depth, 16);
            depthDesc.useMipMap = false;

            shadowAttenuationRT = new RenderTexture(attenuationDesc);
            shadowDistanceRT = new RenderTexture(distancesDesc);
            shadowObjectDepthRT = new RenderTexture(depthDesc);

            shadowAttenuationRT.Create();
            shadowDistanceRT.Create();
            shadowObjectDepthRT.Create();
        }
        void SetupMaterials()
        {
            compositorMaterial = new Material(AssetBundleLoader.parallaxScaledShaders["Custom/PostProcessShadows"]);

            compositorMaterial.SetTexture("_MainTex", shadowAttenuationRT);
            compositorMaterial.SetTexture("_ShadowDistances", shadowDistanceRT);
            compositorMaterial.SetTexture("_ShadowDepth", shadowObjectDepthRT);

            //foreach ()
        }
        public void RenderShadows()
        {
            shadowCommandBuffer.Clear();

            // Setup RTs
            shadowCommandBuffer.SetRenderTarget(new RenderTargetIdentifier[] { shadowAttenuationRT, shadowDistanceRT }, shadowObjectDepthRT);

            // Clear target attenuation to 1 and depth to 1
            shadowCommandBuffer.ClearRenderTarget(true, true, Color.white);

            // Only render what we can see and is loaded
            for (int i = 0; i < scaledBodies.Count; i++)
            {
                ParallaxScaledBody scaledBody = scaledBodies[i];
                MeshRenderer meshRenderer = meshRenderers[i];
                if (!scaledBody.Loaded)
                {
                    continue;
                }
                
                // Prevent "donut" object rendering around small bodies from causing intense shadow flickering - stop rendering the attenuation mesh
                // Only set opacity on the current planet, or it'll set all scaled planet opacities to 0 if on pqs
                if (FlightGlobals.currentMainBody != null && RuntimeOperations.currentPlanetOpacity <= 0 && FlightGlobals.currentMainBody.name == scaledBody.planetName)
                {
                    // Don't render - we're in pqs view
                }
                else
                {
                    shadowCommandBuffer.SetGlobalMatrix("_WorldRotation", Matrix4x4.Inverse(Matrix4x4.Rotate(meshRenderer.localToWorldMatrix.rotation)));
                    shadowCommandBuffer.DrawRenderer(meshRenderer, scaledBody.shadowCasterMaterial, 0);
                    
                    // Set planet opacity for current and other scaled planets
                    if (FlightGlobals.currentMainBody != null && FlightGlobals.currentMainBody.name == scaledBody.planetName)
                    {
                        shadowCommandBuffer.SetGlobalFloat("_ScaledPlanetOpacity", RuntimeOperations.currentPlanetOpacity);
                    }
                    else
                    {
                        shadowCommandBuffer.SetGlobalFloat("_ScaledPlanetOpacity", 1.0f);
                    }
                }
            }
        }

        void SetupLightCommandBuffer(Material blitMaterial)
        {
            lightCommandBuffer.Blit(shadowAttenuationRT, BuiltinRenderTextureType.CurrentActive, blitMaterial);
            mainLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);
        }

        bool debug = false;
        void LateUpdate()
        {
            RenderShadows();

            // Debug shadow attenuation render
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha7))
            {
                debug = !debug;
            }
            if (debug)
            {
                if (ScaledCamera.Instance.cam.gameObject.GetComponent<ShadowDebug>() != null)
                {
                    return;
                }
                ShadowDebug shadowDebug = ScaledCamera.Instance.cam.gameObject.AddComponent<ShadowDebug>();
                shadowDebug.shadowTex = shadowAttenuationRT;

            }
            if (!debug)
            {
                ShadowDebug shadowDebug = ScaledCamera.Instance.cam.gameObject.GetComponent<ShadowDebug>();
                if (shadowDebug != null)
                {
                    UnityEngine.Object.Destroy(shadowDebug);
                }
            }
        }

        public void OnDestroy()
        {
            Debug.Log("Command buffer removed");
            ScaledCamera.Instance.cam.RemoveCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
            mainLight.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);
            lightCommandBuffer.Dispose();
            shadowCommandBuffer.Dispose();

            UnityEngine.Object.Destroy(scaledMesh);
        }
    }

    public class ShadowDebug : MonoBehaviour
    {
        public RenderTexture shadowTex;
        public Material shadowMaterial;
        void Start()
        {
            shadowMaterial = new Material(AssetBundleLoader.parallaxDebugShaders["Unlit/DummyBlit"]);
        }
        void Update()
        {
            Debug.Log("Looking for lights");
            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light light in lights)
            {
                Debug.Log("Light name: " + light.name);
                Debug.Log(" - type: " + light.type.ToString());
                if (light == ScaledSun.Instance.gameObject.GetComponent<Light>() || light == Sun.Instance.scaledSunLight)
                {
                    Debug.Log("is sun light");
                }
            }
        }
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(shadowTex, destination, shadowMaterial);
        }
    }
}
