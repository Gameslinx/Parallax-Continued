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
                                "SHADOWS_CUBE", "SHADOWS_DEPTH", "SHADOWS_SCREEN", "SHADOWS_SOFT", "SPOT", "STEREO_CUBEMAP_RENDER_ON", "STEREO_INSTANCING_ON", "STEREO_MULTIVIEW_ON",
                                "UNITY_SINGLE_PASS_STEREO", "VERTEXLIGHT_ON" };
    Dictionary<string, bool> shaderKeywords = new Dictionary<string, bool>();

    [MenuItem("Parallax/Edit Shader Keywords")]
    private static void Initialize()
    {
        mWindow = GetWindow<KeywordsMenu>("Edit Parallax Shader Keywords");
        mWindow.Show();
        keywordMethodInfo = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", BindingFlags.Static | BindingFlags.NonPublic);
    }
    public enum RenderType
    {
        opaque,
        cutout,
        transparent
    }
    RenderType renderType = RenderType.opaque;
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
        renderType = (RenderType)EditorGUILayout.EnumPopup(renderType);
        if (renderType == RenderType.opaque)
        {
            parallaxMaterial.SetOverrideTag("RenderType", "Opaque");
            parallaxMaterial.SetOverrideTag("Queue", "Geometry");

            parallaxMaterial.SetInt("_SrcMode", (int)UnityEngine.Rendering.BlendMode.One);
            parallaxMaterial.SetInt("_DstMode", (int)UnityEngine.Rendering.BlendMode.Zero);
        }
        if (renderType == RenderType.cutout)
        {
            parallaxMaterial.SetOverrideTag("RenderType", "TransparentCutout");
            parallaxMaterial.SetOverrideTag("IgnoreProjector", "True");
            parallaxMaterial.SetOverrideTag("Queue", "AlphaTest");

            parallaxMaterial.SetInt("_SrcMode", (int)UnityEngine.Rendering.BlendMode.One);
            parallaxMaterial.SetInt("_DstMode", (int)UnityEngine.Rendering.BlendMode.Zero);
        }
        if (renderType == RenderType.transparent)
        {
            // We set it to opaque so we can have shadows
            parallaxMaterial.SetOverrideTag("RenderType", "Opaque");
            parallaxMaterial.SetOverrideTag("Queue", "Geometry");

            parallaxMaterial.SetInt("_SrcMode", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            parallaxMaterial.SetInt("_DstMode", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
    }
}