using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine;

namespace Parallax
{
    // One of these per scatter per quad
    // Main bulk of scatter system processing
    public class ScatterData
    {
        // Grab reference to parent quad to access mesh data
        ScatterSystemQuadData parent;
        // The scatter we're generating
        Scatter scatter;
        // Output evaluate results to the renderer
        ScatterRenderer scatterRenderer;

        // Contains our distribute and evaluate kernels
        public ComputeShader scatterShader;
        int distributeKernel;
        int evaluateKernel;

        // Output distribution -> evaluation buffer
        public ComputeBuffer outputScatterDataBuffer;

        // Evaluation buffers
        ComputeBuffer dispatchArgs;
        ComputeBuffer objectLimits;

        // Distribution params that don't require a full reinitialization
        public bool requiresFullRestart = false;
        public float noiseScale = 1.0f;

        int numTriangles;

        // Stores count of distribution output
        int[] count = new int[] { 0, 0, 0 };
        uint[] indirectArgs = { 1, 1, 1 };

        bool readyForValidationChecks = false;
        bool eventAdded = false;
        bool cleaned = false;

        public ScatterData(ScatterSystemQuadData parent, Scatter scatter)
        {
            this.parent = parent;
            this.scatter = scatter;
        }
        public void Start()
        {
            Initialize();
            InitializeDistribute();
            InitializeEvaluate();
            Distribute();

            readyForValidationChecks = true;
        }
        void Initialize()
        {
            scatterRenderer = ScatterManager.Instance.fastScatterRenderers[scatter.scatterName];
            numTriangles = parent.numMeshTriangles;
        }
        void InitializeDistribute()
        {
            // For now....
            scatterShader = UnityEngine.Object.Instantiate(AssetBundleLoader.parallaxComputeShaders["TerrainScatters"]);

            int populationMultiplier = scatter.distributionParams.populationMultiplier;
            int outputSize = populationMultiplier * numTriangles;

            // Values from config
            scatterShader.SetInt("_PopulationMultiplier", populationMultiplier);
            scatterShader.SetInt("_AlignToTerrainNormal", 0);
            scatterShader.SetFloat("_SpawnChance", scatter.distributionParams.spawnChance);

            // Required values
            scatterShader.SetVector("_PlanetNormal", Vector3.Normalize(parent.quad.transform.position - parent.quad.quadRoot.transform.position));
            scatterShader.SetInt("_MaxCount", outputSize);

            distributeKernel = scatterShader.FindKernel("Distribute");
            evaluateKernel = scatterShader.FindKernel("Evaluate");

            outputScatterDataBuffer = new ComputeBuffer(outputSize, PositionData.Size(), ComputeBufferType.Append);

            scatterShader.SetBuffer(distributeKernel, "vertices", parent.sourceVertsBuffer);
            scatterShader.SetBuffer(distributeKernel, "normals", parent.sourceNormalsBuffer);
            scatterShader.SetBuffer(distributeKernel, "triangles", parent.sourceTrianglesBuffer);
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
            // Data was cleaned up before generation completed - stop here
            if (cleaned) { return; }
            count = request.GetData<int>().ToArray(); //Creates garbage, unfortunate

            // Initialise indirect args
            count[0] = Mathf.CeilToInt((float)count[0] / 32f);
            count[1] = 1;
            count[2] = 1;

            dispatchArgs = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            dispatchArgs.SetData(count);

            // Ready to start evaluating
            scatterRenderer.onEvaluateScatters += Evaluate;

            // This part of the code may never execute because the quad tries to cleanup before the readback is complete
            // So add this guard to prevent trying to remove the event before it was ever added
            eventAdded = true;
        }
        public void InitializeEvaluate()
        {
            scatterShader.SetBuffer(evaluateKernel, "positions", outputScatterDataBuffer);
            scatterShader.SetBuffer(evaluateKernel, "instancingData", scatterRenderer.outputLOD0);

            scatterShader.SetFloat("_CullRange", scatter.optimizationParams.frustumCullingIgnoreRadius);
            scatterShader.SetFloat("_CullLimit", scatter.optimizationParams.frustumCullingSafetyMargin);

            scatterShader.SetFloat("_MaxRange", scatter.distributionParams.range);
        }
        public void Evaluate()
        {
            // Is it even worth evaluating this scatter?
            if (!parent.quad.isVisible) { return; }
            
            // Update runtime shader vars
            // We need to know the size of the distribution before continuing with this
            scatterShader.SetMatrix("_ObjectToWorldMatrix", parent.quad.gameObject.transform.localToWorldMatrix);
            scatterShader.SetVector("_PlanetNormal", Vector3.Normalize(parent.quad.gameObject.transform.position - parent.quad.sphereRoot.gameObject.transform.position));
            scatterShader.SetFloats("_CameraFrustumPlanes", RuntimeOperations.floatCameraFrustumPlanes);
            scatterShader.SetVector("_WorldSpaceCameraPosition", RuntimeOperations.vectorCameraPos);

            scatterShader.DispatchIndirect(evaluateKernel, dispatchArgs, 0);
        }
        public void Cleanup()
        {
            // Check against this in the readback to stop us from adding an event after the data is cleaned up
            cleaned = true;

            if (eventAdded)
            {
                scatterRenderer.onEvaluateScatters -= Evaluate;
                eventAdded = false;
            }

            outputScatterDataBuffer?.Dispose();
            dispatchArgs?.Dispose();
            objectLimits?.Dispose();
        }
    }
}
