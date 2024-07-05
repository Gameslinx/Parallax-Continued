using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LimitFPS : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 1000;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
