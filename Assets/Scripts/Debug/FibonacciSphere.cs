using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FibonacciSphere : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Just an output from the below function
        Vector3[] spherePoints = new Vector3[] 
        {
            new Vector3( 0.000f,  1.000f,  0.000f),
            new Vector3(-0.261f,  0.935f,  0.239f),
            new Vector3( 0.043f,  0.871f, -0.489f),
            new Vector3( 0.360f,  0.806f,  0.469f),
            new Vector3(-0.660f,  0.742f, -0.117f),
            new Vector3( 0.621f,  0.677f, -0.395f),
            new Vector3(-0.205f,  0.613f,  0.763f),
            new Vector3(-0.385f,  0.548f, -0.742f),
            new Vector3( 0.822f,  0.484f,  0.300f),
            new Vector3(-0.839f,  0.419f,  0.346f),
            new Vector3( 0.396f,  0.355f, -0.847f),
            new Vector3( 0.286f,  0.290f,  0.913f),
            new Vector3(-0.843f,  0.226f, -0.488f),
            new Vector3( 0.964f,  0.161f, -0.212f),
            new Vector3(-0.572f,  0.097f,  0.814f),
            new Vector3(-0.128f,  0.032f, -0.991f),
            new Vector3( 0.764f, -0.032f,  0.644f),
            new Vector3(-0.994f, -0.097f,  0.041f),
            new Vector3( 0.700f, -0.161f, -0.696f),
            new Vector3(-0.045f, -0.226f,  0.973f),
            new Vector3(-0.613f, -0.290f, -0.735f),
            new Vector3( 0.927f, -0.355f,  0.125f),
            new Vector3(-0.745f, -0.419f,  0.518f),
            new Vector3( 0.192f, -0.484f, -0.854f),
            new Vector3( 0.416f, -0.548f,  0.726f),
            new Vector3(-0.753f, -0.613f, -0.240f),
            new Vector3( 0.668f, -0.677f, -0.309f),
            new Vector3(-0.259f, -0.742f,  0.618f),
            new Vector3(-0.200f, -0.806f, -0.556f),
            new Vector3( 0.435f, -0.871f,  0.229f),
            new Vector3(-0.342f, -0.935f,  0.090f),
            new Vector3( 0.000f, -1.000f, -0.000f)
        };

        for (int i = 0; i < spherePoints.Length; i++)
        {
            Vector3 point = spherePoints[i];
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.1f;

            go.transform.position = spherePoints[i];
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static List<Vector3> GenerateFibonacciSphere(int numPoints)
    {
        List<Vector3> points = new List<Vector3>(numPoints);
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numPoints; i++)
        {
            float y = 1f - (i / (float)(numPoints - 1)) * 2f; // y goes from 1 to -1
            float radius = Mathf.Sqrt(1f - y * y); // Radius at height y

            float theta = phi * i; // Angle for each point
            float x = Mathf.Cos(theta) * radius;
            float z = Mathf.Sin(theta) * radius;

            points.Add(new Vector3(x, y, z));
            Debug.Log(points[i].ToString("F3"));
        }

        return points;
    }
}
