using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rtp : MonoBehaviour
{
    GameObject[] spheres = new GameObject[10];
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            foreach (GameObject go in spheres)
            {
                Destroy(go);
            }
            for (int i = 0; i < spheres.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                float rX = (Random.value * 2.0f - 1.0f) * 10.0f;
                float rY = (Random.value * 2.0f - 1.0f) * 10.0f;

                go.transform.position = new Vector3(rX, 0, rY) + transform.position;
                //go.transform.localScale = Vector3.one * 0.1f;

                spheres[i] = go;
            }
        }
    }
}
