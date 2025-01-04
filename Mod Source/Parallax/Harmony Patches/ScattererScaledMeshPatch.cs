using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Harmony_Patches
{
    /// <summary>
    /// Change how scatterer applies its mesh adjustments to the stock system
    /// </summary>

    //[HarmonyPatch]
    //public class ScattererScaledSphereContainerPatch
    //{
    //    internal static readonly System.Type _type = AccessTools.TypeByName("Scatterer.SkyNode");
    //    internal static MethodBase TargetMethod() => AccessTools.Method(_type, "InitScaledScattering");
    //
    //    [HarmonyPrepare]
    //    internal static bool Prepare()
    //    {
    //        return _type != null;
    //    }
    //    [HarmonyPrefix]
    //    internal static bool Prefix_InitScaledScattering(object __instance)
    //    {
    //        ParallaxDebug.Log("Overriding Scatterer InitScaledScattering function - Any errors from here on are caused by Parallax unless told it's safe");
    //
    //        if (__instance == null)
    //        {
    //            Debug.Log("Instance is null");
    //            return false;
    //        }
    //
    //        Type runtimeType = __instance.GetType();
    //        if (runtimeType.FullName == "Scatterer.SkyNode")
    //        {
    //            Debug.Log("Skynode located");
    //
    //            // Get the field 'scaledScatteringContainer'
    //            var scatteringContainerField = runtimeType.GetField("scaledScatteringContainer", BindingFlags.Public | BindingFlags.Instance);
    //            if (scatteringContainerField != null)
    //            {
    //                Debug.Log("Found scattering container");
    //
    //                // Get the field type of 'scaledScatteringContainer'
    //                Type containerFieldType = scatteringContainerField.FieldType;
    //
    //                // Find the constructor for ScaledScatteringContainer
    //                var constructor = containerFieldType.GetConstructor(new[]
    //                {
    //                    typeof(Mesh),           // Mesh parameter
    //                    typeof(Material),       // Material parameter
    //                    typeof(Transform),      // parentLocalTransform parameter
    //                    typeof(Transform)       // parentScaledTransform parameter
    //                });
    //
    //                // Retrieve fields required for the constructor
    //                Material scaledScatteringMaterial = (Material)runtimeType.GetField("scaledScatteringMaterial", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
    //                Transform parentLocalTransform = (Transform)runtimeType.GetField("parentLocalTransform", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
    //                Transform parentScaledTransform = (Transform)runtimeType.GetField("parentScaledTransform", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
    //
    //                // Access prolandManager and its parentCelestialBody
    //                var prolandManagerField = runtimeType.GetField("prolandManager", BindingFlags.Public | BindingFlags.Instance);
    //                object prolandManager = prolandManagerField.GetValue(__instance);
    //
    //                var parentCelestialBodyField = prolandManager.GetType().GetField("parentCelestialBody", BindingFlags.Public | BindingFlags.Instance);
    //                object parentCelestialBody = parentCelestialBodyField.GetValue(prolandManager);
    //
    //                if (constructor != null)
    //                {
    //                    Debug.Log("Found constructor for ScaledScatteringContainer");
    //
    //                    // Get the shared mesh from the MeshFilter of the parentScaledTransform
    //                    //Mesh sharedMesh = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("ParallaxContinued/Models/ScaledMesh").GetComponent<MeshFilter>().mesh);
    //                    Mesh sharedMesh = UnityEngine.Object.Instantiate(parentScaledTransform.GetComponent<MeshFilter>().sharedMesh);
    //                    FixupScattererMesh(sharedMesh, parentCelestialBody as CelestialBody);
    //
    //                    // Dynamically create an instance of ScaledScatteringContainer
    //                    object scaledScatteringContainer = constructor.Invoke(new object[]
    //                    {
    //                        sharedMesh,
    //                        scaledScatteringMaterial,
    //                        parentLocalTransform,
    //                        parentScaledTransform
    //                    });
    //
    //                    // Assign the instance to the field
    //                    scatteringContainerField.SetValue(__instance, scaledScatteringContainer);
    //                    Debug.Log("Scaled scattering container assigned successfully");
    //
    //                    var pqsControllerField = parentCelestialBody.GetType().GetField("pqsController", BindingFlags.Public | BindingFlags.Instance);
    //                    object pqsController = pqsControllerField.GetValue(parentCelestialBody);
    //
    //                    // Check the loaded scene and switch modes accordingly
    //                    MethodInfo switchLocalMode = containerFieldType.GetMethod("SwitchLocalMode", BindingFlags.Public | BindingFlags.Instance);
    //                    MethodInfo switchScaledMode = containerFieldType.GetMethod("SwitchScaledMode", BindingFlags.Public | BindingFlags.Instance);
    //
    //                    if (HighLogic.LoadedScene != GameScenes.MAINMENU)
    //                    {
    //                        bool isActive = (pqsController as PQS).isActive;
    //
    //                        if (isActive && HighLogic.LoadedScene != GameScenes.TRACKSTATION)
    //                        {
    //                            switchLocalMode.Invoke(scaledScatteringContainer, null);
    //                        }
    //                        else
    //                        {
    //                            switchScaledMode.Invoke(scaledScatteringContainer, null);
    //                        }
    //                    }
    //
    //                    // Set render queue for the material
    //                    scaledScatteringMaterial.renderQueue = 2997;
    //
    //                    // Dynamically call InitUniforms(scaledScatteringMaterial)
    //                    MethodInfo initUniformsMethod = runtimeType.GetMethod("InitUniforms", BindingFlags.Public | BindingFlags.Instance);
    //                    initUniformsMethod.Invoke(__instance, new object[] { scaledScatteringMaterial });
    //
    //                    Debug.Log("InitScaledScattering completed successfully");
    //                }
    //                else
    //                {
    //                    Debug.LogError("Could not find a suitable constructor for ScaledScatteringContainer");
    //                }
    //            }
    //            else
    //            {
    //                Debug.LogError("Could not find field 'scaledScatteringContainer'");
    //            }
    //        }
    //        else
    //        {
    //            Debug.LogError("Type is not Scatterer.SkyNode");
    //        }
    //        ParallaxDebug.Log("Handing control back to Scatterer");
    //        return false;
    //    }
    //    // Approximate scaled mesh
    //    // The function only runs if parallax scaled is present on this body, which means the mesh is always perfectly spherical
    //    // And at altitude 0
    //    static void FixupScattererMesh(Mesh mesh, CelestialBody body)
    //    {
    //
    //        if (!ConfigLoader.parallaxScaledBodies.ContainsKey(body.name))
    //        {
    //            return;
    //        }
    //        ParallaxScaledBody scaledBody = ConfigLoader.parallaxScaledBodies[body.name];
    //
    //        Vector3[] verts = mesh.vertices;
    //        Vector2[] uvs = mesh.uv;
    //        Vector3[] normals = mesh.normals;
    //
    //        // Calculate scaling factors world -> local
    //        // We know this because the mesh is always a parallax scaled mesh
    //        double meshRadius = 1000.0f;
    //        double planetRadius = body.Radius;
    //        double worldToScaledFactor = meshRadius / planetRadius;
    //
    //        // World space real min/max altitude
    //        // We'll pad it a bit so the atmosphere is always slightly above
    //        float minRadialAlt = (float)((scaledBody.minTerrainAltitude + 1) * worldToScaledFactor);
    //        float maxRadialAlt = (float)((scaledBody.maxTerrainAltitude*2 + 1) * worldToScaledFactor);
    //
    //        // Init blit texture
    //        Material mat = new Material(AssetBundleLoader.parallaxScaledShaders["Custom/MaxBlit"]);
    //        mat.SetInt("_KernelSize", 20);
    //
    //        // Load the heightmap
    //        Texture2D heightmap = TextureLoader.LoadTexture(scaledBody.scaledMaterialParams.shaderProperties.shaderTextures["_HeightMap"], true, false);
    //        Debug.Log("Heightmap mip count: " + heightmap.mipmapCount);
    //        RenderTexture rt = new RenderTexture(heightmap.width, heightmap.height, 0, heightmap.graphicsFormat, heightmap.mipmapCount);
    //        rt.useMipMap = true;
    //        Debug.Log("RT mip count: " + rt.mipmapCount);
    //        rt.Create();
    //        Debug.Log("RT mip count: " + rt.mipmapCount);
    //        // Blit heightmap into rt using max filter, then copy back
    //        Graphics.Blit(heightmap, rt, mat);
    //        Graphics.CopyTexture(rt, heightmap);
    //
    //        // Release resources
    //        rt.Release();
    //        UnityEngine.Object.Destroy(rt);
    //        UnityEngine.Object.Destroy(mat);
    //
    //        float heightValue;
    //        for (int i = 0; i < verts.Length; i++)
    //        {
    //            Vector2 uv = uvs[i];
    //            heightValue = heightmap.GetPixelBilinear(uv.x, uv.y, 0).r;
    //            float altitude = Mathf.Lerp(minRadialAlt, maxRadialAlt, heightValue);
    //            if (body.ocean && altitude < 0)
    //            {
    //                altitude = 0;
    //            }
    //
    //
    //            verts[i] = verts[i] + normals[i] * altitude;
    //        }
    //
    //        mesh.vertices = verts;
    //
    //        UnityEngine.Object.Destroy(heightmap);
    //    }
    //}
}
