using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ScatterGUI : MonoBehaviour
{
    // Update is called once per frame
    private static Rect window = new Rect(100, 100, 450, 300);
    private static Rect windowDefault = new Rect(100, 100, 450, 300);

    static bool showDistribution;
    static bool showMaterial;

    static int currentScatterIndex = 0;

    public static Scatter[] scatters = new Scatter[] { new Scatter("Dummy1"), new Scatter("Dummy2"), new Scatter("Dummy3") };
    void Start()
    {
        window = new Rect(0, 0, 450, 100);
    }
    void OnGUI()
    {
        // Do visibility key check here
        //window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Parallax GUI");
    }
    static void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        ///////////////////////////

        GUIStyle alignment = UnityEngine.GUI.skin.GetStyle("Label");
        alignment.alignment = TextAnchor.MiddleCenter;

        // Reset window size
        if (!showDistribution && !showMaterial)
        {
            window.height = windowDefault.height;
        }

        // Show current scatter
        Scatter scatter = GetScatter();
        GUILayout.Label("Currently displaying scatter: " + scatter.scatterName, alignment);

        // Align correctly
        alignment.alignment = TextAnchor.MiddleLeft;

        CreateFloatParam("Spawn Chance", ref scatter.distributionParams.spawnChance);

        ProcessDistributionParams(scatter);

        ///////////////////////////
        GUILayout.EndVertical();

        // Must be last or buttons wont work
        UnityEngine.GUI.DragWindow();
    }
    static Scatter GetScatter()
    {
        // Advance scatter
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous Scatter"))
        {
            currentScatterIndex = DecrementScatterIndex();
        }
        if (GUILayout.Button("Next Scatter"))
        {
            currentScatterIndex = IncrementScatterIndex();
        }
        GUILayout.EndHorizontal();
        return scatters[currentScatterIndex];
    }
    static void ProcessDistributionParams(Scatter scatter)
    {
        if (GUILayout.Button("Distribution Params"))
        {
            showDistribution = !showDistribution;
        }
        if (showDistribution)
        {
            CreateIntParam("Pop Mult", ref scatter.distributionParams.populationMultiplier);
            CreateVector3Param("Min Scale", ref scatter.distributionParams.minScale);
            
        }
    }
    static int IncrementScatterIndex()
    {
        int newIndex = currentScatterIndex + 1;
        if (newIndex >= scatters.Length)
        {
            newIndex = 0;
        }
        return newIndex;
    }
    static int DecrementScatterIndex()
    {
        int newIndex = currentScatterIndex - 1;
        if (newIndex < 0)
        {
            newIndex = scatters.Length - 1;
        }
        return newIndex;
    }
    static void CreateFloatParam(string name, ref float existingValue)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(name);
        existingValue = GUIUtils.FloatField(existingValue);

        GUILayout.EndHorizontal();
    }
    static void CreateIntParam(string name, ref int existingValue)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(name);
        existingValue = GUIUtils.IntField(existingValue);

        GUILayout.EndHorizontal();
    }
    static void CreateVector3Param(string name, ref Vector3 existingValue)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(name);
        existingValue = GUIUtils.Vector3Field(existingValue);

        GUILayout.EndHorizontal();
    }
    static void CreateColorParam(string name, ref Color existingValue)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(name);
        existingValue = GUIUtils.ColorField(existingValue);

        GUILayout.EndHorizontal();
    }
}
