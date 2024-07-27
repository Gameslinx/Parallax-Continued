using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static KSP.UI.Screens.RDNode;

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
            float scatterRange = ConfigLoader.parallaxScatterBodies[FlightGlobals.currentMainBody.name].scatters.FirstOrDefault().Value.distributionParams.range;
            double quadDistance = quad.gcDist;
            if (quadDistance < scatterRange + Mathf.Sqrt(ScatterComponent.scatterQuadData[quad].sqrQuadWidth))
            {
                material.SetColor("_Color", Color.white);
            }
            else
            {
                material.SetColor("_Color", Color.black);
            }
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
