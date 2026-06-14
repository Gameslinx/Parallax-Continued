using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Parallax.Scaled_System
{
    /// <summary>
    /// Class to extract the skybox and create a cubemap from it
    /// </summary>
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class SkyboxControl : MonoBehaviour
    {
        public static RenderTexture equirectangularSkybox;
        public static Cubemap cubeMap;
        public static bool alreadyGenerated = false;

        void Awake()
        {
            // This runs for tracking and flight
            // Main menu needs a separate approach - see below
            if (!HighLogic.LoadedSceneIsFlight && !(HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                return;
            }
            ExtractSkyboxFrom(GalaxyCubeControl.Instance?.gameObject);
            GameObject.DontDestroyOnLoad(this);
        }

        public static void ExtractSkyboxFrom(GameObject skybox)
        {
            // We can only successfully get the skybox in flight or tracking station, which works well enough
            if (alreadyGenerated && !(HighLogic.LoadedScene == GameScenes.MAINMENU))
            {
                return;
            }
            
            ParallaxDebug.Log("Scaled: Processing skybox");
            float startTime = Time.realtimeSinceStartup;
            //GalaxyCubeControl skybox = GalaxyCubeControl.Instance;
            Renderer[] renderers = skybox.GetComponentsInChildren<Renderer>(true);
            
            // Should be 6
            Texture2D[] skyboxTextures = new Texture2D[renderers.Length];
            ParallaxDebug.Log("Found " + renderers.Length + " renderers");
            
            // Texture format:
            // 0 = XP
            // 1 = XN
            // 2 = YP
            // 3 = YN
            // 4 = ZP
            // 5 = ZN
            
            foreach (Renderer renderer in renderers)
            {
                string name = renderer.name;
                Material material = renderer?.material;
                ParallaxDebug.Log("Material name: " + name + ", null = " + (material == null));
                if (name == "XP")
                {
                    skyboxTextures[0] = material.mainTexture as Texture2D;
                }
                if (name == "XN")
                {
                    skyboxTextures[1] = material.mainTexture as Texture2D;
                }
            
                if (name == "YP")
                {
                    skyboxTextures[2] = material.mainTexture as Texture2D;
                }
                if (name == "YN")
                {
                    skyboxTextures[3] = material.mainTexture as Texture2D;
                }
            
                if (name == "ZP")
                {
                    skyboxTextures[4] = material.mainTexture as Texture2D;
                }
                if (name == "ZN")
                {
                    skyboxTextures[5] = material.mainTexture as Texture2D;
                }
            }
            
            if (skyboxTextures.Any(x => x == null))
            {
                ParallaxDebug.LogError("Unable to process skybox for scaled space reflections");
            }

            Resources.UnloadUnusedAssets();

            // Store current quality setting
            // Game settings texture quality changes master texture limit, which resizes all textures before we can extract the skybox
            int previousMasterTextureLimit = 0;
            if (GameSettings.TEXTURE_QUALITY != 0)
            {
                previousMasterTextureLimit = QualitySettings.masterTextureLimit;

                // Force full-resolution textures temporarily
                QualitySettings.masterTextureLimit = 0;
            }

            int resultDim = skyboxTextures[0].width;

            if (!TryCopyDirectly(resultDim, skyboxTextures, out var cube))
                cube = ExtractDownloadAndCompress(resultDim, skyboxTextures);

            cubeMap = cube;

            GameObject.DontDestroyOnLoad(cubeMap);

            if (HighLogic.LoadedScene != GameScenes.MAINMENU)
            {
                alreadyGenerated = true;
            }

            float timeTaken = Time.realtimeSinceStartup - startTime;
            ParallaxDebug.Log("Skybox processing took " + timeTaken.ToString("F2") + " seconds");
        }
    
        static bool TryCopyDirectly(
            int resultDim,
            Texture2D[] skyboxTextures,
            out Cubemap cubemap
        )
        {
            var format = skyboxTextures[0].format;
            var mipmapCount = skyboxTextures[0].mipmapCount;
            cubemap = null;

            if (!SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.DifferentTypes))
                return false;

            if (!GraphicsFormatUtility.IsCompressedFormat(skyboxTextures[0].graphicsFormat))
                return false;

            foreach (var tex in skyboxTextures)
            {
                if (tex.width != resultDim
                    || tex.height != resultDim
                    || tex.format != format
                    || tex.mipmapCount != mipmapCount)
                {
                    return false;
                }
            }

            cubemap = new Cubemap(resultDim, format, mipmapCount);
            cubemap.Apply(false, true);
            for (int i = 0; i < 6; ++i)
                Graphics.CopyTexture(skyboxTextures[i], 0, cubemap, i);

            return true;
        }

        static Cubemap ExtractDownloadAndCompress(
            int resultDim,
            Texture2D[] skyboxTextures)
        {

            RenderTexture rt = new RenderTexture(resultDim, resultDim, 0, GraphicsFormat.R8G8B8A8_UNorm);
            rt.Create();

            Texture2D[] destTextures = new Texture2D[6];
            for (int i = 0; i < skyboxTextures.Length; i++)
            {
                Texture2D tex = skyboxTextures[i];

                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;

                destTextures[i] = new Texture2D(resultDim, resultDim, TextureFormat.RGBA32, true);
                destTextures[i].ReadPixels(new Rect(0, 0, resultDim, resultDim), 0, 0, true);
                destTextures[i].Compress(false);

                // Destroy the texture at the end of the frame
                Texture.Destroy(destTextures[i]);
            }

            RenderTexture.active = null;
            rt.Release();

            var cubemap = new Cubemap(resultDim, destTextures[0].format, destTextures[0].mipmapCount);

            if (SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.DifferentTypes))
            {
                foreach (var tex in destTextures)
                    tex.Apply(false, true);

                cubemap.Apply(false, true);
                for (int i = 0; i < 6; i++)
                    Graphics.CopyTexture(skyboxTextures[i], 0, cubemap, i);
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    var tex = destTextures[i];
                    var data = tex.GetRawTextureData<byte>();

                    int offset = 0;
                    int size = resultDim;
                    int mipCount = cubemap.mipmapCount;

                    for (int mip = 0; mip < mipCount; ++mip)
                    {
                        var mipsize = (int)GraphicsFormatUtility.ComputeMipmapSize(
                            size,
                            size,
                            cubemap.graphicsFormat
                        );
                        if (offset + mipsize > data.Length)
                            throw new Exception($"dest texture data size was too small (expected at least {offset + mipsize} but got {data.Length})");

                        cubemap.SetPixelData(data, mip, (CubemapFace)i, offset);
                        offset += mipsize;
                    }
                }

                cubemap.Apply(false, true);
            }

            return cubemap;
        }

        readonly struct RTGuard(RenderTexture[] textures) : IDisposable
        {
            public void Dispose()
            {
                foreach (var rt in textures)
                    rt?.Release();
            }
        }

        readonly struct TextureLimitGuard : IDisposable
        {
            readonly int prev;

            public TextureLimitGuard()
            {
                prev = QualitySettings.masterTextureLimit;
            }

            public void Dispose() => QualitySettings.masterTextureLimit = prev;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class SkyboxControlMainMenu : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(PostApplySkybox());
        }
        IEnumerator PostApplySkybox()
        {
            // Wait for skybox mods to apply
            int frameDelay = 5;
            for (int i = 0; i < frameDelay; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            ParallaxDebug.Log("Extracting skybox from the main menu scene");
            SkyboxControl.ExtractSkyboxFrom(GameObject.Find("MainMenuGalaxy"));
        }
    }
}
