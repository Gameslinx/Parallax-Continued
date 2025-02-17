using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowDebug : MonoBehaviour
{
    Camera mainCamera;
    CommandBuffer cmd;

    public RenderTexture attenuationTexture;
    public Material dummyBlitMaterial;
    // Start is called before the first frame update
    void Start()
    {
        cmd = new CommandBuffer();
        mainCamera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //void OnPreRender()
    //{
    //    cmd.Clear();
    //
    //    cmd.SetGlobalTexture("_AttenuationTexture", attenuationTexture);
    //    cmd.Blit(attenuationTexture, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), dummyBlitMaterial);
    //
    //    mainCamera.AddCommandBuffer(CameraEvent.AfterEverything, cmd);
    //}
    //
    //void OnPostRender()
    //{
    //    if (cmd != null)
    //    {
    //        mainCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, cmd);
    //    }
    //}
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //dummyBlitMaterial.SetTexture("_MainTex", )
        Graphics.Blit(attenuationTexture, destination);
    }

    void OnDisable()
    {
        if (cmd != null)
        {
            cmd.Dispose();
        }
    }
}
