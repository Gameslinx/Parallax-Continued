using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectScreenSize : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        float fov = Camera.main.fieldOfView * Mathf.Deg2Rad;
        float d = Vector3.Distance(transform.position, Camera.main.transform.position);
        float r = 0.5f;

        float projRadius = (1.0f / Mathf.Tan(fov * 0.5f)) * r / Mathf.Sqrt(d * d - r * r);
        float screenPixels = projRadius * Screen.height;

        Debug.Log("Size (screen) " + screenPixels);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
