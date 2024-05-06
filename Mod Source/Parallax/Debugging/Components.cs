using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Debugging
{
    public class QuadRangeComponent : MonoBehaviour
    {
        public float maxRange = 20000.0f;
        public PQ quad;
        Material material;
        void Start ()
        {
            material = GetComponent<MeshRenderer>().sharedMaterial;
        }
        void Update()
        {
            double quadDistance = quad.gcDist;
            float percentage = (float)quadDistance / maxRange;
            material.SetColor("_Color", Color.white * percentage);
        }
    }

    public class QuadBiomeComponent : MonoBehaviour
    {
        public PQ quad;
        Material material;
        void Start()
        {
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            Vector2[] uvs = PQSMod_Parallax.quadPlanetUVs[quad];
            mesh.uv = uvs;

            material = GetComponent<MeshRenderer>().sharedMaterial;
            Texture2D biomeMap = ScatterManager.currentBiomeMap;
            material.SetTexture("_MainTex", biomeMap);
        }
    }
}
