using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RaymarchedShadows : MonoBehaviour
{
    public GameObject customShadowObject;
    public Light mainLight;
    public Material shadowMaterial;
    public Material compositorMaterial;

    public RenderTexture shadowAttenuationRT;
    public RenderTexture shadowDistanceRT;
    public RenderTexture shadowObjectDepth;

    CommandBuffer shadowCommandBuffer;
    CommandBuffer lightCommandBuffer;

    void Start()
    {
        SetupMaterials();
        SetupShadowCommandBuffer(shadowMaterial);
        SetupLightCommandBuffer(compositorMaterial);
    }
    void SetupMaterials()
    {
        compositorMaterial.SetTexture("_ShadowDistances", shadowDistanceRT);
        compositorMaterial.SetTexture("_ShadowDepth", shadowObjectDepth);
    }
    void SetupShadowCommandBuffer(Material shadowMaterial)
    {
        shadowCommandBuffer = new CommandBuffer { name = "Render Custom Shadows" };

        if (Camera.main.renderingPath == RenderingPath.DeferredShading)
        {
            Camera.main.AddCommandBuffer(CameraEvent.BeforeLighting, shadowCommandBuffer);
        }
        else
        {
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, shadowCommandBuffer);
        }
    }
    void RenderShadows()
    {
        shadowCommandBuffer.Clear();
        shadowCommandBuffer.SetRenderTarget(
            new RenderTargetIdentifier[] { shadowAttenuationRT, shadowDistanceRT },
            shadowObjectDepth
        );
        shadowCommandBuffer.ClearRenderTarget(true, true, Color.clear);
        shadowCommandBuffer.SetGlobalInt("_FrameCount", Time.frameCount);
        shadowCommandBuffer.DrawMesh(customShadowObject.GetComponent<MeshFilter>().sharedMesh, customShadowObject.transform.localToWorldMatrix, shadowMaterial);
    }
    void Update()
    {
        RenderShadows();
    }
    void SetupLightCommandBuffer(Material blitMaterial)
    {
        lightCommandBuffer = new CommandBuffer { name = "Composite Shadows" };

        lightCommandBuffer.Blit(shadowAttenuationRT, BuiltinRenderTextureType.CurrentActive, blitMaterial);
        mainLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, lightCommandBuffer);
    }

    void OnDisable()
    {
        lightCommandBuffer.Dispose();
        shadowCommandBuffer.Dispose();
    }
}
