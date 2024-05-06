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
            
            Stopwatch sw = Stopwatch.StartNew();
            directionsFromCenter = GetDirectionsFromCenter(vertices, quad.sphereRoot.gameObject.transform.position);
            sw.Stop();
            UnityEngine.Debug.Log("Elapsed time (dir from center: " + sw.ElapsedMilliseconds + " ms");

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
            return true;
        }
        /// <summary>
        /// Get the direction from planet center for each vertex in the quad mesh. Used to get Vector3s for calculating noise values on the GPU.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="planetCenter"></param>
        /// <returns></returns>
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
