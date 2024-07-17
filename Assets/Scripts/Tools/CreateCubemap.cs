using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CreateCubemap : MonoBehaviour
{
    // Start is called before the first frame update
    public static RenderTexture cubemap;
    public static RenderTexture equirectangular;

    [MenuItem("Parallax/Generate Cubemap")]
    public static void OnWizardCreate()
    {

        RenderTexture cubemap = new RenderTexture(4096, 4096, 24);
        cubemap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        cubemap.enableRandomWrite = true;
        cubemap.Create();

        // Set up equirectangular render texture
        RenderTexture equirectangular = new RenderTexture(4096, 4096, 24);
        equirectangular.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        equirectangular.enableRandomWrite = true;
        equirectangular.Create();

        // Create a temporary camera for rendering
        GameObject go = new GameObject("CubemapCamera");
        go.transform.SetParent(Camera.main.transform, false);
        Camera cam = go.AddComponent<Camera>();

        

        cam.transform.position = Vector3.up * 5;
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = Color.black;
        cam.cullingMask = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 9) | (1 << 10) | (1 << 15) | (1 << 18) | (1 << 19);

        

        // Render into the cubemap
        cam.RenderToCubemap(cubemap);

        // Convert cubemap to equirectangular
        cubemap.ConvertToEquirect(equirectangular, Camera.MonoOrStereoscopicEye.Mono);

        // Create Texture2D and copy RenderTexture to it
        Texture2D dest = new Texture2D(4096, 4096, TextureFormat.ARGB32, false);
        RenderTexture.active = equirectangular;
        dest.ReadPixels(new Rect(0, 0, equirectangular.width, equirectangular.height), 0, 0);
        dest.Apply();

        // Save to PNG file
        byte[] result = dest.EncodeToPNG();
        File.WriteAllBytes("C:/Users/tuvee/Pictures/cubemapExport.png", result);

        // Clean up
        DestroyImmediate(go);
        cubemap.Release();
        equirectangular.Release();


    }

    
}
