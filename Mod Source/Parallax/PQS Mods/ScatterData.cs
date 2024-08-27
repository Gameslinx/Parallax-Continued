using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine;
using static Parallax.Legacy.LegacyConfigLoader;
using UnityEngine.Profiling;
using Unity.Collections;
using Parallax.Tools;

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
        public ScatterRenderer scatterRenderer;

        // Collider data, if needed
        public ScatterColliderData collisionData;

        // Contains our distribute and evaluate kernels
        public ComputeShader scatterShader;
        int distributeKernel;
        int evaluateKernel;

        // Output distribution -> evaluation buffer
        public ComputeBuffer outputScatterDataBuffer;

        // Evaluation buffers
        private ComputeBuffer dispatchArgs;
        private ComputeBuffer objectLimits;

        int numTriangles;
        int outputSize;
        int maxCount;

        // Stores count of distribution output
        int[] count = new int[] { 0, 0, 0 };
        uint[] indirectArgs = { 1, 1, 1 };
        int realCount = 0;

        bool eventAdded = false;
        bool collidersAdded = false;
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
            Distribute();
        }
        void Initialize()
        {
            cleaned = false;
            scatterRenderer = ScatterManager.Instance.fastScatterRenderers[scatter.scatterName];
            numTriangles = parent.numMeshTriangles;

            outputSize = scatter.distributionParams.populationMultiplier * numTriangles * (int)Mathf.Pow(2, parent.quad.sphereRoot.maxLevel - parent.quad.subdivision);
            maxCount = outputSize;

            // Scale the output size by the relative density mult and spawn chance, overshoot slightly for safety
            if (parent.sphereRelativeDensityMult < 0.92)
            {
                outputSize = Mathf.CeilToInt((float)outputSize * (parent.sphereRelativeDensityMult + 0.08f));
            }
            
            if (outputSize > 100 && scatter.distributionParams.spawnChance < 0.92)
            {
                outputSize = Mathf.CeilToInt((float)outputSize * (scatter.distributionParams.spawnChance + 0.08f));
            }
        }
        void InitializeDistribute()
        {
            scatterShader = scatter.shader;

            // Required values
            scatterShader.SetFloat(ParallaxScatterShaderProperties.planetRadiusID, parent.planetRadius);
            scatterShader.SetInt(ParallaxScatterShaderProperties.maxCountID, maxCount);
            scatterShader.SetInt(ParallaxScatterShaderProperties.numberOfBiomesID, scatter.biomeCount);

            scatterShader.SetVector(ParallaxScatterShaderProperties.planetNormalID, parent.planetNormal);
            scatterShader.SetVector(ParallaxScatterShaderProperties.localPlanetNormalID, parent.localPlanetNormal);
            scatterShader.SetVector(ParallaxScatterShaderProperties.planetOriginID, parent.planetOrigin);


            scatterShader.SetMatrix(ParallaxScatterShaderProperties.objectToWorldMatrixID, parent.quad.meshRenderer.localToWorldMatrix);
            scatterShader.SetMatrix(ParallaxScatterShaderProperties.worldToObjectMatrixID, parent.quad.meshRenderer.worldToLocalMatrix);

            distributeKernel = GetDistributeKernel();
            evaluateKernel = GetEvaluateKernel();

            outputScatterDataBuffer = new ComputeBuffer(outputSize, PositionData.Size(), ComputeBufferType.Append);
            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.transformsBufferID, outputScatterDataBuffer);
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.positionsBufferID, outputScatterDataBuffer);

            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.parentVertsBufferID, parent.sourceVertsBuffer);
            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.parentNormalsBufferID, parent.sourceNormalsBuffer);
            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.parentTrisBufferID, parent.sourceTrianglesBuffer);
            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.parentUVsBufferID, parent.sourceUVsBuffer);
            scatterShader.SetBuffer(distributeKernel, ParallaxScatterShaderProperties.parentDirsBufferID, parent.sourceDirsFromCenterBuffer);
            
            scatterShader.SetTexture(distributeKernel, ParallaxScatterShaderProperties.biomeMapTextureID, ScatterManager.currentBiomeMap);
            scatterShader.SetTexture(distributeKernel, ParallaxScatterShaderProperties.scatterBiomesTextureID, scatter.biomeControlMap);

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
            return kernel;
        }
        //
        //  Note: PARITY MUST BE MAINTAINED WITH TerrainScatters.compute KERNEL DEFINITIONS
        //
        public int GetEvaluateKernel()
        {
            int kernel = 3;
            if (scatter.distributionParams.coloredByTerrain)
            {
                kernel = 4;
            }
            return kernel;
        }
        public void SetDistributionVars()
        {
            // Values from config
            scatterShader.SetInt(ParallaxScatterShaderProperties.populationMultiplierID, scatter.distributionParams.populationMultiplier * (int)Mathf.Pow(2, parent.quad.sphereRoot.maxLevel - parent.quad.subdivision));
            scatterShader.SetFloat(ParallaxScatterShaderProperties.spawnChanceID, scatter.distributionParams.spawnChance * parent.sphereRelativeDensityMult);
            scatterShader.SetInt(ParallaxScatterShaderProperties.alignToTerrainNormalID, scatter.distributionParams.alignToTerrainNormal);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.maxNormalDevianceID, scatter.distributionParams.maxNormalDeviance);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.seedID, scatter.distributionParams.seed);

            scatterShader.SetVector(ParallaxScatterShaderProperties.minScaleID, scatter.distributionParams.minScale);
            scatterShader.SetVector(ParallaxScatterShaderProperties.maxScaleID, scatter.distributionParams.maxScale);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.scaleRandomnessID, scatter.distributionParams.scaleRandomness);

            scatterShader.SetInt(ParallaxScatterShaderProperties.invertNoiseID, scatter.noiseParams.inverted ? 1 : 0);
            scatterShader.SetInt(ParallaxScatterShaderProperties.noiseOctavesID, scatter.noiseParams.octaves);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.noiseFrequencyID, scatter.noiseParams.frequency);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.noiseLacunarityID, scatter.noiseParams.lacunarity);
            scatterShader.SetInt(ParallaxScatterShaderProperties.noiseSeedID, scatter.noiseParams.seed);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.noiseCutoffThresholdID, scatter.distributionParams.noiseCutoff);

            scatterShader.SetFloat(ParallaxScatterShaderProperties.steepPowerID, scatter.distributionParams.steepPower);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.steepContrastID, scatter.distributionParams.steepContrast);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.steepMidpointID, scatter.distributionParams.steepMidpoint);

            scatterShader.SetFloat(ParallaxScatterShaderProperties.minAltitudeID, scatter.distributionParams.minAltitude);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.maxAltitudeID, scatter.distributionParams.maxAltitude);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.altitudeFadeRangeID, scatter.distributionParams.altitudeFadeRange);

            scatterShader.SetFloat(ParallaxScatterShaderProperties.rangeFadeStartID, ConfigLoader.parallaxGlobalSettings.scatterGlobalSettings.fadeOutStartRange);

            // If we're distributing to a fixed altitude
            if (scatter.distributionParams.fixedAltitude)
            {
                scatterShader.SetInt(ParallaxScatterShaderProperties.distributeFixedAltitudeID, 1);
                scatterShader.SetFloat(ParallaxScatterShaderProperties.fixedAltitudeID, scatter.distributionParams.placementAltitude);
            }
            else
            {
                scatterShader.SetInt(ParallaxScatterShaderProperties.distributeFixedAltitudeID, 0);
            }
        }
        public void Distribute()
        {
            outputScatterDataBuffer.SetCounterValue(0);

            // Dispatch 1 thread per triangle
            int dispatchCount = Mathf.CeilToInt((float)numTriangles / 32.0f);
            scatterShader.Dispatch(distributeKernel, dispatchCount, 1, 1);

            // Prepare for evaluation
            ComputeDispatchArgs();
        }

        // Processing an async readback request is faster than dispatching another compute shader to get the count
        public void ComputeDispatchArgs()
        {
            // Stores the count of the generated positions
            objectLimits = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            objectLimits.SetData(indirectArgs);

            // Read count from AppendStructuredBuffer to the objectlimits, used in the early return check from evaluate - can't process more data than exists
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.objectLimitsBufferID, objectLimits);
            ComputeBuffer.CopyCount(outputScatterDataBuffer, objectLimits, 0);

            // Read this back to construct indirect dispatch args
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(objectLimits, OnDistributeComplete);
            }
            else
            {
                ImmediateReadback();
            }
        }
        public void OnDistributeComplete(AsyncGPUReadbackRequest request)
        {
            // Data was cleaned up before generation completed - stop here
            if (cleaned) 
            { 
                return; 
            }
            count = request.GetData<int>().ToArray(); //Creates garbage, unfortunate
            realCount = count[0];
            // Process collider data, if this scatter is collideable
            // Todo: Make this a GetData if initializing the scene for the first time so all colliders are here on time
            // If we're paused, the colliders already exist
            if (CollidersEligible() && !collidersAdded)
            {
                AsyncGPUReadback.Request(outputScatterDataBuffer, OnColliderReadbackComplete);
            }

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
        /// <summary>
        /// For systems that don't support asyncGPUReadback - such as OpenGL
        /// </summary>
        public void ImmediateReadback()
        {
            // Data was cleaned up before generation completed - stop here
            if (cleaned)
            {
                return;
            }

            count = new int[3];
            objectLimits.GetData(count);
            realCount = count[0];

            // Process collider data, if this scatter is collideable
            // Todo: Make this a GetData if initializing the scene for the first time so all colliders are here on time
            // If we're paused, the colliders already exist
            if (CollidersEligible() && !collidersAdded)
            {
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    AsyncGPUReadback.Request(outputScatterDataBuffer, OnColliderReadbackComplete);
                }
                else
                {
                    DirectColliderReadback();
                }
            }

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

        // Request the output buffer for collider processing
        public void OnColliderReadbackComplete(AsyncGPUReadbackRequest req)
        {
            if (cleaned || collidersAdded)
            {
                return;
            }

            // Note - GetData requests the entire buffer!! Not just what we have appended, so most of this data is full of zeroes... FML
            // Use slices to get the data we're interested in
            // When stuff like grass is set to collideable this can take a few ms, otherwise pretty quick
            NativeArray<PositionData> data = req.GetData<PositionData>();

            // If we estimated the buffer size incorrectly, the buffer count is higher than the maximum buffer length
            // And we should appropriately account for it
            if (realCount > data.Length)
            {
                ParallaxDebug.Log("Warning: Received data count was greater than the buffer length. Not serious, but worth reporting as an error.");
            }
            int colliderDataCount = Mathf.Min(realCount, data.Length);

            NativeArray<PositionData> realData = new NativeArray<PositionData>(colliderDataCount, Allocator.Persistent);

            NativeSlice<PositionData> slice1 = new NativeSlice<PositionData>(data, 0, colliderDataCount);
            NativeSlice<PositionData> slice2 = new NativeSlice<PositionData>(realData);

            slice2.CopyFrom(slice1);
            data.Dispose();

            collisionData = new ScatterColliderData(parent, realData, scatter.collideableArrayIndex);
            CollisionManager.QueueIncomingData(collisionData);
            collidersAdded = true;
        }
        // For systems that do not support asyncGPUReadback - such as OpenGL
        public void DirectColliderReadback()
        {
            if (cleaned)
            {
                return;
            }

            // Note - GetData requests the entire buffer!! Not just what we have appended, so most of this data is full of zeroes... FML
            // Use slices to get the data we're interested in
            // When stuff like grass is set to collideable this can take a few ms, otherwise pretty quick
            PositionData[] data = new PositionData[realCount];
            outputScatterDataBuffer.GetData(data);

            NativeArray<PositionData> nativeData = new Unity.Collections.NativeArray<PositionData>(data, Allocator.Persistent);

            collisionData = new ScatterColliderData(parent, nativeData, scatter.collideableArrayIndex);
            CollisionManager.QueueIncomingData(collisionData);
            collidersAdded = true;
        }
        // Evaluate which objects are in range, what LODs to show, and frustum cull them
        // This is called very often, every frame. All calls combined take around 0.4 to 0.5ms CPU
        public bool paused = false;
        public void Evaluate()
        {
            // There aren't any of this scatter on this quad
            if (count[0] == 0) { return; }

            // Quad is not in the view frustum or used for shadow rendering
            if (!parent.quad.meshRenderer.isVisible) { return; }

            // Quad is out of range
            if (parent.cameraDistance > scatter.distributionParams.range * scatter.distributionParams.range + parent.sqrQuadWidth)
            {
                return;
            }

            if (paused) { return; }

            // Not finished distributing yet
            if (!eventAdded) { return; }

            // Point the scatter's compute shader buffers to this quad's
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.parentTrisBufferID, parent.sourceTrianglesBuffer);
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.parentVertsBufferID, parent.sourceVertsBuffer);

            if (scatter.distributionParams.coloredByTerrain)
            {
                scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.parentColorsBufferID, parent.sourceColorsBuffer);
            }

            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.lod0BufferID, scatterRenderer.outputLOD0);
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.lod1BufferID, scatterRenderer.outputLOD1);
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.lod2BufferID, scatterRenderer.outputLOD2);

            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.objectLimitsBufferID, objectLimits);
            scatterShader.SetBuffer(evaluateKernel, ParallaxScatterShaderProperties.positionsBufferID, outputScatterDataBuffer);

            // Update runtime shader vars
            scatterShader.SetMatrix(ParallaxScatterShaderProperties.objectToWorldMatrixID, parent.quad.meshRenderer.localToWorldMatrix);
            scatterShader.SetVector(ParallaxScatterShaderProperties.planetNormalID, Vector3.Normalize(parent.quad.PrecisePosition - parent.quad.sphereRoot.PrecisePosition));
            scatterShader.SetFloats(ParallaxScatterShaderProperties.cameraFrustumPlanesID, RuntimeOperations.floatCameraFrustumPlanes);
            scatterShader.SetVector(ParallaxScatterShaderProperties.worldSpaceCameraPositionID, RuntimeOperations.vectorCameraPos);
            scatterShader.SetInt(   ParallaxScatterShaderProperties.maxCountID, realCount);

            // Update the culling and lod params
            scatterShader.SetFloat(ParallaxScatterShaderProperties.cullRadiusID, scatter.optimizationParams.frustumCullingIgnoreRadius);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.cullLimitID, scatter.optimizationParams.frustumCullingSafetyMargin);

            scatterShader.SetFloat(ParallaxScatterShaderProperties.maxRangeID, scatter.distributionParams.range);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.lod01SplitID, scatter.distributionParams.lod1.range);
            scatterShader.SetFloat(ParallaxScatterShaderProperties.lod12SplitID, scatter.distributionParams.lod2.range);

            //Debug.Log("E" + scatter.scatterName);

            scatterShader.DispatchIndirect(evaluateKernel, dispatchArgs, 0);
        }
        /// <summary>
        /// Returns true if the scatter is collideable and the quad is fully subdivided
        /// </summary>
        /// <returns></returns>
        public bool CollidersEligible()
        {
            return scatter.collideable && parent.quad.subdivision == parent.quad.sphereRoot.maxLevel && realCount > 0;
        }
        /// <summary>
        /// Pause all operations on this ScatterData. Frees buffers but keeps colliders
        /// </summary>
        public void Pause()
        {
            if (!paused)
            {
                outputScatterDataBuffer?.Dispose();
                dispatchArgs?.Dispose();
                objectLimits?.Dispose();

                paused = true;
                cleaned = true;

                if (eventAdded)
                {
                    scatterRenderer.onEvaluateScatters -= Evaluate;
                    eventAdded = false;
                }
            }
        }
        /// <summary>
        /// Reactivate this ScatterData after pausing
        /// </summary>
        public void Resume()
        {
            if (paused)
            {
                parent.Reinitialize();
                cleaned = false;
                
                Start();
                paused = false;
            }
        }
        public void Cleanup()
        {
            // Check against this in the readback to stop us from adding an event after the data is cleaned up
            cleaned = true;

            // Remove event
            if (eventAdded)
            {
                scatterRenderer.onEvaluateScatters -= Evaluate;
                eventAdded = false;
            }

            // Queue up collider removal
            if (collidersAdded)
            {
                CollisionManager.QueueOutgoingData(collisionData);
            }

            outputScatterDataBuffer?.Dispose();
            dispatchArgs?.Dispose();
            objectLimits?.Dispose();
        }
    }
}
