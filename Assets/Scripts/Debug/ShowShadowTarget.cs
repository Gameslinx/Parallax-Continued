using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Burst.Intrinsics.X86.Avx;

public class ShowShadowTarget : MonoBehaviour
{
    // Start is called before the first frame update
    public Material blitMaterial;
    public RenderTexture shadowMap;
    public RenderTexture shadowDistances;

    CommandBuffer cmd;
    Light lightComponent;
    void Start()
    {
        lightComponent = GetComponent<Light>();
        cmd = new CommandBuffer();


        // Get unity shadow map
        int _MyScreenSpaceShadowRTID = Shader.PropertyToID("_UnityShadowMap");
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.RHalf);
        cmd.GetTemporaryRT(_MyScreenSpaceShadowRTID, descriptor);
        cmd.SetGlobalTexture("_UnityShadowMap", BuiltinRenderTextureType.CurrentActive);
        cmd.SetGlobalTexture("_ShadowDistances", shadowDistances);
        cmd.Blit(shadowMap, BuiltinRenderTextureType.CurrentActive, blitMaterial);
        lightComponent.AddCommandBuffer(LightEvent.AfterScreenspaceMask, cmd);
    }
    // Update is called once per frame
    void Update()
    {
        
    }

}
