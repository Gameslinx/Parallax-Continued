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
        public Scatter scatter;
        // Output evaluate results to the renderer
        ScatterRenderer scatterRenderer;

        // Contains our distribute and evaluate kernels
        public ComputeShader scatterShader;
        int distributeKernel;
        int evaluateKernel;

        // Output distribution -> evaluation buffer
        public ComputeBuffer outputScatterDataBuffer;

        // Evaluation buffers
        private ComputeBuffer dispatchArgs;
        private ComputeBuffer objectLimits;

        // Distribution params that don't require a full reinitialization
        public bool requiresFullRestart = false;

        int numTriangles;
        int outputSize;

        // Stores count of distribution output
        int[] count = new int[] { 0, 0, 0 };
        uint[] indirectArgs = { 1, 1, 1 };

        bool eventAdded = false;
        public bool cleaned = false;

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
        }
        void Initialize()
        {
            cleaned = false;
            scatterRenderer = ScatterManager.Instance.fastScatterRenderers[scatter.scatterName];
            numTriangles = parent.numMeshTriangles;
            outputSize = scatter.distributionParams.populationMultiplier * numTriangles;
        }
        void InitializeDistribute()
        {
            scatterShader = ConfigLoader.computeShaderPool.Fetch();

            // Required values
            scatterShader.SetFloat("_PlanetRadius", parent.planetRadius);
            scatterShader.SetInt("_MaxCount", outputSize);
            scatterShader.SetInt("_NumberOfBiomes", scatter.biomeCount);

            scatterShader.SetVector("_PlanetNormal", parent.planetNormal);
            scatterShader.SetVector("_LocalPlanetNormal", parent.localPlanetNormal);
            scatterShader.SetVector("_PlanetOrigin", parent.planetOrigin);

            scatterShader.SetMatrix("_ObjectToWorldMatrix", parent.quad.gameObject.transform.localToWorldMatrix);

            distributeKernel = GetDistributeKernel();
            evaluateKernel = scatterShader.FindKernel("Evaluate");

            outputScatterDataBuffer = new ComputeBuffer(outputSize, PositionData.Size(), ComputeBufferType.Append);
            scatterShader.SetBuffer(distributeKernel, "output", outputScatterDataBuffer);
            scatterShader.SetBuffer(evaluateKernel, "positions", outputScatterDataBuffer);

            scatterShader.SetBuffer(distributeKernel, "vertices", parent.sourceVertsBuffer);
            scatterShader.SetBuffer(distributeKernel, "normals", parent.sourceNormalsBuffer);
            scatterShader.SetBuffer(distributeKernel, "triangles", parent.sourceTrianglesBuffer);
            scatterShader.SetBuffer(distributeKernel, "uvs", parent.sourceUVsBuffer);
            scatterShader.SetBuffer(distributeKernel, "directionsFromCenter", parent.sourceDirsFromCenterBuffer);
            
            scatterShader.SetTexture(distributeKernel, "biomeMap", ScatterManager.currentBiomeMap);
            scatterShader.SetTexture(distributeKernel, "scatterBiomes", scatter.biomeControlMap);

            SetDistributionVars();
        }
        //
        //  NOTE: PARITY MUST BE MAINTAINED WITH TerrainScatters.compute KERNEL DEFINITIONS
        //
        public int GetDistributeKernel()
        {
            int kernel = 0;

            // Noise type
            switch (scatter.noiseParams.noiseType)
            {
                case NoiseType.simplexPerlin:
                {
                    kernel = 0;
                    break;
                }
                case NoiseType.simplexCellular:
                {
                    kernel = 1;
                    break;
                }
                case NoiseType.simplexPolkaDot:
                {
                    kernel = 2;
                    break;
                }
            }
            if (scatter.noiseParams.inverted)
            {
                // Use the kernels with inverted noise
                kernel = 3;
            }
            return kernel;
        }
        public void SetDistributionVars()
        {
            // Values from config
            scatterShader.SetInt("_PopulationMultiplier", scatter.distributionParams.populationMultiplier);
            scatterShader.SetFloat("_SpawnChance", scatter.distributionParams.spawnChance);
            scatterShader.SetInt("_AlignToTerrainNormal", scatter.distributionParams.alignToTerrainNormal);
            scatterShader.SetFloat("_MaxNormalDeviance", scatter.distributionParams.maxNormalDeviance);
            scatterShader.SetFloat("_Seed", scatter.distributionParams.seed);

            scatterShader.SetVector("_MinScale", scatter.distributionParams.minScale);
            scatterShader.SetVector("_MaxScale", scatter.distributionParams.maxScale);
            scatterShader.SetFloat("_ScaleRandomness", scatter.distributionParams.scaleRandomness);

            scatterShader.SetInt("_InvertNoise", scatter.noiseParams.inverted ? 1 : 0);
            scatterShader.SetInt("_NoiseOctaves", scatter.noiseParams.octaves);
            scatterShader.SetFloat("_NoiseFrequency", scatter.noiseParams.frequency);
            scatterShader.SetFloat("_NoiseLacunarity", scatter.noiseParams.lacunarity);
            scatterShader.SetInt("_NoiseSeed", scatter.noiseParams.seed);
            scatterShader.SetFloat("_NoiseCutoffThreshold", scatter.distributionParams.noiseCutoff);

            scatterShader.SetFloat("_SteepPower", scatter.distributionParams.steepPower);
            scatterShader.SetFloat("_SteepContrast", scatter.distributionParams.steepContrast);
            scatterShader.SetFloat("_SteepMidpoint", scatter.distributionParams.steepMidpoint);

            scatterShader.SetFloat("_MinAltitude", scatter.distributionParams.minAltitude);
            scatterShader.SetFloat("_MaxAltitude", scatter.distributionParams.maxAltitude);
            scatterShader.SetFloat("_AltitudeFadeRange", scatter.distributionParams.altitudeFadeRange);

            scatterShader.SetFloat("_RangeFadeStart", ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange);
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

            // This function may never execute because the quad tries to cleanup before the readback is complete
            // So add this guard to prevent trying to remove the event before it was ever added
            eventAdded = true;
        }
        public void InitializeEvaluate()
        {
            scatterShader.SetBuffer(evaluateKernel, "triangles", parent.sourceTrianglesBuffer);
            scatterShader.SetBuffer(evaluateKernel, "vertices", parent.sourceVertsBuffer);

            scatterShader.SetBuffer(evaluateKernel, "instancingDataLOD0", scatterRenderer.outputLOD0);
            scatterShader.SetBuffer(evaluateKernel, "instancingDataLOD1", scatterRenderer.outputLOD1);
            scatterShader.SetBuffer(evaluateKernel, "instancingDataLOD2", scatterRenderer.outputLOD2);

            scatterShader.SetFloat("_CullRange", scatter.optimizationParams.frustumCullingIgnoreRadius);
            scatterShader.SetFloat("_CullLimit", scatter.optimizationParams.frustumCullingSafetyMargin);

            scatterShader.SetFloat("_MaxRange", scatter.distributionParams.range);
            scatterShader.SetFloat("_Lod01Split", scatter.distributionParams.lod1.range);
            scatterShader.SetFloat("_Lod12Split", scatter.distributionParams.lod2.range);
        }
        public void Evaluate()
        {
            // Is it even worth evaluating this scatter?
            // Quad is not in the view frustum
            if (!parent.quad.meshRenderer.isVisible || !parent.quad.isVisible) { return; }
            // Quad is out of range
            if (parent.cameraDistance > scatter.distributionParams.range + Mathf.Sqrt(parent.sqrQuadWidth)) { return; }
            if (!eventAdded) { return; }

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

            ConfigLoader.computeShaderPool.Add(scatterShader);

            outputScatterDataBuffer?.Dispose();
            dispatchArgs?.Dispose();
            objectLimits?.Dispose();
        }
    }
}
