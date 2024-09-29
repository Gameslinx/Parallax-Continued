using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace Parallax
{
    //
    // Terrain Shader Quad Data
    // One per quad, handles parallax terrain shader subdivision and material swapping
    //
    public class TerrainShaderQuadData
    {
        public Material quadMaterial;
        public ParallaxTerrainBody body;

        GameObject newQuad;
        JobifiedSubdivision subdivisionComponent;

        MeshRenderer quadMeshRenderer;
        Mesh mesh;

        public PQ quad;
        public int subdivisionLevel;
        public float subdivisionRadius;
        public bool isMaxLevel;
        public float quadWidth;

        float blendLowMidStart;
        float blendLowMidEnd;
        float blendMidHighStart;
        float blendMidHighEnd;

        bool alreadyInitialized = false;
        bool materialCreated = false;

        bool probeEventAdded = false;
        public TerrainShaderQuadData(PQ quad, int subdivisionLevel, float subdivisionRadius, bool isMaxLevel)
        {
            this.quad = quad;
            this.subdivisionLevel = subdivisionLevel;
            this.subdivisionRadius = subdivisionRadius;
            this.isMaxLevel = isMaxLevel;
        }
        // Get all required properties on the planet
        public void Initialize()
        {
            body = ConfigLoader.parallaxTerrainBodies[quad.sphereRoot.name];

            blendLowMidStart = body.terrainShaderProperties.shaderFloats["_LowMidBlendStart"];
            blendLowMidEnd = body.terrainShaderProperties.shaderFloats["_LowMidBlendEnd"];
            blendMidHighStart = body.terrainShaderProperties.shaderFloats["_MidHighBlendStart"];
            blendMidHighEnd = body.terrainShaderProperties.shaderFloats["_MidHighBlendEnd"];

            quadMaterial = DetermineMaterial();
            
            if (body.emissive)
            {
                quadMaterial.EnableKeyword("EMISSION");
            }
            if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.advancedTextureBlending)
            {
                quadMaterial.EnableKeyword("ADVANCED_BLENDING");
            }
            if (ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.ambientOcclusion)
            {
                quadMaterial.EnableKeyword("AMBIENT_OCCLUSION");
            }

            quadMeshRenderer = quad.gameObject.GetComponent<MeshRenderer>();

            if (isMaxLevel)
            {
                // Sadly a requirement or quad meshes become corrupt
                mesh = new Mesh();
                
                quadWidth = (float)((2f * Mathf.PI * quad.sphereRoot.radius / 4f) / (Mathf.Pow(2f, quad.sphereRoot.maxLevel)));
                quadWidth *= quadWidth;

                EventHandler.OnQuadRangeCheck += RangeCheck;
                RangeCheck();
            }
            else
            {
                SwapMaterial(false);
            }

            // Quads can build before the flight scene is ready. Quads built after will call this fine, but quads built before need to rely on the event
            if (RuntimeOperations.flightProbeObject != null)
            {
                //SetReflectionProbeAnchor(RuntimeOperations.flightProbeObject.transform);
            }
            else
            {
                //RuntimeOperations.onFlightReflectionProbeReady += SetReflectionProbeAnchor;
                probeEventAdded = true;
            }
        }
        // Params to avoid garbage gen
        Vector3 worldOrigin = Vector3.zero;
        float dist = 0;
        // Quad normals changed, we need to reinitialize the mesh
        public void OnQuadNormalUpdate()
        {
            if (isMaxLevel)
            {
                if (alreadyInitialized)
                {
                    OutOfRange();
                }
                //mesh.normals = quad.GetComponent<MeshFilter>().sharedMesh.normals;
                //mesh.co
            }
        }
        /// <summary>
        /// Get the quad mesh again, usually after it's modified, but maybe you want to just call it for fun
        /// </summary>
        public bool UpdateMesh()
        {
            // The normals will update again, wait
            if (quad.isQueuedForNormalUpdate)
            {
                return false;
            }

            // I'd rather not do this but accessing quad mesh properties in any way outside of instantiation completely fucks the quads
            // Removing this will improve quad build times a fair bit
            mesh = UnityEngine.Object.Instantiate(quad.mesh);
            return true;
        }
        public float GetSqrQuadCameraDistance(in Vector3 quadPosition, in Vector3 cameraPosition)
        {
            return (quadPosition - cameraPosition).sqrMagnitude;
        }
        public void RangeCheck()
        {
            dist = GetSqrQuadCameraDistance(quad.PrecisePosition, RuntimeOperations.vectorCameraPos);
            // We're within range
            if (dist < quadWidth)
            {
                if (!alreadyInitialized)
                {
                    if (UpdateMesh())
                    {
                        CreateFakeQuad();
                        subdivisionComponent = newQuad.AddComponent<JobifiedSubdivision>();
                        subdivisionComponent.maxSubdivisionLevel = subdivisionLevel;
                        subdivisionComponent.subdivisionRange = subdivisionRadius;
                        subdivisionComponent.mesh = newQuad.GetComponent<MeshFilter>().sharedMesh;

                        materialCreated = false;
                    }
                }
            }
            // Out of range, and need to clean up the fake quad
            if (dist > quadWidth && alreadyInitialized)
            {
                // This calls cleanup of jobified subdivision, completing all pending jobs
                // Consider preventing jobs from being started when the quad is GOING out of range, so they complete before then
                //newQuad.SetActive(false);
                OutOfRange();
            }
            if (dist > quadWidth && !materialCreated)
            {
                SwapMaterial(false);
                materialCreated = true;
            }
        }
        // We can't edit the meshrenderer or meshfilter on the quad, or everything will disintigrate spectacularly
        // So we need to make a fake visual quad and hide the real one

        // Move this to an object pool
        public void CreateFakeQuad()
        {
            newQuad = new GameObject(quad.name);

            newQuad.transform.position = quad.gameObject.transform.position;
            newQuad.transform.rotation = quad.gameObject.transform.rotation;
            newQuad.transform.localScale = quad.gameObject.transform.localScale;

            newQuad.transform.parent = quad.gameObject.transform;
            newQuad.layer = quad.gameObject.layer;
            newQuad.tag = quad.gameObject.tag;

            MeshFilter fakeQuadMeshFilter = newQuad.AddComponent<MeshFilter>();
            MeshRenderer fakeQuadMeshRenderer = newQuad.AddComponent<MeshRenderer>();

            fakeQuadMeshFilter.sharedMesh = mesh;
            fakeQuadMeshRenderer.sharedMaterial = quadMaterial;

            newQuad.SetActive(true);

            SwapMaterial(true);

            alreadyInitialized = true;
        }
        public Material DetermineMaterial()
        {
            if (ConfigLoader.parallaxGlobalSettings.debugGlobalSettings.wireframeTerrain)
            {
                return ConfigLoader.wireframeMaterial;
            }
            double quadLowAltitude = quad.meshVertMin;
            double quadHighAltitude = quad.meshVertMax;

            // This quad uses all three textures
            if (quadLowAltitude < blendLowMidEnd && quadHighAltitude > blendMidHighStart)
            {
                body.parallaxMaterials.parallaxFull.EnableKeyword("PARALLAX_FULL");
                return body.parallaxMaterials.parallaxFull;
            }

            // This quad uses entirely 'High' texture
            if (quadLowAltitude > blendMidHighEnd)
            {
                body.parallaxMaterials.parallaxHigh.EnableKeyword("PARALLAX_SINGLE_HIGH");
                return body.parallaxMaterials.parallaxHigh;
            }

            // This quad uses entirely 'Mid' texture
            if (quadLowAltitude > blendLowMidEnd && quadHighAltitude < blendMidHighStart)
            {
                body.parallaxMaterials.parallaxMid.EnableKeyword("PARALLAX_SINGLE_MID");
                return body.parallaxMaterials.parallaxMid;
            }

            // This quad uses entirely 'Low' texture
            if (quadHighAltitude < blendLowMidStart)
            {
                body.parallaxMaterials.parallaxLow.EnableKeyword("PARALLAX_SINGLE_LOW");
                return body.parallaxMaterials.parallaxLow;
            }

            // This quad uses 'Low' and 'Mid' textures
            // Since any other combination has already been returned
            if ((quadLowAltitude < blendLowMidStart && quadHighAltitude > blendLowMidEnd) || (quadLowAltitude < blendLowMidStart && quadHighAltitude < blendLowMidEnd && quadHighAltitude > blendLowMidStart) || (quadLowAltitude > blendLowMidStart && quadLowAltitude < blendLowMidEnd && quadHighAltitude > blendLowMidEnd) || (quadLowAltitude > blendLowMidStart && quadLowAltitude < blendLowMidEnd && quadHighAltitude > blendLowMidStart && quadHighAltitude < blendLowMidEnd))
            {
                body.parallaxMaterials.parallaxLowMid.EnableKeyword("PARALLAX_DOUBLE_LOWMID");
                return body.parallaxMaterials.parallaxLowMid;
            }

            // This quad uses 'Mid' and 'high' textures
            // Since any other combination has already been returned
            if ((quadLowAltitude < blendMidHighStart && quadHighAltitude > blendMidHighEnd) || (quadLowAltitude < blendMidHighStart && quadHighAltitude < blendMidHighEnd && quadHighAltitude > blendMidHighStart) || (quadLowAltitude > blendMidHighStart && quadLowAltitude < blendMidHighEnd && quadHighAltitude > blendMidHighEnd) || (quadLowAltitude > blendMidHighStart && quadLowAltitude < blendMidHighEnd && quadHighAltitude > blendMidHighStart && quadHighAltitude < blendMidHighEnd))
            {
                body.parallaxMaterials.parallaxMidHigh.EnableKeyword("PARALLAX_DOUBLE_MIDHIGH");
                return body.parallaxMaterials.parallaxMidHigh;
            }

            // This quad uses all three textures - fallback to this
            body.parallaxMaterials.parallaxFull.EnableKeyword("PARALLAX_FULL");
            return body.parallaxMaterials.parallaxFull;
        }
        public void SwapMaterial(bool inSubdivisionRange)
        {
            if (inSubdivisionRange)
            {
                quadMeshRenderer.material = ConfigLoader.transparentMaterial;
            }
            else
            {
                quadMeshRenderer.sharedMaterial = quadMaterial;
                quadMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
            }
        }
        public void OutOfRange()
        {
            UnityEngine.Object.Destroy(newQuad);
            quadMeshRenderer.sharedMaterial = quadMaterial;
            alreadyInitialized = false;
        }
        public void SetReflectionProbeAnchor(Transform probeTransform)
        {
            quadMeshRenderer.probeAnchor = probeTransform;
        }

        /// <summary>
        /// Called from the GUI if the subdivision level is changed
        /// </summary>
        public void UpdateSubdivision(int newLevel, float newRadius)
        {
            subdivisionLevel = newLevel;
            subdivisionRadius = newRadius;
            if (alreadyInitialized)
            {
                subdivisionComponent.maxSubdivisionLevel = subdivisionLevel;
                subdivisionComponent.subdivisionRange = newRadius;
            }
        }
        public void Cleanup()
        {
            if (isMaxLevel)
            {
                EventHandler.OnQuadRangeCheck -= RangeCheck;
                UnityEngine.Object.Destroy(newQuad);
                UnityEngine.Object.Destroy(mesh);
                alreadyInitialized = false;
            }
            if (subdivisionComponent != null)
            {
                subdivisionComponent.Cleanup();
                subdivisionComponent = null;
            }
            if (probeEventAdded)
            {
                probeEventAdded = false;
                //RuntimeOperations.onFlightReflectionProbeReady -= SetReflectionProbeAnchor;
            }
        }
    }
}
