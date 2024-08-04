using Parallax.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        Vector3[] vertices;
        Vector3[] normals;
        int[] triangles;

        // UV in xy, allowScatter in z
        Vector3[] uvs;

        // Density multiplier based on the quad's position on the sphere
        public float sphereRelativeDensityMult = 1.0f;

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
            Profiler.BeginSample("Parallax Scatter System Initialize");

            mesh = quad.mesh;
            vertices = mesh.vertices;
            normals = mesh.normals;
            triangles = mesh.triangles;

            // We can estimate this using (float)((2f * Mathf.PI * quad.sphereRoot.radius / 4f) / (Mathf.Pow(2f, quad.subdivision)));
            // But quads do vary in size because of the cube sphere transformation, making this unreliable
            sqrQuadWidth = (quad.transform.TransformPoint(vertices[0]) - quad.transform.TransformPoint(vertices[vertices.Length - 1])).sqrMagnitude * 4; 

            // Quad has UVs but they're not the right ones - we want planet UVs so we fetch them from here
            uvs = PQSMod_Parallax.quadPlanetUVs[quad];
            
            directionsFromCenter = GetDirectionsFromCenter(vertices, quad.sphereRoot.gameObject.transform.position);

            // Create compute buffers
            sourceVertsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceNormalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int), ComputeBufferType.Structured);
            sourceUVsBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 3, ComputeBufferType.Structured);
            sourceDirsFromCenterBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);

            sourceVertsBuffer.SetData(vertices);
            sourceNormalsBuffer.SetData(normals);
            sourceTrianglesBuffer.SetData(triangles);
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
            Profiler.BeginSample("Determine scatters (and generate)");
            DetermineScatters();
            Profiler.EndSample();
            Profiler.EndSample();
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
        public void ReinitializeScatters(params ScatterData[] scatterData)
        {
            Reinitialize();
            foreach (ScatterData data in scatterData)
            {
                data.Start();
            }
        }
        // Get the square distance from the quad to the camera
        public void UpdateQuadCameraDistance(ref Vector3 cameraPos)
        {
            cameraDistance = ((Vector3)quad.meshRenderer.localToWorldMatrix.GetColumn(3) - cameraPos).sqrMagnitude;

            // Warning - bugged - Especially on GUI refreshes
            // Intention is to clean up quads that are out of range completely to save some VRAM and reduce compute shader usage...
            // Commented out for now 

            //foreach (ScatterData scatter in quadScatters)
            //{
            //    if (cameraDistance > scatter.scatter.distributionParams.range * scatter.scatter.distributionParams.range + sqrQuadWidth)
            //    { 
            //        scatter.Cleanup(); 
            //    }
            //    else
            //    {
            //        if (scatter.cleaned)
            //        {
            //            ReinitializeScatters(scatter);
            //        }
            //    }
            //}
        }
        /// <summary>
        /// Determines what scatters appear on this quad with some optimization to skip scatters that aren't eligible.
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
        /// PQS is a subdivided cube-sphere. At the corners, the vertices are much closer together. This function accounts for that by approximating an appropriate reduction in density
        /// </summary>
        /// <param name="directionFromCenter"></param>
        /// <returns></returns>
        public float GetSphereRelativeDensityMult(CelestialBody body)
        {
            // This calculation is wrong, but it surprisingly works much better for KSP
            UnityEngine.Vector2d latlon = LatLon.GetLatitudeAndLongitude(body.BodyFrame, body.transform.position, quad.PrecisePosition);

            //From -22.5 to 22.5 where 0 we want the highest density and -22.5 we want 1/3 density
            double lat = Math.Abs(latlon.x) % 45.0 - 22.5;
            double lon = Math.Abs(latlon.y) % 45.0 - 22.5;   

            //Now from -1 to 1, with 0 being where we want most density and -1 where we want 1/3
            lat /= 22.5;
            lon /= 22.5;    

            //Now from 0 to 1. 1 when at a corner
            lat = Math.Abs(lat);
            lon = Math.Abs(lon);    

            double factor = (lat + lon) / 2;
            float multiplier = Mathf.Clamp01(Mathf.Lerp(1.0f, 0.333333f, Mathf.Pow((float)factor, 2)));

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
            sourceUVsBuffer?.Dispose();
            sourceDirsFromCenterBuffer.Dispose();
        }
    }
}
