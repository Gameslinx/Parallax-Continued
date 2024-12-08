using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Parallax.Scaled_System
{
    /// <summary>
    /// Class to extract the skybox and create a cubemap from it
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class SkyboxControl : MonoBehaviour
    {
        public static RenderTexture equirectangularSkybox;
        public static Cubemap cubeMap;
        void Awake()
        {
            ParallaxDebug.Log("Scaled: Processing skybox");
            float startTime = Time.realtimeSinceStartup;
            GalaxyCubeControl skybox = GalaxyCubeControl.Instance;
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

            int faceDim = skyboxTextures[0].width;

            // We're using this for environment reflections, does not need to be as high-res
            int resultDim = faceDim / 4;
            TextureFormat format = skyboxTextures[0].format;
            
            // Elaborate way of supporting compressed textures and cramming them into a cubemap
            RenderTexture rt = new RenderTexture(resultDim, resultDim, 0, GraphicsFormat.R8G8B8A8_UNorm);
            rt.Create();

            Texture2D[] destTextures = new Texture2D[6];
            for (int i = 0; i < skyboxTextures.Length; i++)
            {
                Texture2D tex = skyboxTextures[i];
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                destTextures[i] = new Texture2D(resultDim, resultDim, TextureFormat.RGBA32, true);
                destTextures[i].ReadPixels(new Rect(0, 0, resultDim, resultDim), 0, 0, false);
                destTextures[i].Compress(false);
                destTextures[i].Apply(true, true);
            }

            Cubemap cube = new Cubemap(resultDim, destTextures[0].format, destTextures[0].mipmapCount);

            Graphics.CopyTexture(destTextures[0], 0, cube, 0);
            Graphics.CopyTexture(destTextures[1], 0, cube, 1);
            Graphics.CopyTexture(destTextures[2], 0, cube, 2);
            Graphics.CopyTexture(destTextures[3], 0, cube, 3);
            Graphics.CopyTexture(destTextures[4], 0, cube, 4);
            Graphics.CopyTexture(destTextures[5], 0, cube, 5);

            cubeMap = cube;

            float timeTaken = Time.realtimeSinceStartup - startTime;
            ParallaxDebug.Log("Skybox processing took " + timeTaken.ToString("F2") + " seconds");
        }
    }
}
