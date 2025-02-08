using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class CreateAssetBundles
{
    [MenuItem("Assets/Build Shader Bundle")]
    static void BuildAllAssetBundles()
    {
        // Bring up save panel
        string path = EditorUtility.SaveFilePanel("Save Resource", "", "New Resource", "unity3d");

        // Check if the user canceled the dialog
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("Asset bundle build canceled by user.");
            return;
        }

        var opts = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle;
        BuildTarget[] platforms = { BuildTarget.StandaloneWindows, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64 };
        string[] platformExts = { "-windows", "-macosx", "-linux" };

        for (var i = 0; i < platforms.Length; ++i)
        {
            // Build the resource file from the active selection.
            Object[] selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
            var shaderAssets = new List<Object>();

            foreach (Object obj in selection)
            {
                string pathToObject = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(pathToObject))
                {
                    string[] shaderPaths = AssetDatabase.FindAssets("t:Shader", new[] { pathToObject });
                    foreach (string shaderGUID in shaderPaths)
                    {
                        string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGUID);
                        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                        if (shader != null)
                        {
                            shaderAssets.Add(shader);
                        }
                    }

                    string[] computeShaderPaths = AssetDatabase.FindAssets("t:ComputeShader", new[] { pathToObject });
                    foreach (string shaderGUID in computeShaderPaths)
                    {
                        string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGUID);
                        ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);
                        if (shader != null)
                        {
                            shaderAssets.Add(shader);
                        }
                    }
                }
                else if (obj is Shader || obj is ComputeShader)
                {
                    shaderAssets.Add(obj);
                }
            }

            // Set the main asset to the first shader, or null if no shaders are found
            Object mainAsset = shaderAssets.Count > 0 ? shaderAssets[0] : null;

            #pragma warning disable CS0618
            BuildPipeline.BuildAssetBundle(mainAsset, shaderAssets.ToArray(), path.Replace(".unity3d", platformExts[i] + ".unity3d"), opts, platforms[i]);
        }
    }
}