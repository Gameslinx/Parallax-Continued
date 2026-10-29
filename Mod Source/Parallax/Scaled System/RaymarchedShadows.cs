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

        public const int MaxShadowStars = 4;

        // Meshes to draw
        public Mesh scaledMesh;

        class ShadowStar
        {
            public Light light;
            public CelestialBody body;
            public CommandBuffer compositeCommandBuffer;
            public float importance;
        }

        // Kopernicus gives every star its own scaled light
        List<ShadowStar> stars = new List<ShadowStar>();
        List<ShadowStar> rankedStars = new List<ShadowStar>();
        static readonly Comparison<ShadowStar> compareImportance = (a, b) => b.importance.CompareTo(a.importance);

        Material[] compositorMaterials = new Material[MaxShadowStars];
        Vector4[] shadowLights = new Vector4[MaxShadowStars];

        // Stores shadow attenuation, 0 or 1
        RenderTexture shadowAttenuationRT;

        // Stores ray distance for umbra/penumbra calcs, float
        RenderTexture shadowDistanceRT;

        // Stores depth for the meshes being rendered into it
        RenderTexture shadowObjectDepthRT;

        CommandBuffer shadowCommandBuffer;

        Texture2D blueNoiseTexture;

        public List<ParallaxScaledBody> scaledBodies = new List<ParallaxScaledBody>();
        public List<CelestialBody> celestialBodies = new List<CelestialBody>();
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
                CelestialBody body = FlightGlobals.GetBodyByName(pair.Value.planetName);
                scaledBodies.Add(pair.Value);
                celestialBodies.Add(body);
                meshRenderers.Add(body.scaledBody.GetComponent<MeshRenderer>());
            }

            // Ensure the screenspace mask is generated
            stars.AddRange(GetStars());
            foreach (ShadowStar star in stars)
            {
                star.light.shadows = LightShadows.Soft;
                star.light.shadowStrength = 1;
                star.light.lightShadowCasterMode = LightShadowCasterMode.Everything;
                star.light.shadowResolution = LightShadowResolution.VeryHigh;
                star.compositeCommandBuffer = new CommandBuffer { name = "Composite Shadows" };
                star.light.AddCommandBuffer(LightEvent.AfterScreenspaceMask, star.compositeCommandBuffer);
            }

            // Get the scaled mesh and material for rendering
            scaledMesh = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("ParallaxContinued/Models/ScaledMesh").GetComponent<MeshFilter>().mesh);
            if (scaledMesh == null)
            {
                ParallaxDebug.LogError("Parallax Scaled mesh couldn't be located, it should be in ParallaxContinued/Models/ScaledMesh");
            }

            // First time buffer init
            shadowCommandBuffer = new CommandBuffer { name = "Render Custom Shadows" };

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

            Camera.onPreCull += OnCameraPreCull;
        }
        void OnCameraPreCull(Camera cam)
        {
            if (ScaledCamera.Instance != null && cam == ScaledCamera.Instance.cam)
            {
                RenderShadows();
            }
        }
        static IEnumerable<ShadowStar> GetStars()
        {
            if (Kopernicus.Components.KopernicusStar.Stars == null)
            {
                return new ShadowStar[] { new ShadowStar { light = Sun.Instance.scaledSunLight, body = Sun.Instance.sun } };
            }
            return Kopernicus.Components.KopernicusStar.Stars
                .Where(star => star != null && star.scaledSunLight != null && star.sun != null)
                .Select(star => new ShadowStar { light = star.scaledSunLight, body = star.sun });
        }
        public void Enable()
        {
            this.enabled = true;
            Start();
        }
        void SetupRenderTextures()
        {
            RenderTextureDescriptor attenuationDesc = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGB32, 16);
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
            for (int i = 0; i < MaxShadowStars; i++)
            {
                Material compositorMaterial = new Material(AssetBundleLoader.parallaxScaledShaders["Custom/PostProcessShadows"]);

                compositorMaterial.SetTexture("_MainTex", shadowAttenuationRT);
                compositorMaterial.SetTexture("_ShadowDistances", shadowDistanceRT);
                compositorMaterial.SetTexture("_ShadowDepth", shadowObjectDepthRT);

                Vector4 channelMask = Vector4.zero;
                channelMask[i] = 1;
                compositorMaterial.SetVector("_ShadowChannelMask", channelMask);

                compositorMaterials[i] = compositorMaterial;
            }
        }
        void RankStars()
        {
            rankedStars.Clear();
            foreach (ShadowStar star in stars)
            {
                star.compositeCommandBuffer.Clear();
                if (star.light == null || !star.light.isActiveAndEnabled)
                {
                    continue;
                }

                star.importance = star.light.intensity * star.light.color.grayscale;
                if (star.importance > 0)
                {
                    rankedStars.Add(star);
                }
            }

            rankedStars.Sort(compareImportance);

            int maxStars = Mathf.Clamp(ConfigLoader.parallaxGlobalSettings.scaledGlobalSettings.scaledRaymarchedShadowMaxStars, 1, MaxShadowStars);
            if (rankedStars.Count > maxStars)
            {
                rankedStars.RemoveRange(maxStars, rankedStars.Count - maxStars);
            }

            for (int i = 0; i < rankedStars.Count; i++)
            {
                rankedStars[i].compositeCommandBuffer.Blit(shadowAttenuationRT, BuiltinRenderTextureType.CurrentActive, compositorMaterials[i]);
            }
        }
        void SetShadowLights(CelestialBody body, Material shadowCasterMaterial)
        {
            for (int i = 0; i < rankedStars.Count; i++)
            {
                CelestialBody star = rankedStars[i].body;
                if (star == body)
                {
                    shadowLights[i] = Vector4.zero;
                    continue;
                }

                Vector3 lightDirection = (star.scaledBody.transform.position - body.scaledBody.transform.position).normalized;
                float penumbraAngle = Mathf.Atan2((float)star.Radius, (body.transform.position - star.transform.position).magnitude);
                shadowLights[i] = new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, penumbraAngle);
            }

            shadowCasterMaterial.SetVectorArray("_ShadowLights", shadowLights);
            shadowCasterMaterial.SetInt("_ShadowLightCount", rankedStars.Count);
        }
        public void RenderShadows()
        {
            RankStars();

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

                    SetShadowLights(celestialBodies[i], scaledBody.shadowCasterMaterial);

                    shadowCommandBuffer.SetGlobalInt("_FrameCount", Time.frameCount);
                    shadowCommandBuffer.DrawRenderer(meshRenderer, scaledBody.shadowCasterMaterial, 0, 0);
                }
            }
        }

        bool debug = false;
        void Update()
        {
            DebugShadowComponent();
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

            foreach (ShadowStar star in stars)
            {
                if (star.light != null)
                {
                    star.light.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, star.compositeCommandBuffer);
                }
                star.compositeCommandBuffer.Dispose();
            }
            stars.Clear();
            rankedStars.Clear();

            shadowCommandBuffer.Dispose();

            scaledBodies.Clear();
            celestialBodies.Clear();
            meshRenderers.Clear();

            shadowAttenuationRT.Release();
            shadowDistanceRT.Release();
            shadowObjectDepthRT.Release();

            UnityEngine.Object.Destroy(scaledMesh);
            for (int i = 0; i < MaxShadowStars; i++)
            {
                UnityEngine.Object.Destroy(compositorMaterials[i]);
            }
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
