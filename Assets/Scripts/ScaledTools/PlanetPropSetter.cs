using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PlanetPropSetter : MonoBehaviour
{
    Material mat;
    
    // Mesh radius
    float _MeshRadius = 1.0f;

    // Altitudes from planet radius and planet radius
    // Real units, real size
    public float _MinAltitude;
    public float _MaxAltitude;
    public float _LowMidBlendStart;
    public float _LowMidBlendEnd;
    public float _MidHighBlendStart;
    public float _MidHighBlendEnd;
    public float _PlanetRadius = 0.5f;
    public float _PlanetRadius2 = 0.5f;

    public float _SkyboxRotation = 0;

    void Start()
    {
        mat = GetComponent<MeshRenderer>().sharedMaterial;
        //_MeshRadius = gameObject.GetComponent<MeshRenderer>().bounds.size.x * 0.5f; //gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * 0.5f;

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh.isReadable)
        {
            Vector3[] verts = GetComponent<MeshFilter>().sharedMesh.vertices;
            float avgRad = 0;
            foreach (Vector3 v in verts)
            {
                avgRad += Vector3.Distance(Vector3.zero, transform.TransformPoint(v));
            }
            avgRad /= verts.Length;
            _MeshRadius = avgRad;
        }
        else
        {
            _MeshRadius = gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * 0.5f * transform.localScale.x;
        }
    }

    // Update is called once per frame
    void Update()
    {

        // Assume normalised heightmap (0 min altitude, 1 max altitude)

        // Multiply by the scaling factor to maintain proportions
        float scalingFactor = _MeshRadius / _PlanetRadius;

        mat.SetVector("_PlanetOrigin", transform.position);
        mat.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
        mat.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);

        mat.SetFloat("_LowMidBlendStart", (_PlanetRadius + _LowMidBlendStart) * scalingFactor);
        mat.SetFloat("_LowMidBlendEnd", (_PlanetRadius + _LowMidBlendEnd) * scalingFactor);
        mat.SetFloat("_MidHighBlendStart", (_PlanetRadius + _MidHighBlendStart) * scalingFactor);
        mat.SetFloat("_MidHighBlendEnd", (_PlanetRadius + _MidHighBlendEnd) * scalingFactor);

        mat.SetFloat("_PlanetRadius", _PlanetRadius2);

        Quaternion rot = Quaternion.Euler(0, _SkyboxRotation, 0);
        Matrix4x4 rotMat = Matrix4x4.Rotate(rot);

        mat.SetMatrix("_SkyboxRotation", rotMat);
    }
}
