using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// Calculate frustum planes
// Monobehaviour version for Unity, rather than KSP
class CameraUtils : MonoBehaviour
{
    public static NativeArray<ParallaxPlane> planeNormals;
    public static float[] scatterPlaneNormals;
    private void ConstructFrustumPlanes(Camera camera)
    {
        // https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
        // Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        const int floatPerNormal = 4;
        Plane[] unityPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

        // Convert from unity plane to parallax plane which has float3 support
        for (int i = 0; i < unityPlanes.Length; i++)
        {
            planeNormals[i] = unityPlanes[i];
        }
        scatterPlaneNormals = new float[unityPlanes.Length * floatPerNormal];
        for (int i = 0; i < unityPlanes.Length; ++i)
        {
            scatterPlaneNormals[i * floatPerNormal + 0] = unityPlanes[i].normal.x;
            scatterPlaneNormals[i * floatPerNormal + 1] = unityPlanes[i].normal.y;
            scatterPlaneNormals[i * floatPerNormal + 2] = unityPlanes[i].normal.z;
            scatterPlaneNormals[i * floatPerNormal + 3] = unityPlanes[i].distance;
        }
    }
    void Start()
    {
        planeNormals = new NativeArray<ParallaxPlane>(6, Allocator.Persistent);
    }
    void Update()
    {
        ConstructFrustumPlanes(Camera.main);
    }
    void OnDisable()
    {
        planeNormals.Dispose();
    }
}
