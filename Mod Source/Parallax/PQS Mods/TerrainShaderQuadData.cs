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

        JobifiedSubdivision subdivision;

        MeshRenderer quadMeshRenderer;
        Mesh subdividedMesh;

        public PQ quad;
        public int subdivisionLevel;
        public float subdivisionRadius;
        public bool isMaxLevel;
        public float quadWidth;

        float blendLowMidStart;
        float blendLowMidEnd;
        float blendMidHighStart;
        float blendMidHighEnd;

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
            subdivisionLevel = BiomeLoader.GetSphereRelativeSubdivisionLevel(FlightGlobals.GetBodyByName(quad.sphereRoot.name), quad, subdivisionLevel);

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
            ApplyMaterial();

            if (isMaxLevel)
            {
                quadWidth = (float)((2f * Mathf.PI * quad.sphereRoot.radius / 4f) / (Mathf.Pow(2f, quad.sphereRoot.maxLevel)));
                quadWidth *= quadWidth;

                EventHandler.OnQuadRangeCheck += RangeCheck;
                RangeCheck();
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
            if (subdivision != null)
            {
                subdivision.RefreshSourceData();
            }
        }
        public float GetSqrQuadCameraDistance(in Vector3 quadPosition, in Vector3 cameraPosition)
        {
            return (quadPosition - cameraPosition).sqrMagnitude;
        }
        public void RangeCheck()
        {
            dist = GetSqrQuadCameraDistance(quad.PrecisePosition, RuntimeOperations.vectorCameraPos);
            // We're within range, but the normals will update again so wait
            if (dist < quadWidth && subdivision == null && !quad.isQueuedForNormalUpdate)
            {
                StartSubdivision();
            }
            // Out of range, and need to clean up the subdivision
            if (dist > quadWidth && subdivision != null)
            {
                // Consider preventing jobs from being started when the quad is GOING out of range, so they complete before then
                StopSubdivision();
            }
        }
        // PQS keeps writing to quad.mesh, which the collider and scatters also use, so the subdivided mesh is a separate mesh that only the quad's mesh filter renders
        void StartSubdivision()
        {
            if (subdividedMesh == null)
            {
                subdividedMesh = new Mesh();
            }

            subdivision = new JobifiedSubdivision(quad, subdividedMesh, subdivisionLevel, subdivisionRadius);
            quad.meshFilter.sharedMesh = subdividedMesh;
        }
        void StopSubdivision()
        {
            subdivision.Cleanup();
            subdivision = null;
            quad.meshFilter.sharedMesh = quad.mesh;
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
        public void ApplyMaterial()
        {
            quadMeshRenderer.sharedMaterial = quadMaterial;
            quadMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
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
            if (subdivision != null)
            {
                subdivision.maxSubdivisionLevel = subdivisionLevel;
                subdivision.subdivisionRange = newRadius;
            }
        }
        public void Cleanup()
        {
            if (isMaxLevel)
            {
                EventHandler.OnQuadRangeCheck -= RangeCheck;
                if (subdivision != null)
                {
                    StopSubdivision();
                }
                if (subdividedMesh != null)
                {
                    UnityEngine.Object.Destroy(subdividedMesh);
                    subdividedMesh = null;
                }
            }
            if (probeEventAdded)
            {
                probeEventAdded = false;
                //RuntimeOperations.onFlightReflectionProbeReady -= SetReflectionProbeAnchor;
            }
        }
    }
}
