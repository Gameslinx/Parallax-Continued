using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CubeHeightGenerator : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // First invert the normals of the cube so we can hit it with raycasts
        //GameObject cubeObject = GameObject.Find("RayReceiver");
        //MeshFilter filter = cubeObject.GetComponent<MeshFilter>();
        //
        //MeshCollider collider = cubeObject.GetComponent<MeshCollider>();
        //
        //Mesh mesh = Instantiate(filter.mesh);
        //
        //Vector3[] normals = mesh.normals;
        //for (int i = 0; i < normals.Length; i++)
        //{
        //    normals[i] = -normals[i];
        //}
        //
        //mesh.normals = normals;
        //
        //filter.mesh = mesh;
        //collider.sharedMesh = mesh;

        // Now create the texture - linear space

        Texture2D heightmap = new Texture2D(4096, 2048, TextureFormat.ARGB32, false, true);

        // Now cast for every pixel in our texture

        float[,] data = new float[heightmap.width, heightmap.height];

        float minDistance = 0;
        float maxDistance = -1;

        for (int i = 0; i < heightmap.width; i++)
        {
            for (int j = 0; j < heightmap.height; j++)
            {
                Vector3 directionFromCenter = ConvertToSphericalDirection(i, j, heightmap.width, heightmap.height);

                Vector3 surfacePositionSphere = transform.position + directionFromCenter * 0.5f;
                RaycastHit hit;
                if (Physics.Raycast(surfacePositionSphere, directionFromCenter, out hit, 100.0f))
                {
                    float distance = hit.distance;
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                    data[i, j] = distance;
                }
            }
        }

        // Now normalize the distances to 0 to 1

        for (int i = 0; i < heightmap.width; i++)
        {
            for (int j = 0;j < heightmap.height; j++)
            {
                float distance = data[i, j];
                float normalizedDistance = data[i, j] / maxDistance;
                heightmap.SetPixel(i, j, Color.white * distance * 2.7419354f);
            }
        }

        // Save texture
        heightmap.Apply();
        byte[] bytes = heightmap.EncodeToPNG();
        File.WriteAllBytes("C:/Users/tuvee/Pictures/cubeHeightmap.png", bytes);
    }

    public static Vector3 ConvertToSphericalDirection(int pixelX, int pixelY, int textureWidth, int textureHeight)
    {
        // Normalize pixel coordinates to [0, 1] range
        float u = (float)pixelX / (float)textureWidth;
        float v = (float)pixelY / (float)textureHeight;

        // Convert normalized texture coordinates to spherical coordinates
        float theta = u * 2.0f * Mathf.PI - Mathf.PI; // Longitude, range [-π, π]
        float phi = v * Mathf.PI; // Latitude, range [0, π]

        // Convert spherical coordinates to 3D Cartesian coordinates
        float x = Mathf.Sin(phi) * Mathf.Cos(theta);
        float y = Mathf.Cos(phi);
        float z = Mathf.Sin(phi) * Mathf.Sin(theta);

        return new Vector3(x, y, z).normalized;
    }
}
