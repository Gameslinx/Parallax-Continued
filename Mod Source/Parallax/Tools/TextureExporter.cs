using FinePrint.Utilities;
using Kopernicus.Components.ModularScatter;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using static PQS;

namespace Parallax.Tools
{
    public class TextureExporter : MonoBehaviour
    {
        public static bool exportRequested = true;
        public static int planetExportProgress = 0;
        public struct TextureExporterOptions
        {
            public int horizontalResolution;

            public bool exportColor;
            public bool exportNormal;
            public bool exportHeight;

            public bool multithread;
        }
        public static IEnumerator GenerateTextures(TextureExporterOptions options, params CelestialBody[] bodies)
        {
            ResetExportProgress();
            int nextProgress = 1;
            foreach (CelestialBody body in bodies)
            {
                ParallaxDebug.Log("Exporter: Exporting " + body.name);
                PQS pqs = body.pqsController;
                if (pqs == null)
                {
                    ParallaxDebug.Log("Skipping " + body.name + " as it has no surface");
                    AdvanceExportProgress();
                    nextProgress = planetExportProgress + 1;
                    continue;
                }

                exportRequested = true;
                BuildPlanetMaps(pqs, body, options);

                // Wait to fire the next export off until the previous one is complete (async method means we'll run past it)
                yield return new WaitUntil(() => (planetExportProgress == nextProgress));

                nextProgress++;
                exportRequested = false;
            }

            ResetExportProgress();
        }
        private static void AdvanceExportProgress()
        {
            planetExportProgress++;
        }
        private static void ResetExportProgress()
        {
            planetExportProgress = 0;
        }
        public static async void BuildPlanetMaps(PQS pqs, CelestialBody body, TextureExporterOptions options)
        {
            int resX = options.horizontalResolution;
            int resY = options.horizontalResolution / 2;

            double[,] heightValues = new double[resX, resY];
            Color[,] colorValues = new Color[resX, resY];
            Vector3d[,] directions = new Vector3d[resX, resY];

            ScreenMessage message = ScreenMessages.PostScreenMessage("Generating terrain data", Single.MaxValue, ScreenMessageStyle.UPPER_CENTER);

            Vector3d center = Vector3d.zero;
            float radius = (float)pqs.radius;

            // Keep the main thread running
            int numTasks = Mathf.Max(SystemInfo.processorCount - 1, 1);
            int chunkSize = Mathf.CeilToInt((float)resX / numTasks);
            var tasks = new List<Task>();

            List<PQS> pqsTasklist = new List<PQS>();
            List<Delegate> onVertexBuildHeightList = new List<Delegate>();
            List<Delegate> onVertexBuildList = new List<Delegate>();

            float[] threadProgress = new float[numTasks];

            //PQSMod_VertexColorMap originalColorMap = (PQSMod_VertexColorMap)pqs.mods.FirstOrDefault((x) => x.GetType() == typeof(PQSMod_VertexColorMap));

            // Per thread data
            for (int i = 0; i < numTasks; i++)
            {
                PQS thisPQS = Instantiate(pqs);
                CloneAllMapSO(pqs, thisPQS);
                thisPQS.enabled = false;

                // Remove this, we don't need it and it throws exceptions
                ModularScatter[] kopScatterComponent = thisPQS.GetComponentsInChildren<ModularScatter>();
                for (int j = 0; j < kopScatterComponent.Length; j++) { Destroy(kopScatterComponent[j]); }

                thisPQS.SetupExternalRender();
                pqsTasklist.Add(thisPQS);
                onVertexBuildHeightList.Add((Action<PQS.VertexBuildData, Boolean>)Delegate.CreateDelegate(typeof(Action<PQS.VertexBuildData, Boolean>), thisPQS, typeof(PQS).GetMethod("Mod_OnVertexBuildHeight", BindingFlags.NonPublic | BindingFlags.Instance)));
                onVertexBuildList.Add((Action<PQS.VertexBuildData>)Delegate.CreateDelegate(typeof(Action<PQS.VertexBuildData>), thisPQS, typeof(PQS).GetMethod("Mod_OnVertexBuild", BindingFlags.NonPublic | BindingFlags.Instance)));
            }
            //return;
            ThreadSafeProgressReporter reporter = ThreadSafeProgressReporter.Instance;

            double[] minHeightPerBlock = new double[numTasks];
            double[] maxHeightPerBlock = new double[numTasks];

            // Dispatch the PQSMods split into tasks
            for (int i = 0; i < numTasks; i++)
            {
                int startX = i * chunkSize;
                int endX = Mathf.Min(startX + chunkSize, resX); // Ensure the last chunk does not go out of bounds
                int taskID = i;

                double minHeight = double.MaxValue;
                double maxHeight = -double.MaxValue;

                // Dispatch a task for each range of values per thread
                tasks.Add(Task.Run(() =>
                {
                    VertexBuildData vertexBuildData = new VertexBuildData();

                    Debug.Log("Tasks started");
                    Debug.Log("Thread: " + taskID + ", startX = " + startX + ", endX = " + endX + ", chunkSize = " + chunkSize);

                    for (int x = startX; x < endX; x++)
                    {
                        threadProgress[taskID] = (float)(x - startX) / (float)(endX - startX) * 100.0f;
                        for (int y = 0; y < resY; y++)
                        {
                            vertexBuildData.directionFromCenter = QuaternionD.AngleAxis(360d / resX * x, Vector3d.up) *
                                QuaternionD.AngleAxis(90d - 180d / (resX / 2f) * y, Vector3d.right) *
                                Vector3d.forward;

                            vertexBuildData.vertHeight = radius;
                            ((Action<PQS.VertexBuildData, Boolean>)onVertexBuildHeightList[taskID])(vertexBuildData, true);
                            ((Action<PQS.VertexBuildData>)onVertexBuildList[taskID])(vertexBuildData);

                            heightValues[x, y] = vertexBuildData.vertHeight;
                            directions[x, y] = vertexBuildData.directionFromCenter;
                            colorValues[x, y] = vertexBuildData.vertColor;

                            if (heightValues[x, y] < minHeight)
                            {
                                minHeight = heightValues[x, y];
                            }
                            if (heightValues[x, y] > maxHeight)
                            {
                                maxHeight = heightValues[x, y];
                            }
                        }
                        // Advance one frame every 32 * resY pixels (can be left out for async)
                        if (x % 32 == 0 && taskID == 0)
                        {
                            float lowestProgressSoFar = threadProgress.Min();
                            reporter.Report(lowestProgressSoFar);
                        }
                    }

                    minHeightPerBlock[taskID] = minHeight;
                    maxHeightPerBlock[taskID] = maxHeight;

                }));
            }
            await Task.WhenAll(tasks);

            // When threads have joined, calculate the min/max height for the entire map
            double minPQSHeight = minHeightPerBlock.Min();
            double maxPQSHeight = maxHeightPerBlock.Max();

            ParallaxGUI.SetMinMaxAltitudeLabels(body.name, (float)(minPQSHeight - body.Radius), (float)(maxPQSHeight - body.Radius));

            if (options.exportNormal)
            {
                Texture2D normalMap = new Texture2D(resX, resY, TextureFormat.RGB24, false);

                Color[] normalMapPixels = GenerateNormalMap(resX, resY, heightValues, directions, pqs, body.ocean);
                normalMap.SetPixels(normalMapPixels, 0);
                normalMap.Apply(false);

                SaveMap(normalMap, body.name, body.name + "_Normal");
                Destroy(normalMap);
            }
            if (options.exportHeight)
            {
                if (body.ocean)
                {
                    Texture2D flatHeightMap = new Texture2D(resX, resY, TextureFormat.RGB24, false);

                    Color[] flatHeightMapPixels = GenerateHeightMap(resX, resY, heightValues, pqs.radius, maxPQSHeight);
                    flatHeightMap.SetPixels(flatHeightMapPixels, 0);
                    flatHeightMap.Apply(false);
                    SaveMap(flatHeightMap, body.name, body.name + "_Height_Ocean");

                    Destroy(flatHeightMap);
                }
                
                Texture2D heightMap = new Texture2D(resX, resY, TextureFormat.RGB24, false);

                Color[] heightMapPixels = GenerateHeightMap(resX, resY, heightValues, minPQSHeight, maxPQSHeight);
                heightMap.SetPixels(heightMapPixels, 0);
                heightMap.Apply(false);
                SaveMap(heightMap, body.name, body.name + "_Height");

                Destroy(heightMap);
            }
            if (options.exportColor)
            {
                if (body.ocean)
                {
                    Texture2D colorMapOcean = new Texture2D(resX, resY, TextureFormat.RGB24, false);

                    Color[] colorMapOceanPixels = GenerateColorMapOcean(resX, resY, heightValues, colorValues, pqs);
                    colorMapOcean.SetPixels(colorMapOceanPixels, 0);
                    colorMapOcean.Apply(false);

                    SaveMap(colorMapOcean, body.name, body.name + "_Color_Ocean");
                    Destroy(colorMapOcean);
                }
                Texture2D colorMap = new Texture2D(resX, resY, TextureFormat.RGB24, false);

                Color[] colorMapPixels = GenerateColorMap(resX, resY, colorValues);
                colorMap.SetPixels(colorMapPixels, 0);
                colorMap.Apply(false);

                SaveMap(colorMap, body.name, body.name + "_Color");
                Destroy(colorMap);
            }

            ScreenMessages.PostScreenMessage("Maps built!", 3.0f);

            // Clean up
            for (int i = 0; i < numTasks; i++)
            {
                PQS thisPQS = pqsTasklist[i];
                DestroyAllMapSO(thisPQS);
                Destroy(thisPQS);
            }

            AdvanceExportProgress();
        }

