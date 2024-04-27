using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static KSP.UI.Screens.RDNode;

namespace Parallax
{
    // Holds most info on the quad itself, and assigns ScatterData when required
    // after meeting certain criteria, eg, is quad in range
    public class ScatterSystemQuadData
    {
        // The terrain quad
        public PQ quad;
        public int subdivisionLevel;
        public float subdivisionRadius;

        // Physical mesh data
        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;
        int[] triangles;

        public int numMeshTriangles = 0;

        // Distribution buffers - Stores quad mesh information
        public ComputeBuffer sourceVertsBuffer;
        public ComputeBuffer sourceNormalsBuffer;
        public ComputeBuffer sourceTrianglesBuffer;

        // Stores vertex direction from planet centre, as defined in ParallaxPQSMod.cs
        public ComputeBuffer quadNoiseData;

        // Stores the scatter components
        List<ScatterData> quadScatters = new List<ScatterData>();

        public ScatterSystemQuadData(PQ quad, int subdivisionLevel, float subdivisionRadius)
        {
            this.quad = quad;
            this.subdivisionLevel = subdivisionLevel;
            this.subdivisionRadius = subdivisionRadius;
        }
        public void Initialize()
        {
            mesh = UnityEngine.Object.Instantiate(quad.mesh);
            vertices = mesh.vertices;
            normals = mesh.normals;
            triangles = mesh.triangles;

            // Create compute buffers
            sourceVertsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceNormalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int), ComputeBufferType.Structured);

            sourceVertsBuffer.SetData(vertices);
            sourceNormalsBuffer.SetData(normals);
            sourceTrianglesBuffer.SetData(triangles);

            numMeshTriangles = triangles.Length / 3;

            DetermineScatters();
        }
        public void DetermineScatters()
        {
            if (quad.subdivision != quad.sphereRoot.maxLevel)
            {
                return;
            }
            ScatterData newScatter = new ScatterData(this);
            newScatter.Start();

            quadScatters.Add(newScatter);
        }
        // Called when a quad is unloaded, or has a subdivision level below this
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
            quadNoiseData?.Dispose();
        }
    }
}
