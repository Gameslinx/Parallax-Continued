using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using static KSP.UI.Screens.RDNode;

namespace Parallax
{
    /// <summary>
    /// Holds all shared mesh information about the quad and the scatters that are on it.
    /// </summary>
    public class ScatterSystemQuadData
    {
        /// <summary>
        /// The index of this class in the manager list
        /// </summary>
        public int ID { get; set; }

        ParallaxScatterBody body;

        // The terrain quad
        public PQ quad;
        public int subdivisionLevel;
        public float subdivisionRadius;
        public float sqrQuadWidth;
        public float cameraDistance;

        // Direction from planet to quad in world and local space
        public Vector3 planetNormal;
        public Vector3 runtimePlanetNormal;
        public Vector3 localPlanetNormal;
        public Vector3 planetOrigin;
        public float planetRadius;

        // PQS data
        // Potentially store a scaled version of this to get closer to the desired frequency and reduce precision errors
        // Length parity with quad vertex count
        public Vector3[] directionsFromCenter;

        // Physical mesh data
        Mesh mesh;
        public Vector3[] vertices;
        public Vector3[] normals;
        public int[] triangles;
        public Color[] colors;

        // UV in xy, allowScatter in z
        Vector3[] uvs;

        // Density multiplier based on the quad's position on the sphere
        public float sphereRelativeDensityMult = 1.0f;

        // Corners in 0 to 3, center in 4
        string[] cornerBiomes = new string[5];

        public int numMeshTriangles = 0;

        // Distribution buffers - Stores quad mesh information
        public ComputeBuffer sourceVertsBuffer;
        public ComputeBuffer sourceNormalsBuffer;
        public ComputeBuffer sourceTrianglesBuffer;
        public ComputeBuffer sourceColorsBuffer;
        public ComputeBuffer sourceUVsBuffer;
        public ComputeBuffer sourceDirsFromCenterBuffer;

        // Stores the scatter components
        public List<ScatterData> quadScatters = new List<ScatterData>();

