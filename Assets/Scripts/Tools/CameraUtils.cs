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
    private void ConstructFrustumPlanes(Camera camera)
    {
        // https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
        // Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        Plane[] unityPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

        // Convert from unity plane to parallax plane which has float3 support
        for (int i = 0; i < unityPlanes.Length; i++)
        {
            planeNormals[i] = unityPlanes[i];
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
