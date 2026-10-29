using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSPTextureLoader;
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

        // Kopernicus gives every star its own scaled light
        List<Light> starLights = new List<Light>();
        Material compositorMaterial;

        // Stores shadow attenuation, 0 or 1
        RenderTexture shadowAttenuationRT;

        // Stores ray distance for umbra/penumbra calcs, float
        RenderTexture shadowDistanceRT;

        // Stores depth for the meshes being rendered into it
        RenderTexture shadowObjectDepthRT;

        CommandBuffer shadowCommandBuffer;
        CommandBuffer lightCommandBuffer;

        Texture2D blueNoiseTexture;

        public List<ParallaxScaledBody> scaledBodies = new List<ParallaxScaledBody>();
        public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        void Awake()
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(this);

            if (!ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledSpaceShadows)
            {
                this.enabled = false;
            }
        }
        void Start()
        {
            // Load the texture - uncompressed DDS Luminance 8, linear
            var options = new TextureLoadOptions
            {
                Linear = true,
                Unreadable = true,
                Hint = TextureLoadHint.Synchronous
            };
            var handle = TextureLoader
                .LoadTexture<Texture2D>("ParallaxContinued/Textures/PluginData/blueNoise.dds", options);
            ParallaxDebug.LogTextureLoaded(handle);
            blueNoiseTexture = handle.TakeTexture();

            // Setup
            foreach (KeyValuePair<string, ParallaxScaledBody> pair in ConfigLoader.parallaxScaledBodies)
            {
                pair.Value.shadowCasterMaterial.SetTexture("_BlueNoise", blueNoiseTexture);
                scaledBodies.Add(pair.Value);
                meshRenderers.Add(FlightGlobals.GetBodyByName(pair.Value.planetName).scaledBody.GetComponent<MeshRenderer>());
            }

            // Ensure the screenspace mask is generated
            starLights.AddRange(GetStarLights());
            foreach (Light light in starLights)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1;
                light.lightShadowCasterMode = LightShadowCasterMode.Everything;
                light.shadowResolution = LightShadowResolution.VeryHigh;
            }

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
                ScaledCamera.Instance.cam.AddCommandBuffer(CameraEvent.AfterDepthTexture, shadowCommandBuffer);
            }

            SetupRenderTextures();
            SetupMaterials();
            SetupLightCommandBuffer(compositorMaterial);

            Camera.onPreCull += OnCameraPreCull;
        }
        void OnCameraPreCull(Camera cam)
        {
            if (ScaledCamera.Instance != null && cam == ScaledCamera.Instance.cam)
            {
                RenderShadows();
            }
        }
        static IEnumerable<Light> GetStarLights()
        {
            if (Kopernicus.Components.KopernicusStar.Stars == null)
            {
                return new Light[] { Sun.Instance.scaledSunLight };
            }
            return Kopernicus.Components.KopernicusStar.Stars
                .Where(star => star != null && star.scaledSunLight != null)
                .Select(star => star.scaledSunLight);
        }
        public void Enable()
        {
            this.enabled = true;
            Start();
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
                    // Set planet opacity for current and other scaled planets
                    if (FlightGlobals.currentMainBody != null && FlightGlobals.currentMainBody.name == scaledBody.planetName)
                    {
                        shadowCommandBuffer.SetGlobalFloat("_ScaledPlanetOpacity", RuntimeOperations.currentPlanetOpacity);
                    }
                    else
                    {
                        shadowCommandBuffer.SetGlobalFloat("_ScaledPlanetOpacity", 1.0f);
                    }

                    shadowCommandBuffer.SetGlobalInt("_FrameCount", Time.frameCount);
                    shadowCommandBuffer.DrawRenderer(meshRenderer, scaledBody.shadowCasterMaterial, 0, 0);
                }
            }
        }

        void SetupLightCommandBuffer(Material blitMaterial)
        {
            lightCommandBuffer.Blit(shadowAttenuationRT, BuiltinRenderTextureType.CurrentActive, blitMaterial);
            foreach (Light light in starLights)
            {
                light.AddCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);
            }
        }

        bool debug = false;
        void Update()
        {
            DebugShadowComponent();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha8))
            {
                LogShadowDiagnostics();
            }
        }
        void LogShadowDiagnostics()
        {
            ParallaxDebug.Log("[Shadow Diagnostics] Scene: " + HighLogic.LoadedScene + ", frame " + Time.frameCount);

            Camera cam = ScaledCamera.Instance != null ? ScaledCamera.Instance.cam : null;
            if (cam == null)
            {
                ParallaxDebug.Log("[Shadow Diagnostics] ScaledCamera.Instance.cam is null");
            }
            else
            {
                CameraEvent cameraEvent = cam.renderingPath == RenderingPath.DeferredShading ? CameraEvent.BeforeLighting : CameraEvent.AfterDepthTexture;
                bool hasShadowBuffer = cam.GetCommandBuffers(cameraEvent).Contains(shadowCommandBuffer);
                ParallaxDebug.Log("[Shadow Diagnostics] Scaled camera '" + cam.name + "' id " + cam.GetInstanceID() + ", enabled " + cam.enabled + ", active " + cam.gameObject.activeInHierarchy
                    + ", renderingPath " + cam.renderingPath + ", actualRenderingPath " + cam.actualRenderingPath + ", depthTextureMode " + cam.depthTextureMode
                    + ", has shadow buffer on " + cameraEvent + ": " + hasShadowBuffer);
            }

            foreach (Camera c in Camera.allCameras)
            {
                ParallaxDebug.Log("[Shadow Diagnostics] Active camera '" + c.name + "' id " + c.GetInstanceID() + ", depth " + c.depth + ", cullingMask " + c.cullingMask + ", renderingPath " + c.actualRenderingPath);
            }

            ParallaxDebug.Log("[Shadow Diagnostics] Tracked star lights: " + starLights.Count);
            foreach (Light light in starLights)
            {
                if (light == null)
                {
                    ParallaxDebug.Log("[Shadow Diagnostics] Tracked light destroyed");
                    continue;
                }
                ParallaxDebug.Log("[Shadow Diagnostics] Tracked light '" + light.name + "' id " + light.GetInstanceID() + ", enabled " + light.enabled + ", shadows " + light.shadows
                    + ", has composite buffer: " + light.GetCommandBuffers(LightEvent.AfterScreenspaceMask).Contains(lightCommandBuffer));
            }

            foreach (Light light in GetStarLights())
            {
                ParallaxDebug.Log("[Shadow Diagnostics] Current star light '" + (light != null ? light.name + "' id " + light.GetInstanceID() + ", enabled " + light.enabled + ", shadows " + light.shadows + ", tracked " + starLights.Contains(light) : "null'"));
            }

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    ParallaxDebug.Log("[Shadow Diagnostics] Shadowing directional light '" + light.name + "' id " + light.GetInstanceID() + ", enabled " + light.enabled + ", cullingMask " + light.cullingMask
                        + ", composite buffers " + light.GetCommandBuffers(LightEvent.AfterScreenspaceMask).Length);
                }
            }

            for (int i = 0; i < scaledBodies.Count; i++)
            {
                if (scaledBodies[i].Loaded)
                {
                    Material m = scaledBodies[i].shadowCasterMaterial;
                    ParallaxDebug.Log("[Shadow Diagnostics] Loaded body " + scaledBodies[i].planetName + ": _MaxRayDistance " + m.GetFloat("_MaxRayDistance") + ", _LightWidth " + m.GetFloat("_LightWidth")
                        + ", _ShadowLightDirection " + m.GetVector("_ShadowLightDirection") + ", renderer null " + (meshRenderers[i] == null));
                }
            }
            ParallaxDebug.Log("[Shadow Diagnostics] Global _ParallaxScaledShadowStepSize " + Shader.GetGlobalInt("_ParallaxScaledShadowStepSize") + ", QualitySettings.shadowDistance " + QualitySettings.shadowDistance);
        }
        void DebugShadowComponent()
        {
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
        public void Disable()
        {
            OnDestroy();
            this.enabled = false;
        }
        public void OnDestroy()
        {
            ParallaxDebug.Log("Raymarched shadows shutting down");

            Camera.onPreCull -= OnCameraPreCull;

            debug = false;
            DebugShadowComponent();

            if (ScaledCamera.Instance.cam.renderingPath == RenderingPath.DeferredShading)
            {
                ScaledCamera.Instance.cam.RemoveCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
            }
            else
            {
                ScaledCamera.Instance.cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, shadowCommandBuffer);
            }
            
            foreach (Light light in starLights)
            {
                if (light != null)
                {
                    light.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);
                }
            }
            starLights.Clear();

            lightCommandBuffer.Dispose();
            shadowCommandBuffer.Dispose();

            scaledBodies.Clear();
            meshRenderers.Clear();

            shadowAttenuationRT.Release();
            shadowDistanceRT.Release();
            shadowObjectDepthRT.Release();

            UnityEngine.Object.Destroy(scaledMesh);
            UnityEngine.Object.Destroy(compositorMaterial);
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
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(shadowTex, destination, shadowMaterial);
        }
    }
}
