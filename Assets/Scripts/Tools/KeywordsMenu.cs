using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using System.Reflection;
using System.Linq;

public class KeywordsMenu : EditorWindow
{
    private static KeywordsMenu mWindow;
    public static Material parallaxMaterial;
    public static Material lastParallaxMaterial;
    static MethodInfo keywordMethodInfo;
    string[] allKeywordNames;
    string[] ignoredKeywords = {"DIRECTIONAL", "DIRECTIONAL_COOKIE", "DIRLIGHTMAP_COMBINED", "DYNAMICLIGHTMAP_ON", "LIGHTMAP_ON", "LIGHTMAP_SHADOW_MIXING", "LIGHTPROBE_SH", "POINT", "POINT_COOKIE",
                                "SHADOWS_CUBE", "SHADOWS_DEPTH", "SHADOWS_SCREEN", "SHADOWS_SHADOWMASK", "SHADOWS_SOFT", "SPOT", "STEREO_CUBEMAP_RENDER_ON", "STEREO_INSTANCING_ON", "STEREO_MULTIVIEW_ON",
                                "UNITY_SINGLE_PASS_STEREO", "VERTEXLIGHT_ON" };
    Dictionary<string, bool> shaderKeywords = new Dictionary<string, bool>();

    [MenuItem("Parallax/Edit Shader Keywords")]
    private static void Initialize()
    {
        mWindow = GetWindow<KeywordsMenu>("Edit Parallax Shader Keywords");
        mWindow.Show();
        keywordMethodInfo = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", BindingFlags.Static | BindingFlags.NonPublic);
    }
    public void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Parallax Keywords Editor");
        parallaxMaterial = (Material)EditorGUILayout.ObjectField("Material", parallaxMaterial, typeof(Material), true);
        if (parallaxMaterial == null)
        {
            return;
        }
        if (lastParallaxMaterial != parallaxMaterial)
        {
            keywordMethodInfo = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", BindingFlags.Static | BindingFlags.NonPublic);
            lastParallaxMaterial = parallaxMaterial;
            Shader shader = parallaxMaterial.shader;//AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Parallax.shader");
            allKeywordNames = (string[])keywordMethodInfo.Invoke(null, new object[] { shader });
            shaderKeywords = new Dictionary<string, bool>();
            for (int i = 0; i < allKeywordNames.Length; i++)
            {
                if (!ignoredKeywords.Contains(allKeywordNames[i]))
                {
                    shaderKeywords.Add(allKeywordNames[i], false);
                }
            }
        }

        int numKeywords = shaderKeywords.Count;
        string[] keys = shaderKeywords.Keys.ToArray();
        for (int i = 0; i < numKeywords; i++)
        {
            EditorGUIUtility.labelWidth = 300;
            shaderKeywords[keys[i]] = EditorGUILayout.Toggle(keys[i] + "", shaderKeywords[keys[i]]);

            if (shaderKeywords[keys[i]] == true)
            {
                parallaxMaterial.EnableKeyword(keys[i]);
            }
            else
            {
                parallaxMaterial.DisableKeyword(keys[i]);
            }
        }
    }
}