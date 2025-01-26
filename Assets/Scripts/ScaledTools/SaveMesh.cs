using UnityEngine;
using UnityEditor;
using System.IO;

public class SaveMesh : EditorWindow
{
    private GameObject selectedGameObject;

    [MenuItem("Parallax/Save Mesh To Obj")]
    private static void ShowWindow()
    {
        GetWindow<SaveMesh>("Save Mesh");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Select a GameObject with a Mesh", EditorStyles.boldLabel);
        selectedGameObject = (GameObject)EditorGUILayout.ObjectField("GameObject", selectedGameObject, typeof(GameObject), true);

        if (GUILayout.Button("Save Mesh") && selectedGameObject != null)
        {
            string path = EditorUtility.SaveFilePanel("Save Mesh", "", "New Mesh", "obj");
            if (File.Exists(path))
            {
                MeshExporter.SaveMeshAsOBJ(selectedGameObject.GetComponent<MeshFilter>().mesh, path);
            }
        }
    }

}