        public ScatterSystemQuadData(ParallaxScatterBody body, PQ quad)
        {
            this.body = body;
            this.quad = quad;
        }
        /// <summary>
        /// Perform a first time initialization of this quad. Gets all prerequisite data and generates all scatters.
        /// </summary>
        public void Initialize()
        {
            mesh = quad.mesh;
            vertices = mesh.vertices;
            normals = mesh.normals;
            triangles = mesh.triangles;
            colors = mesh.colors;

            // We can estimate this using (float)((2f * Mathf.PI * quad.sphereRoot.radius / 4f) / (Mathf.Pow(2f, quad.subdivision)));
            // But quads do vary in size because of the cube sphere transformation, making this unreliable
            sqrQuadWidth = GetSqrQuadWidth(vertices);

            // Quad has UVs but they're not the right ones - we want planet UVs so we fetch them from here
            uvs = PQSMod_Parallax.quadPlanetUVs[quad];
            
            directionsFromCenter = GetDirectionsFromCenter(vertices, quad.sphereRoot.gameObject.transform.position);

            // Create compute buffers
            sourceVertsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceNormalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int), ComputeBufferType.Structured);
            sourceColorsBuffer = new ComputeBuffer(colors.Length, sizeof(float) * 4, ComputeBufferType.Structured);
            sourceUVsBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceDirsFromCenterBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);

            sourceVertsBuffer.SetData(vertices);
            sourceNormalsBuffer.SetData(normals);
            sourceTrianglesBuffer.SetData(triangles);
            sourceColorsBuffer.SetData(colors);
            sourceUVsBuffer.SetData(uvs);
            sourceDirsFromCenterBuffer.SetData(directionsFromCenter);

            numMeshTriangles = triangles.Length / 3;

            planetNormal = Vector3.Normalize(quad.transform.position - quad.sphereRoot.transform.position);
            localPlanetNormal = quad.gameObject.transform.InverseTransformDirection(planetNormal);

            CelestialBody body = FlightGlobals.GetBodyByName(quad.sphereRoot.name);
            planetOrigin = body.transform.position;
            planetRadius = (float)body.Radius;

            sphereRelativeDensityMult = GetSphereRelativeDensityMult(body);

            GetCornerBiomes(body);

            // Get the camera distance now, or we wait an additional frame
            UpdateQuadCameraDistance(ref RuntimeOperations.vectorCameraPos);
            DetermineScatters();
        }
        /// <summary>
        /// Reinitialize the prerequisite data on this quad that must be refreshed. It does NOT reinitialize all scatters. Use this if regenerating scatters.
        /// </summary>
        public void Reinitialize()
        {
            planetNormal = Vector3.Normalize(quad.transform.position - quad.sphereRoot.transform.position);
            localPlanetNormal = quad.gameObject.transform.InverseTransformDirection(planetNormal);

            CelestialBody body = FlightGlobals.GetBodyByName(quad.sphereRoot.name);
            planetOrigin = body.transform.position;

            directionsFromCenter = GetDirectionsFromCenter(vertices, quad.sphereRoot.gameObject.transform.position);
            sourceDirsFromCenterBuffer.SetData(directionsFromCenter);

            sphereRelativeDensityMult = GetSphereRelativeDensityMult(body);
        }
        /// <summary>
        /// Reinitializes an amount of scatters. Refreshes prerequisite data and regenerates the scatters specified.
        /// Performs a linear search on all scatters on this quad, so use this sparingly. Parallax only uses this for GUI refreshes.
        /// </summary>
        /// <param name="scatters"></param>
        public void ReinitializeScatters(params Scatter[] scatters)
        {
            // Fetch updated quad data
            Reinitialize();
            foreach (Scatter scatter in scatters)
            {
                // First check if this scatter is on this quad
                ScatterData data = quadScatters.Where((x) => x.scatter.scatterName == scatter.scatterName).FirstOrDefault();
                if (data != null)
                {
                    data.Cleanup();
                    data.Start();
                }
            }
        }
        /// <summary>
        /// Calculate the max bound on this quad
        /// </summary>
        public float GetSqrQuadWidth(Vector3[] verts)
        {
            // Quad corners are indices 0, 14, 210, 224
            // Find longest diag, divide by sqrt 2
            float width1 = (quad.transform.TransformPoint(verts[0]) - quad.transform.TransformPoint(verts[verts.Length - 1])).sqrMagnitude;
            float width2 = (quad.transform.TransformPoint(verts[210]) - quad.transform.TransformPoint(verts[14])).sqrMagnitude;

            return Mathf.Max(width1, width2) * 0.5f;
        }
        // Get the square distance from the quad to the camera
        public void UpdateQuadCameraDistance(ref Vector3 cameraPos)
        {
            cameraDistance = ((Vector3)quad.meshRenderer.localToWorldMatrix.GetColumn(3) - cameraPos).sqrMagnitude;
        }
        bool alreadyReinitializedThisFrame = false;
        /// <summary>
        /// Generate the instanced transform data on this quad
        /// </summary>
        public void EvaluateQuad()
        {
            foreach (ScatterData scatter in quadScatters)
            {
                // Scatter out of range and active? Pause
                if (cameraDistance > scatter.scatter.distributionParams.range * scatter.scatter.distributionParams.range + sqrQuadWidth)
                { 
                    if (!scatter.cleaned)
                    {
                        scatter.Pause();
                    }
                }
                else
                {
                    // Scatter in range and needs initializing? Reinit quad data and start
                    if (scatter.cleaned)
                    {
                        if (!alreadyReinitializedThisFrame)
                        {
                            Reinitialize();
                            alreadyReinitializedThisFrame = true;
                        }
                        scatter.Resume();
                    }
                }
                scatter.Evaluate();
            }
            alreadyReinitializedThisFrame = false;
        }
        /// <summary>
        /// Determines what scatters appear on this quad
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
                    StartScatter(data);
                }
            }
        }
        /// <summary>
        /// Starts the scatter, or initializes it in a paused state
        /// </summary>
        /// <param name="data"></param>
        public void StartScatter(ScatterData data)
        {
            // Update camera distance to this quad
            UpdateQuadCameraDistance(ref RuntimeOperations.vectorCameraPos);

            if (data.CollidersEligible() && cameraDistance > data.scatter.distributionParams.range * data.scatter.distributionParams.range + sqrQuadWidth)
            {
                // We're out of range, and colliders won't be generated
                data.Pause();
            }
            else
            {
                // We're in range, or colliders will be generated
                data.Start();
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

            int numBiomesIneligible = 0;
            foreach (string biome in cornerBiomes)
            {
                if (scatter.biomeBlacklistParams.fastBlacklistedBiomes.Contains(biome))
                {
                    numBiomesIneligible++;
                }
            }

            // The biome doesn't appear on this quad
            if (numBiomesIneligible == 5)
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
            Vector3 center = quad.gameObject.transform.TransformPoint(vertices[112]);

            // Uses a dictionary, at least...
            CBAttributeMapSO.MapAttribute attribute1 = Kopernicus.Utility.GetBiome(body, corner1);
            CBAttributeMapSO.MapAttribute attribute2 = Kopernicus.Utility.GetBiome(body, corner2);
            CBAttributeMapSO.MapAttribute attribute3 = Kopernicus.Utility.GetBiome(body, corner3);
            CBAttributeMapSO.MapAttribute attribute4 = Kopernicus.Utility.GetBiome(body, corner4);
            CBAttributeMapSO.MapAttribute attribute5 = Kopernicus.Utility.GetBiome(body, center);

            cornerBiomes[0] = attribute1.name;
            cornerBiomes[1] = attribute2.name;
            cornerBiomes[2] = attribute3.name;
            cornerBiomes[3] = attribute4.name;
            cornerBiomes[4] = attribute5.name;
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
        /// PQS is a subdivided cube-sphere. At the corners, the vertices are much closer together. This function accounts for that by approximating an appropriate reduction in density
        /// </summary>
        /// <param name="directionFromCenter"></param>
        /// <returns></returns>
        public float GetSphereRelativeDensityMult(CelestialBody body)
        {
            // This calculation is wrong, but it surprisingly works much better for KSP
            UnityEngine.Vector2d latlon = LatLon.GetLatitudeAndLongitude(body.BodyFrame, body.gameObject.transform.position, quad.gameObject.transform.position);

            float normalisedDensityMultiplier = 1.0f - BiomeLoader.GetDensityAt(latlon);

            // Square it, as we're working with area
            normalisedDensityMultiplier = normalisedDensityMultiplier * normalisedDensityMultiplier;

            float multiplier = Mathf.Clamp01(Mathf.Lerp(0.18f, 1.0f, normalisedDensityMultiplier));

            return multiplier;
        }
        /// <summary>
        /// Returns the non-smoothed terrain normal at a given index in the normals buffer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector3 GetTerrainNormal(uint index)
        {
            int index1 = triangles[index];
            int index2 = triangles[index + 1];
            int index3 = triangles[index + 2];

            Vector3 vert1 = vertices[index1];
            Vector3 vert2 = vertices[index2];
            Vector3 vert3 = vertices[index3];

            Vector3 localNormal = Vector3.Normalize(Vector3.Cross(vert2 - vert1, vert3 - vert1));
            return quad.meshRenderer.localToWorldMatrix.MultiplyVector(localNormal);
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
            sourceColorsBuffer?.Dispose();
            sourceUVsBuffer?.Dispose();
            sourceDirsFromCenterBuffer?.Dispose();
        }
    }
}
