using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using System.Reflection;
using System.Linq;
using UnityEngine.Rendering;

public class KeywordsMenu : EditorWindow
{
    private static KeywordsMenu mWindow;
    public static Material parallaxMaterial;
    public static Material lastParallaxMaterial;
    static Dictionary<string, bool> shaderKeywords = new Dictionary<string, bool>();
    static List<string> storedKeywords = new List<string>();

    [MenuItem("Parallax/Edit Shader Keywords")]
    private static void Initialize()
    {
        mWindow = GetWindow<KeywordsMenu>("Edit Parallax Shader Keywords");
        mWindow.Show();
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
        string[] allEnabled = parallaxMaterial.shaderKeywords;
        for (int i = 0; i < allEnabled.Length; i++)
        {
            Debug.Log(allEnabled[i]);
        }
        if (parallaxMaterial != lastParallaxMaterial)
        {
            lastParallaxMaterial = parallaxMaterial;

            storedKeywords.Clear();
            shaderKeywords.Clear();

            DefineAllKeywords();
            AddAllKeywords();
        }
        string[] keys = shaderKeywords.Keys.ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            shaderKeywords[keys[i]] = EditorGUILayout.Toggle(keys[i] + "", shaderKeywords[keys[i]]);
            if (shaderKeywords[keys[i]] == false) 
            {
                parallaxMaterial.DisableKeyword(keys[i]);
            }
            else
            {
                parallaxMaterial.EnableKeyword(keys[i]);

            }
        }

        //string[] allEnabled = parallaxMaterial.shaderKeywords;
        //for (int i = 0; i < allEnabled.Length; i++)
        //{
        //    Debug.Log(allEnabled[i]);
        //}
    }
    public static void DefineAllKeywords()
    {
        storedKeywords.Add("INFLUENCE_MAPPING");

        storedKeywords.Add("PARALLAX_SINGLE_LOW");
        storedKeywords.Add("PARALLAX_SINGLE_MID");
        storedKeywords.Add("PARALLAX_SINGLE_HIGH");

        storedKeywords.Add("PARALLAX_DOUBLE_LOWMID");
        storedKeywords.Add("PARALLAX_DOUBLE_MIDHIGH");

        storedKeywords.Add("PARALLAX_FULL");
    }
    public static void AddAllKeywords()
    {
        foreach(string key in storedKeywords)
        {
            shaderKeywords.Add(key, false);
        }
    }
}
