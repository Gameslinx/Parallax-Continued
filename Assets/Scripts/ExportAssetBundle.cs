using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections;

public class CreateAssetBundles
{
    [MenuItem("Assets/Build Shader Bundle")]
    static void BuildAllAssetBundles() 
    {
        // Bring up save panel
        string path = EditorUtility.SaveFilePanel("Save Resource", "", "New Resource", "unity3d");

        var opts = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle;
        BuildTarget[] platforms = { BuildTarget.StandaloneWindows, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64 };
        string[] platformExts = { "-windows", "-macosx", "-linux" };

        for (var i = 0; i < platforms.Length; ++i)
        {
            // Build the resource file from the active selection.
            Object[] selection = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            BuildPipeline.BuildAssetBundle(Selection.activeObject, selection, path.Replace(".unity3d", platformExts[i] + ".unity3d"), opts, platforms[i]);
            Selection.objects = selection;
        }//
    }
}