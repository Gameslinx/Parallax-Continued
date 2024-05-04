using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// Output from the distribute points kernel
public struct PositionData
{
    public Vector3 localPos;
    public Vector3 localScale;
    public float rotation;
    public uint index;

    public static int Size()
    {
        return 7 * sizeof(float) + 1 * sizeof(uint);
    }
};

// Output from the evaluate points kernel
// And sent to shader for rendering
public struct TransformData
{
    public Matrix4x4 objectToWorld;
    public static int Size()
    {
        return sizeof(float) * 16;
    }
};

public class TerrainScatters : MonoBehaviour
{
    // Set this from code game-side
    ScatterRenderer scatterRenderer;
    public ComputeShader scatterShader;
    int distributeKernel;
    int evaluateKernel;

    // Distribution buffers
    ComputeBuffer sourceVertsBuffer;
    ComputeBuffer sourceNormalsBuffer;
    ComputeBuffer sourceTrianglesBuffer;
    ComputeBuffer outputScatterDataBuffer;

    // Evaluation buffers
    ComputeBuffer dispatchArgs;
    ComputeBuffer objectLimits;

    // Physical mesh data
    Vector3[] vertices;
    Vector3[] normals;
    int[] triangles;
    Mesh mesh;

    // Distribution params that require a full reinitialization
    [Range(1, 100)] public int _PopulationMultiplier = 1;
    [Range(0.001f, 1.0f)] public float _SpawnChance = 1;

    // Distribution params that don't require a full reinitialization
    public bool requiresFullRestart = false;
    [Range(0.001f, 10f)] public float noiseScale;
    public Vector3 _PlanetOrigin = Vector3.zero;

    int numTriangles;

    // Stores count of distribution output
    int[] count = new int[] { 0, 0, 0 };
    uint[] indirectArgs = { 1, 1, 1 };

    bool readyForValidationChecks = false;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().sharedMesh;
        Initialize();
        InitializeDistribute();
        InitializeEvaluate();
        Distribute();

        readyForValidationChecks = true;
    }
    void OnValidate()
    {
        if (!readyForValidationChecks) { return; }

        // Remove this component (exclude in build) to prevent multiple components from being added
        scatterRenderer.scatterComponents.Remove(this);

        // Clean everything except for indirect args
        sourceVertsBuffer?.Dispose();
        sourceNormalsBuffer?.Dispose();
        sourceTrianglesBuffer?.Dispose();
        outputScatterDataBuffer?.Dispose();
        objectLimits?.Dispose();

        // Reinitialize all
        Initialize();
        InitializeDistribute();
        InitializeEvaluate();
        Distribute();
    }
    void Initialize()
    {
        scatterRenderer = gameObject.GetComponent<ScatterRenderer>();

        vertices = mesh.vertices;
        normals = mesh.normals;
        triangles = mesh.triangles;

        numTriangles = triangles.Length / 3;
    }
    void InitializeDistribute()
    {
        int outputSize = _PopulationMultiplier * numTriangles;

        scatterShader.SetInt("_PopulationMultiplier", _PopulationMultiplier);
        scatterShader.SetFloat("_SpawnChance", _SpawnChance);
        scatterShader.SetInt("_AlignToTerrainNormal", 0);
        scatterShader.SetInt("_MaxCount", outputSize);

        distributeKernel = scatterShader.FindKernel("Distribute");
        evaluateKernel = scatterShader.FindKernel("Evaluate");

        // Create compute buffers
        sourceVertsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        sourceNormalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        sourceTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int), ComputeBufferType.Structured);

        sourceVertsBuffer.SetData(vertices);
        sourceNormalsBuffer.SetData(normals);
        sourceTrianglesBuffer.SetData(triangles);

        outputScatterDataBuffer = new ComputeBuffer(outputSize, PositionData.Size(), ComputeBufferType.Append);

        scatterShader.SetBuffer(distributeKernel, "vertices", sourceVertsBuffer);
        scatterShader.SetBuffer(distributeKernel, "normals", sourceNormalsBuffer);
        scatterShader.SetBuffer(distributeKernel, "triangles", sourceTrianglesBuffer);
        scatterShader.SetBuffer(distributeKernel, "output", outputScatterDataBuffer);

        SetDistributionVars();
    }
    // These don't require a full reinitialization
    public void SetDistributionVars()
    {
        scatterShader.SetFloat("_NoiseScale", noiseScale);
    }
    public void Distribute()
    {
        outputScatterDataBuffer.SetCounterValue(0);

        // Dispatch 1 thread per triangle
        int dispatchCount = Mathf.CeilToInt((float)numTriangles / 32.0f);
        scatterShader.Dispatch(distributeKernel, dispatchCount, 1, 1);
        ComputeDispatchArgs();
    }
    // Processing an async readback request is faster
    // than dispatching another compute shader to get the count
    public void ComputeDispatchArgs()
    {
        // Stores the count of the generated positions
        objectLimits = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
        objectLimits.SetData(indirectArgs);

        // Read count from AppendStructuredBuffer to the objectlimits, used in the early return check from evaluate - can't process more data than exists
        scatterShader.SetBuffer(evaluateKernel, "objectLimits", objectLimits);
        ComputeBuffer.CopyCount(outputScatterDataBuffer, objectLimits, 0);

        // Read this back to construct indirect dispatch args
        AsyncGPUReadback.Request(objectLimits, OnDistributeComplete);
    }
    public void OnDistributeComplete(AsyncGPUReadbackRequest request)
    {
        count = request.GetData<int>().ToArray(); //Creates garbage, unfortunate

        // Initialise indirect args
        count[0] = Mathf.CeilToInt((float)count[0] / 32f);
        count[1] = 1;
        count[2] = 1;

        dispatchArgs = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
        dispatchArgs.SetData(count);

        // Ready to start evaluating
        scatterRenderer.scatterComponents.Add(this);
    }
    public void InitializeEvaluate()
    {
        scatterShader.SetBuffer(evaluateKernel, "positions", outputScatterDataBuffer);
        scatterShader.SetBuffer(evaluateKernel, "instancingData", scatterRenderer.outputLOD0);
    }
    public void Evaluate()
    {
        // Update runtime shader vars
        // We need to know the size of the distribution before continuing with this
        scatterShader.SetMatrix("_ObjectToWorldMatrix", transform.localToWorldMatrix);
        scatterShader.SetVector("_PlanetNormal", Vector3.Normalize(transform.position - _PlanetOrigin));

        scatterShader.SetVector("_WorldSpaceCameraPosition", Camera.main.transform.position);
        
        scatterShader.SetFloats("_CameraFrustumPlanes", CameraUtils.scatterPlaneNormals);
        scatterShader.SetFloat("_CullRadius", 0.05f);
        scatterShader.SetFloat("_CullLimit", 0);
        scatterShader.SetFloat("_MaxRange", 5);

        scatterShader.DispatchIndirect(evaluateKernel, dispatchArgs, 0);
    }
    void Cleanup()
    {
        // Don't use this in KSP, this is a linear search
        if (scatterRenderer != null)
        {
            scatterRenderer.scatterComponents.Remove(this);
        }

        sourceVertsBuffer?.Dispose();
        sourceNormalsBuffer?.Dispose();
        sourceTrianglesBuffer?.Dispose();
        outputScatterDataBuffer?.Dispose();
        dispatchArgs?.Dispose();
        objectLimits?.Dispose();
    }
    void OnDisable()
    {
        Cleanup();
    }
}
