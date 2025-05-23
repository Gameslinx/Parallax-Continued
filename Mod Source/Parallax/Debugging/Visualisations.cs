﻿using Parallax.Debugging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Visualisations : MonoBehaviour
    {
        bool showingNoise = false;
        bool showingDistance = false;
        bool showingBiomes = false;
        bool showingDensity = false;
        bool showingUVs = false;

        void Update_Ignore()
        {
            //return;

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                ScatterSystemQuadData closest = null;
                float closestDist = float.MaxValue;
                foreach (ScatterSystemQuadData quadData in ScatterComponent.scatterQuadData.Values)
                {
                    float distance = quadData.cameraDistance;
                    if (distance < closestDist)
                    {
                        closestDist = distance;
                        closest = quadData;
                    }
                }

                float quadWidth = Vector3.Distance(closest.quad.gameObject.transform.TransformPoint(closest.vertices[0]), closest.quad.gameObject.transform.TransformPoint(closest.vertices[14]));
                Debug.Log("Quad width of closest quad " + quadWidth);
            }

            bool noiseToggle = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha1);
            if (noiseToggle) 
            {
                showingNoise = !showingNoise;
                if (showingNoise) { ScatterNoiseDisplay.ShowNoise(ConfigLoader.parallaxScatterBodies[FlightGlobals.currentMainBody.name].scatters.FirstOrDefault().Value.scatterName); }
                if (!showingNoise) { ScatterNoiseDisplay.Cleanup(); }
            }
            bool distanceToggle = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha2);
            if (distanceToggle)
            {
                showingDistance = !showingDistance;
                if (showingDistance) { QuadDistanceDisplay.ShowQuadDistances(); }
                if (!showingDistance) { QuadDistanceDisplay.Cleanup(); }
            }
            bool biomeToggle = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha3);
            if (biomeToggle)
            {
                showingBiomes = !showingBiomes;
                if (showingBiomes) { QuadBiomeDisplay.ShowQuadBiomes(); }
                if (!showingBiomes) { QuadBiomeDisplay.Cleanup(); }
            }
            bool densityToggle = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha4);
            if (densityToggle)
            {
                showingDensity = !showingDensity;
                if (showingDensity)
                {
                    QuadDensityDisplay.ShowQuadDensities();
                }
                if (!showingDensity)
                {
                    QuadDensityDisplay.Cleanup();
                }
            }
            bool uvToggle = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha5);
            if (uvToggle)
            {
                showingUVs = !showingUVs;
                if (showingUVs)
                {
                    QuadUVDisplay.ShowQuadUVs();
                }
                if (!showingUVs)
                {
                    QuadUVDisplay.Cleanup();
                }
            }
        
            bool logVRAMStats = Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha6);
            if (logVRAMStats)
            {
                ParallaxDiagnostics.LogComputeShaderResourceUsage();
            }
        }
        
        public static GameObject CreateQuadGameObject(PQ quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter)
        {
            GameObject go = new GameObject(quad.name + "-VISUALISATION");
            go.transform.position = quad.gameObject.transform.position;
            go.transform.rotation = quad.gameObject.transform.rotation;
            go.transform.localScale = quad.gameObject.transform.localScale;

            Vector3 positionOffset = Vector3.Normalize(go.transform.position - FlightGlobals.currentMainBody.transform.position) * 0.5f;
            go.transform.position += positionOffset;

            meshFilter = go.AddComponent<MeshFilter>();
            meshRenderer = go.AddComponent<MeshRenderer>();
            return go;
        }
        // Show noise on each quad
        public class ScatterNoiseDisplay
        {
            public static List<GameObject> objectDisplays = new List<GameObject>();
            public static void ShowNoise(string scatterName)
            {
                Scatter scatter = ConfigLoader.parallaxScatterBodies[FlightGlobals.currentMainBody.name].scatters[scatterName];
                foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
                {
                    // Create debug visualisation quad
                    PQ quad = quadData.Key;

                    GameObject go = CreateQuadGameObject(quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter);

                    meshRenderer.sharedMaterial = new Material(AssetBundleLoader.parallaxDebugShaders["Custom/ShowNoise"]);
                    meshRenderer.sharedMaterial.SetFloat("_Frequency", scatter.noiseParams.frequency);
                    meshRenderer.sharedMaterial.SetInt("_Octaves", scatter.noiseParams.octaves);
                    meshRenderer.sharedMaterial.SetFloat("_Lacunarity", scatter.noiseParams.lacunarity);
                    meshRenderer.sharedMaterial.SetInt("_NoiseMode", (int)scatter.noiseParams.noiseType);
                    meshRenderer.sharedMaterial.SetInt("_Inverse", scatter.noiseParams.inverted ? 1 : 0);

                    // Set gameObject mesh data
                    Mesh mesh = UnityEngine.Object.Instantiate(quad.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    Vector3[] vertices = mesh.vertices;
                    Color[] directions = new Color[vertices.Length];

                    // Set vertex colours as directions from planet center
                    Vector3 localPlanetCenter = quad.gameObject.transform.InverseTransformPoint(FlightGlobals.currentMainBody.transform.position);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 dir = Vector3.Normalize(vertices[i] - localPlanetCenter);
                        directions[i] = new Color(dir.x, dir.y, dir.z, 1);
                    }
                    mesh.colors = directions;

                    meshFilter.mesh = mesh;
                    go.SetActive(true);
                    objectDisplays.Add(go);
                }
            }
            public static void Cleanup()
            {
                foreach (GameObject go in objectDisplays)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }
        public class QuadDistanceDisplay
        {
            public static List<GameObject> objectDisplays = new List<GameObject>();

            public static void ShowQuadDistances()
            {
                foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
                {
                    PQ quad = quadData.Key;
                    GameObject go = CreateQuadGameObject(quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter);

                    Mesh mesh = UnityEngine.Object.Instantiate(quad.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    meshFilter.mesh = mesh;
                    meshRenderer.sharedMaterial = Instantiate(ConfigLoader.wireframeMaterial);
                    QuadRangeComponent qrc = go.AddComponent<QuadRangeComponent>();
                    qrc.quad = quad;

                    objectDisplays.Add(go);
                }
            }

            public static void Cleanup()
            {
                foreach (GameObject go in objectDisplays)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }

        public class QuadBiomeDisplay
        {
            public static List<GameObject> objectDisplays = new List<GameObject>();

            public static void ShowQuadBiomes()
            {
                foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
                {
                    PQ quad = quadData.Key;
                    GameObject go = CreateQuadGameObject(quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter);

                    Mesh mesh = UnityEngine.Object.Instantiate(quad.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    meshFilter.mesh = mesh;
                    meshRenderer.sharedMaterial = new Material(AssetBundleLoader.parallaxDebugShaders["Custom/ShowBiomeMap"]);
                    QuadBiomeComponent qrc = go.AddComponent<QuadBiomeComponent>();
                    qrc.quad = quad;

                    objectDisplays.Add(go);
                }
            }

            public static void Cleanup()
            {
                foreach (GameObject go in objectDisplays)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }

        public class QuadDensityDisplay
        {
            public static List<GameObject> objectDisplays = new List<GameObject>();
            public static void ShowQuadDensities()
            {
                foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
                {
                    PQ quad = quadData.Key;
                    GameObject go = CreateQuadGameObject(quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter);

                    Mesh mesh = UnityEngine.Object.Instantiate(quad.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    meshFilter.mesh = mesh;
                    meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));

                    float density = BiomeLoader.GetSphereRelativeDensityMult(FlightGlobals.currentMainBody, quad);
                    meshRenderer.sharedMaterial.SetColor("_Color", Color.white * density);

                    objectDisplays.Add(go);
                }
            }
            public static void Cleanup()
            {
                foreach (GameObject go in objectDisplays)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }

        public class QuadUVDisplay
        {
            public static List<GameObject> objectDisplays = new List<GameObject>();

            public static void ShowQuadUVs()
            {
                foreach (KeyValuePair<PQ, ScatterSystemQuadData> quadData in ScatterComponent.scatterQuadData)
                {
                    PQ quad = quadData.Key;
                    GameObject go = CreateQuadGameObject(quad, out MeshRenderer meshRenderer, out MeshFilter meshFilter);

                    Mesh mesh = UnityEngine.Object.Instantiate(quad.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    meshFilter.mesh = mesh;
                    Vector3[] uv = PQSMod_Parallax.quadPlanetUVs[quad];
                    Vector2[] realUV = new Vector2[uv.Length];
                    for (int i = 0; i < uv.Length; i++)
                    {
                        Vector3 vec = uv[i];
                        realUV[i] = new Vector2(vec.x, vec.y);
                    }
                    meshFilter.mesh.uv = realUV;
                    meshRenderer.sharedMaterial = new Material(AssetBundleLoader.parallaxDebugShaders["Custom/ShowUVs"]);

                    objectDisplays.Add(go);
                }
            }

            public static void Cleanup()
            {
                foreach (GameObject go in objectDisplays)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }
    }
}
