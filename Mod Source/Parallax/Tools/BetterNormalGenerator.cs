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
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class BetterNormalGenerator : MonoBehaviour
    {
        WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        void Update()
        {
            string planetName = PlanetariumCamera.fetch.target.gameObject.name;
            bool shouldGenerateNormal = Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.N);
            if (shouldGenerateNormal)
            {
                CelestialBody body = FlightGlobals.GetBodyByName(planetName);
                PQS pqs = body.pqsController;

                //Coroutine co = StartCoroutine(BuildNormalMap(pqs, modOnVertexBuildHeight));
                BuildNormalMap(pqs, body);
            }
        }
        public async void BuildNormalMap(PQS pqs, CelestialBody body)
        {
            int resX = 4096;
            int resY = 2048;

            Texture2D heightMap = new Texture2D(resX, resY, TextureFormat.RFloat, false);
            double[,] heightValues = new double[resX, resY];
            Vector3d[,] directions = new Vector3d[resX, resY];

            ScreenMessage message = ScreenMessages.PostScreenMessage("Generating terrain data", Single.MaxValue, ScreenMessageStyle.UPPER_CENTER);
            //yield return null;

            Vector3d center = Vector3d.zero;
            float radius = (float)pqs.radius;

            int numTasks = 32;
            int chunkSize = Mathf.CeilToInt((float)resX / numTasks);
            var tasks = new List<Task>();

            List<PQS> pqsTasklist = new List<PQS>();
            List<Delegate> pqsModList = new List<Delegate>();

            float[] threadProgress = new float[numTasks];
            
            for (int i = 0; i < numTasks; i++)
            {
                PQS thisPQS = Instantiate(pqs);
                thisPQS.enabled = false;

                // Remove this, we don't need it and it throws exceptions
                ModularScatter[] kopScatterComponent = thisPQS.GetComponentsInChildren<ModularScatter>();
                for (int j = 0; j < kopScatterComponent.Length; j++) { Destroy(kopScatterComponent[j]); }

                thisPQS.SetupExternalRender();
                pqsTasklist.Add(thisPQS);
                pqsModList.Add((Action<PQS.VertexBuildData, Boolean>)Delegate.CreateDelegate(typeof(Action<PQS.VertexBuildData, Boolean>), thisPQS, typeof(PQS).GetMethod("Mod_OnVertexBuildHeight", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            ThreadSafeProgressReporter reporter = ThreadSafeProgressReporter.Instance;

            // Dispatch the PQSMods split into tasks
            for (int i = 0; i < numTasks; i++)
            {
                int startX = i * chunkSize;
                int endX = Mathf.Min(startX + chunkSize, resX); // Ensure the last chunk does not go out of bounds
                int taskID = i;

                // Dispatch a task for each range
                tasks.Add(Task.Run(() =>
                {
                    VertexBuildData vertexBuildData = new VertexBuildData();

                    for (int x = startX; x < endX; x++)
                    {
                        threadProgress[taskID] = (float)(x - startX) / (float)(endX - startX) * 100.0f;
                        for (int y = 0; y < resY; y++)
                        {
                            vertexBuildData.directionFromCenter = QuaternionD.AngleAxis(360d / resX * x, Vector3d.up) *
                                QuaternionD.AngleAxis(90d - 180d / (resX / 2f) * y, Vector3d.right) *
                                Vector3d.forward;

                            vertexBuildData.vertHeight = radius;
                            ((Action<PQS.VertexBuildData, Boolean>)pqsModList[taskID])(vertexBuildData, true);

                            heightValues[x, y] = vertexBuildData.vertHeight;
                            directions[x, y] = vertexBuildData.directionFromCenter;
                        }
                        // Advance one frame every 32 * resY pixels (can be left out for async)
                        if (x % 32 == 0 && taskID == 0)
                        {
                            float lowestProgressSoFar = threadProgress.Min();
                            reporter.Report(lowestProgressSoFar);
                        }
                    }
                }));
            }
            await Task.WhenAll(tasks);

            Texture2D normalMap = new Texture2D(resX, resY, TextureFormat.ARGB32, false);

            Color[] outputPixels = new Color[resX * resY];

            // Multithreaded normal map generation
            Parallel.For(0, resX, x =>
            {
                for ( int y = 0;y < resY; y++)
                {
                    double height = heightValues[x, y];
                    Vector3d direction = Vector3d.Normalize(directions[x, y]);
                    int adjX = WrapX(resX, x + 1);
                    int adjY = Mathf.Clamp(y + 1, 0, resY - 1);

                    // Real world surface of the vertex at this pixel
                    Vector3d worldPos = center + direction * height;
                    Vector3d worldPosFlat = center + direction * pqs.radius;

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
                    Vector3d dy = Vector3d.Normalize(downVertex -  worldPos);

                    Vector3d realWorldNormal = Vector3d.Normalize(Vector3d.Cross(dx, dy));
                    Vector3 tangentNormal = Vector3.Normalize(TBN.MultiplyVector(realWorldNormal));

                    Color col = new Color(tangentNormal.x, tangentNormal.y, tangentNormal.z, 1);
                    col.r = col.r * 0.5f + 0.5f;
                    col.r = 1 - col.r;
                    col.g = col.g * 0.5f + 0.5f;
                    outputPixels[y * resX + x] = col;
                }
            });

            normalMap.SetPixels(0, 0, resX, resY, outputPixels);

            normalMap.Apply();
            byte[] bytes = normalMap.EncodeToPNG();
            File.WriteAllBytes(KSPUtil.ApplicationRootPath + "GameData/_NormalMap.png", bytes);

            message.duration = 1;
            ScreenMessages.PostScreenMessage("Heightmap extracted and built!", 3.0f);

            // Clean up
            for (int i = 0; i < numTasks; i++)
            {
                PQS thisPQS = pqsTasklist[i];
                Destroy(thisPQS);
            }
        }
        int WrapX(int resX, int x)
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
            messageQueue.Enqueue("Generating map data: Approximately " + percentage + "%");
        }

        private void Update()
        {
            while (messageQueue.TryDequeue(out var message))
            {
                if (screenMessage == null)
                {
                    screenMessage = ScreenMessages.PostScreenMessage(message, 3.0f);
                }
                else
                {
                    screenMessage.textInstance.text.text = message;
                    screenMessage.duration = 3.0f;
                }
            }
        }
    }
}
