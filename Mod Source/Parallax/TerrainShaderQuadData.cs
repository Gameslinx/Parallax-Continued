using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    //
    // Terrain Shader Quad Data
    // One per quad, handles parallax terrain shader subdivision and material swapping
    //
    public class TerrainShaderQuadData
    {
        public AdvancedSubdivision advancedSubdivision;
        public Material quadMaterial;
        public ParallaxBody body;

        GameObject newQuad;

        MeshRenderer quadMeshRenderer;
        MeshFilter quadMeshFilter;
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
            body = ConfigLoader.parallaxBodies[quad.sphereRoot.name];

            blendLowMidStart = body.terrainShaderProperties.shaderFloats["_LowMidBlendStart"];
            blendLowMidEnd = body.terrainShaderProperties.shaderFloats["_LowMidBlendEnd"];
            blendMidHighStart = body.terrainShaderProperties.shaderFloats["_MidHighBlendStart"];
            blendMidHighEnd = body.terrainShaderProperties.shaderFloats["_MidHighBlendEnd"];

            quadMaterial = ConfigLoader.wireframeMaterial; //DetermineMaterial();
            quadMeshRenderer = quad.gameObject.GetComponent<MeshRenderer>();

            if (isMaxLevel)
            {
                mesh = UnityEngine.Object.Instantiate(quad.mesh);
                
                quadWidth = (float)((2f * Mathf.PI * FlightGlobals.GetBodyByName(quad.sphereRoot.name).Radius / 4f) / (Mathf.Pow(2f, quad.sphereRoot.maxLevel)));
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
                SetReflectionProbeAnchor(RuntimeOperations.flightProbeObject.transform);
            }
            else
            {
                RuntimeOperations.onFlightReflectionProbeReady += SetReflectionProbeAnchor;
            }
        }
        // Params to avoid garbage gen
        Vector3 worldOrigin = Vector3.zero;
        Vector3 localOrigin = Vector3.zero;
        float dist = 0;
        public void RangeCheck()
        {
            worldOrigin = Camera.main == null ? Vector3.zero : Camera.main.transform.position; //FlightGlobals.ActiveVessel == null ? Vector3.zero : FlightGlobals.ActiveVessel.transform.position;
            localOrigin = quad.transform.InverseTransformPoint(worldOrigin);
            dist = (worldOrigin - quad.gameObject.transform.position).sqrMagnitude;
            // We're within range
            if (dist < quadWidth)
            {
                if (!alreadyInitialized)
                {
                    CreateFakeQuad();
                    advancedSubdivision = new AdvancedSubdivision(quad, ref newQuad, ref mesh, quadWidth, ref quadMaterial, subdivisionLevel);
                    materialCreated = false;
                }
                advancedSubdivision.RangeCheck(ref localOrigin, false);
            }
            // Out of range, and need to clean up the fake quad
            if (dist > quadWidth && alreadyInitialized)
            {
                advancedSubdivision.Cleanup();
                advancedSubdivision = null;
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
            double quadLowAltitude = quad.meshVertMin;
            double quadHighAltitude = quad.meshVertMax;

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

            // This quad uses all three textures
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
        public void Cleanup()
        {
            if (isMaxLevel)
            {
                EventHandler.OnQuadRangeCheck -= RangeCheck;
                UnityEngine.Object.Destroy(newQuad);
                UnityEngine.Object.Destroy(mesh);
                alreadyInitialized = false;
            }
            if (advancedSubdivision != null)
            {
                advancedSubdivision.Cleanup();
                advancedSubdivision = null;
            }
        }
    }
}
