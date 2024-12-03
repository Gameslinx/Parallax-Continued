using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PlanetPropSetter : MonoBehaviour
{
    Material mat;
    public float _PlanetRadius = 0.5f;
    // Start is called before the first frame update
    void Start()
    {
        mat = GetComponent<MeshRenderer>().sharedMaterial;
    }

    // Update is called once per frame
    void Update()
    {
        mat.SetFloat("_PlanetRadius", _PlanetRadius);
        mat.SetVector("_PlanetOrigin", transform.position);
        //Matrix4x4 scaleOnly = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 100.0f);
        //
        //// zero
        //Vector3 inversePos = -transform.position;
        //Quaternion inverseQuaternion = Quaternion.Inverse(transform.rotation);
        //
        //scaleOnly = Matrix4x4.TRS(-inversePos, inverseQuaternion, Vector3.one);
        //
        //mat.SetMatrix("unity_ObjectToWorldNoTR", scaleOnly);
    }
}
