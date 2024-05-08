using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// Holds all shared mesh information about the quad and the scatters that are on it.
    /// </summary>
    public class ScatterSystemQuadData
    {
        ParallaxScatterBody body;

        // The terrain quad
        public PQ quad;
        public int subdivisionLevel;
        public float subdivisionRadius;
        public float sqrQuadWidth;

        // Direction from planet to quad in world and local space
        public Vector3 planetNormal;
        public Vector3 localPlanetNormal;
        public Vector3 planetOrigin;
        public float planetRadius;

        // PQS data
        // Potentially store a scaled version of this to get closer to the desired frequency and reduce precision errors
        // Length parity with quad vertex count
        public Vector3[] directionsFromCenter;

        // Physical mesh data
        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;
        int[] triangles;
        Vector2[] uvs;

        string[] cornerBiomes = new string[4];

        public int numMeshTriangles = 0;

        // Distribution buffers - Stores quad mesh information
        public ComputeBuffer sourceVertsBuffer;
        public ComputeBuffer sourceNormalsBuffer;
        public ComputeBuffer sourceTrianglesBuffer;
        public ComputeBuffer sourceUVsBuffer;
        public ComputeBuffer sourceDirsFromCenterBuffer;

        // Stores the scatter components
        public List<ScatterData> quadScatters = new List<ScatterData>();

        public ScatterSystemQuadData(ParallaxScatterBody body, PQ quad, int subdivisionLevel, float subdivisionRadius)
        {
            this.body = body;
            this.quad = quad;
            this.subdivisionLevel = subdivisionLevel;
            this.subdivisionRadius = subdivisionRadius;
        }
        public void Initialize()
        {
            sqrQuadWidth = (float)((2f * Mathf.PI * quad.sphereRoot.radius / 4f) / (Mathf.Pow(2f, quad.sphereRoot.maxLevel)));
            sqrQuadWidth *= sqrQuadWidth;

            mesh = UnityEngine.Object.Instantiate(quad.mesh);
            vertices = mesh.vertices;
            normals = mesh.normals;
            triangles = mesh.triangles;

            // Quad has UVs but they're not the right ones - we want planet UVs so we fetch them from here
            uvs = PQSMod_Parallax.quadPlanetUVs[quad];
            
            directionsFromCenter = GetDirectionsFromCenter(vertices, quad.sphereRoot.gameObject.transform.position);

            // Create compute buffers
            sourceVertsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceNormalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int), ComputeBufferType.Structured);
            sourceUVsBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 2, ComputeBufferType.Structured);
            sourceDirsFromCenterBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);

            sourceVertsBuffer.SetData(vertices);
            sourceNormalsBuffer.SetData(normals);
            sourceTrianglesBuffer.SetData(triangles);
            sourceUVsBuffer.SetData(uvs);
            sourceDirsFromCenterBuffer.SetData(directionsFromCenter);

            numMeshTriangles = triangles.Length / 3;

            planetNormal = Vector3.Normalize(quad.transform.position - quad.quadRoot.transform.position);
            localPlanetNormal = quad.gameObject.transform.InverseTransformDirection(planetNormal);

            CelestialBody body = FlightGlobals.GetBodyByName(quad.sphereRoot.name);
            planetOrigin = body.transform.position;
            planetRadius = (float)body.Radius;

            GetCornerBiomes(body);
            DetermineScatters();
        }
        /// <summary>
        /// Determine what scatters appear on this quad with some optimization to skip scatters that aren't eligible.
        /// </summary>
        public void DetermineScatters()
        {
            for (int i = 0; i < body.fastScatters.Length; i++)
            {
                Scatter scatter = body.fastScatters[i];
                if (ScatterEligible(scatter))
                {
                    ScatterData data = new ScatterData(this, body.fastScatters[i]);
                    quadScatters.Add(data);
                    data.Start();
                }
            }
        }
        /// <summary>
        /// Is the scatter eligible to be added to process on this quad?
        /// </summary>
        /// <param name="scatter"></param>
        /// <returns></returns>
        public bool ScatterEligible(Scatter scatter)
        {
            // Max level quads are always eligible because they're in range
            
            float range = scatter.distributionParams.range;

            // The distance at which this quad will subdivide next
            double subdivisionThreshold = quad.sphereRoot.subdivisionThresholds[quad.subdivision] * quad.subdivideThresholdFactor;

            // Subdivision level is too low, and we're not a max level quad
            if (range < subdivisionThreshold && quad.subdivision < quad.sphereRoot.maxLevel)
            {
                return false;
            }

            // The scatter will never appear in the altitude range this quad occupies
            if (scatter.distributionParams.maxAltitude < quad.meshVertMin || scatter.distributionParams.minAltitude > quad.meshVertMax)
            {
                return false;
            }

            // Is the scatter in an eligible biome?
            if (scatter.biomeBlacklistParams.fastBlacklistedBiomes.Contains(cornerBiomes[0]))
            {
                return false;
            }
            if (scatter.biomeBlacklistParams.fastBlacklistedBiomes.Contains(cornerBiomes[1]))
            {
                return false;
            }
            if (scatter.biomeBlacklistParams.fastBlacklistedBiomes.Contains(cornerBiomes[2]))
            {
                return false;
            }
            if (scatter.biomeBlacklistParams.fastBlacklistedBiomes.Contains(cornerBiomes[3]))
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Get the direction from planet center for each vertex in the quad mesh. Used to get Vector3s for calculating noise values on the GPU.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="planetCenter"></param>
        /// <returns></returns>
        public void GetCornerBiomes(CelestialBody body)
        {
            // Pick 4 corners of the quad and get their biomes for determining scatter eligibility
            Vector3 corner1 = quad.gameObject.transform.TransformPoint(vertices[0]);
            Vector3 corner2 = quad.gameObject.transform.TransformPoint(vertices[14]);
            Vector3 corner3 = quad.gameObject.transform.TransformPoint(vertices[224]);
            Vector3 corner4 = quad.gameObject.transform.TransformPoint(vertices[210]);

            // Uses a dictionary, at least...
            CBAttributeMapSO.MapAttribute attribute1 = Kopernicus.Utility.GetBiome(body, corner1);
            CBAttributeMapSO.MapAttribute attribute2 = Kopernicus.Utility.GetBiome(body, corner2);
            CBAttributeMapSO.MapAttribute attribute3 = Kopernicus.Utility.GetBiome(body, corner3);
            CBAttributeMapSO.MapAttribute attribute4 = Kopernicus.Utility.GetBiome(body, corner4);

            cornerBiomes[0] = attribute1.name;
            cornerBiomes[1] = attribute2.name;
            cornerBiomes[2] = attribute3.name;
            cornerBiomes[3] = attribute4.name;
        }
        public Vector3[] GetDirectionsFromCenter(Vector3[] vertices, Vector3 planetCenter)
        {
            Vector3 localPlanetCenter = quad.gameObject.transform.InverseTransformPoint(planetCenter);
            Vector3[] directions = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                directions[i] = Vector3.Normalize(vertices[i] - localPlanetCenter);
            }

            return directions;
        }
        /// <summary>
        /// Releases all memory consumed by this quad. Called when a quad is unloaded, or has a subdivision level below this.
        /// </summary>
        public void Cleanup()
        {
            foreach (ScatterData data in quadScatters)
            {
                data.Cleanup();
            }

            quadScatters.Clear();

            sourceVertsBuffer?.Dispose();
            sourceNormalsBuffer?.Dispose();
            sourceTrianglesBuffer?.Dispose();
            sourceUVsBuffer?.Dispose();
            sourceDirsFromCenterBuffer.Dispose();
        }
    }
}
