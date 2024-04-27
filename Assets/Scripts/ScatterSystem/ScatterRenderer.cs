using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScatterRenderer : MonoBehaviour
{
    public Material instancedMaterialLOD0;
    public Material instancedMaterialLOD1;
    public Material instancedMaterialLOD2;

    public Mesh meshLOD0;
    public Mesh meshLOD1;
    public Mesh meshLOD2;

    public ComputeBuffer outputLOD0;
    public ComputeBuffer outputLOD1;
    public ComputeBuffer outputLOD2;

    ComputeBuffer indirectArgsLOD0;
    ComputeBuffer indirectArgsLOD1;
    ComputeBuffer indirectArgsLOD2;

    Bounds rendererBounds;

    public List<TerrainScatters> scatterComponents;
    void Start()
    {
        Prerequisites();
        Initialize();
        FirstTimeArgs();
    }
    void Prerequisites()
    {
        // See in-game implementation
        // Assign meshes, materials here
    }
    void Initialize()
    {
        // For testing purposes
        // These need to be separate instances otherwise they'll be overwritten
        instancedMaterialLOD1 = Instantiate(instancedMaterialLOD0);
        instancedMaterialLOD2 = Instantiate(instancedMaterialLOD0);

        // Create output buffers - Evaluate() function on quads will will these
        int arbitraryMaxCount = 10000;
        outputLOD0 = new ComputeBuffer(arbitraryMaxCount, TransformData.Size(), ComputeBufferType.Append);
        outputLOD1 = new ComputeBuffer(arbitraryMaxCount, TransformData.Size(), ComputeBufferType.Append);
        outputLOD2 = new ComputeBuffer(arbitraryMaxCount, TransformData.Size(), ComputeBufferType.Append);

        // Set the instance data on the material
        instancedMaterialLOD0.SetBuffer("_InstanceData", outputLOD0);
        instancedMaterialLOD1.SetBuffer("_InstanceData", outputLOD1);
        instancedMaterialLOD2.SetBuffer("_InstanceData", outputLOD2);

        // Must initialize the count to 0
        outputLOD0.SetCounterValue(0);
        outputLOD1.SetCounterValue(0);
        outputLOD2.SetCounterValue(0);

        rendererBounds = new Bounds(Vector3.zero, Vector3.one * 25000.0f);
    }
    void FirstTimeArgs()
    {
        uint[] argumentsLod0 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod0[0] = (uint)meshLOD0.GetIndexCount(0);
        argumentsLod0[1] = 0; // Number of meshes to instance, we will this in Update() through CopyCount
        argumentsLod0[2] = (uint)meshLOD0.GetIndexStart(0);
        argumentsLod0[3] = (uint)meshLOD0.GetBaseVertex(0);

        uint[] argumentsLod1 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod1[0] = (uint)meshLOD1.GetIndexCount(0);
        argumentsLod1[1] = 0; // Number of meshes to instance, we will this in Update() through CopyCount
        argumentsLod1[2] = (uint)meshLOD1.GetIndexStart(0);
        argumentsLod1[3] = (uint)meshLOD1.GetBaseVertex(0);

        uint[] argumentsLod2 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod2[0] = (uint)meshLOD2.GetIndexCount(0);
        argumentsLod2[1] = 0; // Number of meshes to instance, we will this in Update() through CopyCount
        argumentsLod2[2] = (uint)meshLOD2.GetIndexStart(0);
        argumentsLod2[3] = (uint)meshLOD2.GetBaseVertex(0);

        indirectArgsLOD0 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        indirectArgsLOD0.SetData(argumentsLod0);

        indirectArgsLOD1 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        indirectArgsLOD1.SetData(argumentsLod1);

        indirectArgsLOD2 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        indirectArgsLOD2.SetData(argumentsLod2);
    }
    void Update()
    {
        // Hugely important we set the count to 0 or the buffer will keep filling up
        outputLOD0.SetCounterValue(0);
        outputLOD1.SetCounterValue(0);
        outputLOD2.SetCounterValue(0);

        // Fill the buffer with our instanced data
        for (int i = 0; i < scatterComponents.Count; i++)
        {
            scatterComponents[i].Evaluate();
        }

        // Copy the count from the output buffer to the indirect args for instancing
        ComputeBuffer.CopyCount(outputLOD0, indirectArgsLOD0, 4);
        ComputeBuffer.CopyCount(outputLOD1, indirectArgsLOD1, 4);
        ComputeBuffer.CopyCount(outputLOD2, indirectArgsLOD2, 4);

        // Render instanced data
        Graphics.DrawMeshInstancedIndirect(meshLOD0, 0, instancedMaterialLOD0, rendererBounds, indirectArgsLOD0, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null);
        Graphics.DrawMeshInstancedIndirect(meshLOD1, 0, instancedMaterialLOD1, rendererBounds, indirectArgsLOD1, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null);
        Graphics.DrawMeshInstancedIndirect(meshLOD2, 0, instancedMaterialLOD2, rendererBounds, indirectArgsLOD2, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null);
    }
    void Cleanup()
    {
        outputLOD0?.Dispose();
        outputLOD1?.Dispose();
        outputLOD2?.Dispose();
    }
    void OnDisable()
    {
        Cleanup();
    }
}