        static void SaveMap(Texture2D texture, string planetName, string name)
        {
            string folderPath = KSPUtil.ApplicationRootPath + "GameData/ParallaxContinued_PLANET_EXPORTS/" + planetName + "/PluginData/";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = folderPath + name + ".png";
            byte[] data = texture.EncodeToPNG();

            File.WriteAllBytes(filePath, data);
        }
        static int WrapX(int resX, int x)
        {
            if (x < 0)
            {
                return resX + x;
            }
            if (x > resX - 1)
            {
                return x - resX;
            }
            return x;
        }
        static Color[] GenerateNormalMap(int resX, int resY, double[,] heightValues, Vector3d[,] directions, PQS pqs, bool ocean)
        {
            // Output normal map colors
            Color[] outputPixels = new Color[resX * resY];
            Color oceanColor = new Color(0.5f, 0.5f, 1.0f, 1.0f);

            // Multithreaded normal map generation
            Parallel.For(0, resX, x =>
            {
                for (int y = 0; y < resY; y++)
                {
                    double height = heightValues[x, y];
                    Vector3d direction = Vector3d.Normalize(directions[x, y]);
                    int adjX = WrapX(resX, x + 1);
                    int adjY = Mathf.Clamp(y + 1, 0, resY - 1);

                    // Real world surface of the vertex at this pixel
                    Vector3d worldPos = direction * height;
                    Vector3d worldPosFlat = direction * pqs.radius;

                    // First, construct TBN matrix
                    Vector3d adjXFlat = pqs.radius * directions[adjX, y];

                    // Use float vectors now, we've done all the precise stuff
                    Vector3 worldNormal = direction;
                    Vector3 worldTangent = Vector3d.Normalize(adjXFlat - worldPosFlat);
                    Vector3 worldBinormal = Vector3d.Normalize(Vector3d.Cross(direction, worldTangent));

                    Matrix4x4 TBN = new Matrix4x4(
                        new Vector4(worldTangent.x, worldTangent.y, worldTangent.z, 0),
                        new Vector4(worldBinormal.x, worldBinormal.y, worldBinormal.z, 0),
                        new Vector4(worldNormal.x, worldNormal.y, worldNormal.z, 0),
                        new Vector4(0, 0, 0, 1) // Identity row for homogeneous coordinates
                    );

                    // Inverse the matrix to get from world to tangent space
                    TBN = Matrix4x4.Inverse(TBN);

                    // Right vertex
                    Vector3d rightVertex = directions[adjX, y] * heightValues[adjX, y];
                    Vector3d dx = Vector3d.Normalize(rightVertex - worldPos);

                    // Down vertex
                    Vector3d downVertex = directions[x, adjY] * heightValues[x, adjY];
                    Vector3d dy = Vector3d.Normalize(downVertex - worldPos);

                    Vector3d realWorldNormal = Vector3d.Normalize(Vector3d.Cross(dx, dy));
                    Vector3 tangentNormal = Vector3.Normalize(TBN.MultiplyVector(realWorldNormal));

                    Color col = new Color(tangentNormal.x, tangentNormal.y, tangentNormal.z);
                    col.r = col.r * 0.5f + 0.5f;
                    col.g = col.g * 0.5f + 0.5f;
                    outputPixels[y * resX + x] = col;

                    if (ocean && height < pqs.radius)
                    {
                        outputPixels[y * resX + x] = oceanColor;
                    }
                }
            });

            return outputPixels;
        }
        static Color[] GenerateHeightMap(int resX, int resY, double[,] heightValues, double minPQSHeight, double maxPQSHeight)
        {
            // Output height map colors
            Color[] outputPixels = new Color[resX * resY];
            Color col = new Color(1, 1, 1, 1);

            // Normalise the values
            Parallel.For(0, resX, x =>
            {
                for (int y = 0; y < resY; y++)
                {
                    // Normalise (get percentage this value is between min and max)
                    float heightValue = (float)((heightValues[x, y] - minPQSHeight) / (maxPQSHeight - minPQSHeight));
                    outputPixels[y * resX + x] = new Color(heightValue, heightValue, heightValue, 1); //col * heightValue;
                }
            });

            return outputPixels;
        }
        static Color[] GenerateColorMapOcean(int resX, int resY, double[,] heightValues, Color[,] colorValues, PQS pqs)
        {
            // Output map colors
            Color[] outputPixels = new Color[resX * resY];

            Parallel.For(0, resX, x =>
            {
                for (int y = 0; y < resY; y++)
                {
                    outputPixels[y * resX + x] = colorValues[x, y];

                    // In ocean
                    if (heightValues[x, y] < pqs.radius)
                    {
                        outputPixels[y * resX + x] = pqs.mapOceanColor;
                    }
                }
            });

            return outputPixels;
        }
        static Color[] GenerateColorMap(int resX, int resY, Color[,] colorValues)
        {
            // Output map colors
            Color[] outputPixels = new Color[resX * resY];

            Parallel.For(0, resX, x =>
            {
                for (int y = 0; y < resY; y++)
                {
                    outputPixels[y * resX + x] = colorValues[x, y];
                }
            });

            return outputPixels;
        }
        static void CloneAllMapSO(PQS original, PQS pqs)
        {
            pqs.mods = original.mods.Clone() as PQSMod[];
            for (int i = 0; i < pqs.mods.Length; i++)
            {
                Type modType = pqs.mods[i].GetType();
                PQSMod mod = pqs.mods[i];

                foreach (var field in modType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(MapSO))
                    {
                        var originalMapSO = (MapSO)field.GetValue(mod);
                        if (originalMapSO != null)
                        {
                            MapSO clonedMapSO = Instantiate(originalMapSO);
                            field.SetValue(mod, clonedMapSO);
                        }
                    }
                }

                // Process properties
                foreach (var property in modType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (property.PropertyType == typeof(MapSO) && property.CanRead && property.CanWrite)
                    {
                        var originalMapSO = (MapSO)property.GetValue(mod);
                        if (originalMapSO != null)
                        {
                            MapSO clonedMapSO = Instantiate(originalMapSO);
                            property.SetValue(mod, clonedMapSO);
                        }
                    }
                }
            }
            
        }
        static void DestroyAllMapSO(PQS pqs)
        {
            for (int i = 0; i < pqs.mods.Length; i++)
            {
                Type modType = pqs.mods[i].GetType();
                PQSMod mod = pqs.mods[i];

                foreach (var field in modType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(MapSO))
                    {
                        var originalMapSO = (MapSO)field.GetValue(mod);
                        if (originalMapSO != null)
                        {
                            Destroy(originalMapSO);
                        }
                    }
                }

                // Process properties
                foreach (var property in modType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (property.PropertyType == typeof(MapSO) && property.CanRead && property.CanWrite)
                    {
                        var originalMapSO = (MapSO)property.GetValue(mod);
                        if (originalMapSO != null)
                        {
                            Destroy(originalMapSO);
                        }
                    }
                }
            }

        }

    }
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class ThreadSafeProgressReporter : MonoBehaviour
    {
        private ScreenMessage screenMessage;
        private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        public static ThreadSafeProgressReporter Instance;

        void Awake()
        {
            Instance = this;
        }
        public void Report(float percentage)
        {
            messageQueue.Enqueue("Generating map data: Approx " + percentage + "% complete");
        }

        private void Update()
        {
            while (messageQueue.TryDequeue(out var message))
            {
                screenMessage = ScreenMessages.PostScreenMessage(message, 3.0f);
            }
        }
    }
}
