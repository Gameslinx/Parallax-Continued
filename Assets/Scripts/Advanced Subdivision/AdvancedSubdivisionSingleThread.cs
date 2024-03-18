using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeEditor;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
using static UnityEngine.GraphicsBuffer;

namespace Parallax
{
    public struct SubdividableTriangle
    {
        public Vector3 v1, v2, v3;
        public Vector3 n1, n2, n3;
        public Color c1, c2, c3;
        public SubdividableTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, Color c1, Color c2, Color c3)
        {
            this.v1 = v1; this.v2 = v2; this.v3 = v3;
            this.n1 = n1; this.n2 = n2; this.n3 = n3;
            this.c1 = c1; this.c2 = c2; this.c3 = c3;
        }
        public void Subdivide(List<SubdividableTriangle> tris, int level, Vector3 target, int maxSubdivisionLevel, float dist1, float dist2, float dist3)
        {
            if (level == maxSubdivisionLevel) { return; }

            // Get which verts are actually in range
            //float distancev1 = Vector3.Distance(v1, target);
            int subdivisionLevelv1 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist1 / 1.0f);

            //float distancev2 = Vector3.Distance(v2, target);
            int subdivisionLevelv2 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist2 / 1.0f);

            //float distancev3 = Vector3.Distance(v3, target);
            int subdivisionLevelv3 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, dist3 / 1.0f);

            if (AreTwoVertsOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                tris.Add(this);
                return;
            }
            else if (IsOneVertexOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                // Case 1: Line connecting v1 and midpoint between v2 and v3 - two tris
                if (subdivisionLevelv1 < subdivisionLevelv2)
                {
                    Vector3 midPoint = GetVertexBetween(v2, v3);
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    tris.Add(v3v1midPoint);
                    tris.Add(v1v2midPoint);
                    return;
                }
                if (subdivisionLevelv2 < subdivisionLevelv3)
                {
                    Vector3 midPoint = GetVertexBetween(v1, v3);
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    tris.Add(v1v2midPoint);
                    tris.Add(v2v3midPoint);
                    return;
                }
                if (subdivisionLevelv3 < subdivisionLevelv1)
                {
                    Vector3 midPoint = GetVertexBetween(v1, v2);
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black);
                    tris.Add(v3v1midPoint);
                    tris.Add(v2v3midPoint);
                    return;
                }
            }
            else if (AllThreeOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                tris.Add(this);
                return;
            }

            // Divide triangle into 4 new triangles

            // Top tri
            Vector3 tv1 = GetVertexBetween(v1, v3);
            Vector3 tv2 = GetVertexBetween(v3, v2);
            Vector3 tv3 = v3;
                    
            Vector3 tn1 = GetNormalBetween(n1, n3);
            Vector3 tn2 = GetNormalBetween(n3, n2);
            Vector3 tn3 = n3;

            Color tc1 = GetColorBetween(c1, c3);
            Color tc2 = GetColorBetween(c3, c2);
            Color tc3 = c3;

            float td1 = GetFloatBetween(dist1, dist3);
            float td2 = GetFloatBetween(dist3, dist2);
            float td3 = dist3;

            SubdividableTriangle t = new SubdividableTriangle(tv1, tv2, tv3, tn1, tn2, tn3, tc1, tc2, tc3);
            t.Subdivide(tris, level + 1, target, maxSubdivisionLevel, td1, td2, td3);

            // Lower left
            Vector3 blv1 = v1;
            Vector3 blv2 = GetVertexBetween(v1, v2);
            Vector3 blv3 = GetVertexBetween(v1, v3);

            Vector3 bln1 = n1;
            Vector3 bln2 = GetNormalBetween(n1, n2);
            Vector3 bln3 = GetNormalBetween(n1, n3);

            Color blc1 = c1;
            Color blc2 = GetColorBetween(c1, c2);
            Color blc3 = GetColorBetween(c1, c3);

            float bld1 = dist1;
            float bld2 = GetFloatBetween(dist1, dist2);
            float bld3 = GetFloatBetween(dist1, dist3);

            SubdividableTriangle bl = new SubdividableTriangle(blv1, blv2, blv3, bln1, bln2, bln3, blc1, blc2, blc3);
            bl.Subdivide(tris, level + 1, target, maxSubdivisionLevel, bld1, bld2, bld3);

            // Lower right
            Vector3 brv1 = GetVertexBetween(v1, v2);
            Vector3 brv2 = v2;
            Vector3 brv3 = GetVertexBetween(v3, v2);

            Vector3 brn1 = GetNormalBetween(n1, n2);
            Vector3 brn2 = n2;
            Vector3 brn3 = GetNormalBetween(n3, n2);

            Color brc1 = GetColorBetween(c1, c2);
            Color brc2 = c2;
            Color brc3 = GetColorBetween(c3, c2);

            float brd1 = GetFloatBetween(dist1, dist2);
            float brd2 = dist2;
            float brd3 = GetFloatBetween(dist3, dist2);

            SubdividableTriangle br = new SubdividableTriangle(brv1, brv2, brv3, brn1, brn2, brn3, brc1, brc2, brc3);
            br.Subdivide(tris, level + 1, target, maxSubdivisionLevel, brd1, brd2, brd3);

            // Center tri
            Vector3 cv1 = GetVertexBetween(v1, v2);
            Vector3 cv2 = GetVertexBetween(v2, v3);
            Vector3 cv3 = GetVertexBetween(v3, v1);

            Vector3 cn1 = GetNormalBetween(n1, n2);
            Vector3 cn2 = GetNormalBetween(n2, n3);
            Vector3 cn3 = GetNormalBetween(n3, n1);

            Color cc1 = GetColorBetween(c1, c2);    
            Color cc2 = GetColorBetween(c2, c3);
            Color cc3 = GetColorBetween(c3, c1);

            float cd1 = GetFloatBetween(dist1, dist2);
            float cd2 = GetFloatBetween(dist2, dist3);
            float cd3 = GetFloatBetween(dist3, dist1);

            SubdividableTriangle c = new SubdividableTriangle(cv1, cv2, cv3, cn1, cn2, cn3, cc1, cc2, cc3);
            c.Subdivide(tris, level + 1, target, maxSubdivisionLevel, cd1, cd2, cd3);

            if (level + 1 == subdivisionLevelv1 && level + 1 == subdivisionLevelv2 && level + 1 == subdivisionLevelv3)
            {
                tris.Add(t);
                tris.Add(bl);
                tris.Add(br);
                tris.Add(c);
            }
        }
        bool AreTwoVertsOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
        {
            if (level1 == level2 && level3 > level2 && thisLevel + 0 == level2)
                return true;
            if (level2 == level3 && level1 > level3 && thisLevel + 0 == level3)
                return true;
            if (level3 == level1 && level2 > level1 && thisLevel + 0 == level1)
                return true;

            return false;
        }
        bool IsOneVertexOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
        {
            if (level1 == level2 && level3 < level2 && thisLevel + 0 == level3)
                return true;
            if (level2 == level3 && level1 < level3 && thisLevel + 0 == level1)
                return true;
            if (level3 == level1 && level2 < level1 && thisLevel + 0 == level2)
                return true;

            return false;
        }
        bool AllThreeOutOfRange(in int thisLevel, in int level1, in int level2, in int level3)
        {
            if (thisLevel == level1 && thisLevel == level2 && thisLevel == level3)
            {
                return true;
            }
            return false;
        }
        public Vector3 GetVertexBetween(in Vector3 v1, in Vector3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public Vector3 GetNormalBetween(in Vector3 v1, in Vector3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public Color GetColorBetween(in Color v1, in Color v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public float GetFloatBetween(in float v1, in float v2)
        {
            return (v1 + v2) * 0.5f;
        }
    }
    public class ASQuad
    {
        public Dictionary<Vector3, int> newVertexIndices = new Dictionary<Vector3, int>();
        public HashSet<Vector3> newHashVerts = new HashSet<Vector3>();
        public List<int> newTris = new List<int>(22000);
        public List<Vector3> newVerts = new List<Vector3>(22000);
        public List<Vector3> newNormals = new List<Vector3>(22000);
        public List<Color> newColors = new List<Color>(22000);

        public Vector3[] newVerts2;
        public void AppendTriangle(SubdividableTriangle tri)
        {
            int index1;
            int index2;
            int index3;

            if (newVertexIndices.TryAdd(tri.v1, newVertexIndices.Count)) { index1 = newVertexIndices.Count - 1; newVerts.Add(tri.v1); newNormals.Add(tri.n1); newColors.Add(tri.c1); } else { index1 = newVertexIndices[tri.v1]; }
            if (newVertexIndices.TryAdd(tri.v2, newVertexIndices.Count)) { index2 = newVertexIndices.Count - 1; newVerts.Add(tri.v2); newNormals.Add(tri.n2); newColors.Add(tri.c2); } else { index2 = newVertexIndices[tri.v2]; }
            if (newVertexIndices.TryAdd(tri.v3, newVertexIndices.Count)) { index3 = newVertexIndices.Count - 1; newVerts.Add(tri.v3); newNormals.Add(tri.n3); newColors.Add(tri.c3); } else { index3 = newVertexIndices[tri.v3]; }

            newTris.Add(index1);
            newTris.Add(index2);
            newTris.Add(index3);
        }
        List<SubdividableTriangle> subdividedTris = new List<SubdividableTriangle>();
        public void RemoveVertexPairs()
        {
            newVertexIndices.Clear();
            Debug.Log("Subdivided tri length: " + subdividedTris.Count);
            foreach (SubdividableTriangle tri in subdividedTris)
            {
                newVertexIndices.TryAdd(tri.v1, newVertexIndices.Count);
                newVertexIndices.TryAdd(tri.v2, newVertexIndices.Count);
                newVertexIndices.TryAdd(tri.v3, newVertexIndices.Count);
            }
            newVerts2 = new Vector3[newVertexIndices.Count];
            Debug.Log("nvI length = " + newVertexIndices.Count);
        }
        public void ConstructMesh()
        {
            foreach (SubdividableTriangle triangle in subdividedTris)
            {
                int index1 = newVertexIndices[triangle.v1];
                int index2 = newVertexIndices[triangle.v2];
                int index3 = newVertexIndices[triangle.v3];

                newVerts2[index1] = triangle.v1;
                newVerts2[index2] = triangle.v2;
                newVerts2[index3] = triangle.v3;

                newTris.Add(index1);
                newTris.Add(index2);
                newTris.Add(index3);
            }
        }
        public void SubdivideAndAppendTriangles(SubdividableTriangle[] triangles, int subdivisionLevel, Vector3 target, int maxSubdivisionLevel)
        {
            subdividedTris.Clear();
            for (int i = 0; i < triangles.Length; i++)
            {
                SubdividableTriangle tri = triangles[i];
                //int maxSubdivisionLevel = 6;
                float dist1 = Mathf.Clamp01(Vector3.Distance(tri.v1, target) / 10.0f);
                float dist2 = Mathf.Clamp01(Vector3.Distance(tri.v2, target) / 10.0f);
                float dist3 = Mathf.Clamp01(Vector3.Distance(tri.v3, target) / 10.0f);

                triangles[i].Subdivide(subdividedTris, subdivisionLevel, target, maxSubdivisionLevel, dist1, dist2, dist3);
            }
            

            //int count = newVertexIndices.Count - 1;
            //
            //for (int i = 0; i < subdividedTris.Count; i++)
            //{
            //    SubdividableTriangle tri = subdividedTris[i];
            //    int index1;
            //    int index2;
            //    int index3;
            //
            //    if (newVertexIndices.TryAdd(tri.v1, count + 1)) { count++; index1 = count; newVerts.Add(tri.v1); newNormals.Add(tri.n1); newColors.Add(tri.c1); } else { index1 = newVertexIndices[tri.v1]; }
            //    if (newVertexIndices.TryAdd(tri.v2, count + 1)) { count++; index2 = count; newVerts.Add(tri.v2); newNormals.Add(tri.n2); newColors.Add(tri.c2); } else { index2 = newVertexIndices[tri.v2]; }
            //    if (newVertexIndices.TryAdd(tri.v3, count + 1)) { count++; index3 = count; newVerts.Add(tri.v3); newNormals.Add(tri.n3); newColors.Add(tri.c3); } else { index3 = newVertexIndices[tri.v3]; }
            //
            //    newTris.Add(index1);
            //    newTris.Add(index2);
            //    newTris.Add(index3);
            //}
        }
        public void Clear()
        {
            newVertexIndices.Clear();
            newHashVerts.Clear();
            newTris.Clear();
            newVerts.Clear();
            newNormals.Clear();
            newColors.Clear();
        }
    }
    public class AdvancedSubdivision : MonoBehaviour
    {
        public GameObject originalObject;
        GameObject newObject;

        public Vector3[] originalVerts;
        public int[] originalTris;
        public Color[] originalColors;
        public Vector3[] originalNormals;

        public Mesh currentMesh;
        public MeshFilter meshFilter;

        SubdividableTriangle[] triangles;

        ASQuad parent = new ASQuad();
        ASQuad child = new ASQuad();

        [Range(1, 6)]
        public int maxSubdivisionLevel = 2;

        [Range(0.01f, 20.0f)]
        public float subdivisionRange = 10.0f;

        void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            currentMesh = meshFilter.sharedMesh;

            GetMeshData();

            // Debug to make colors work
            originalColors = new Color[originalVerts.Length];
            for (int i = 0; i < originalColors.Length; i++) { originalColors[i] = Color.black; }
            currentMesh.SetColors(originalColors);

            CreateTriangles();
        }
        int done = 0;
        void Update()
        {
            parent.Clear();
            child.Clear();

            SubdividableTriangle tri;
            Vector3 mousePos = transform.InverseTransformPoint(GetMousePosInWorld());
            for (int i = 0; i < triangles.Length; i++)
            {
                

                // With reducing distance from center, level starts at maxSubdivisionLevel and goes down
                
                //parent.AppendTriangle(tri);
            }
            parent.SubdivideAndAppendTriangles(triangles, 0, mousePos, maxSubdivisionLevel);

            parent.RemoveVertexPairs();
            parent.ConstructMesh();

            DebugNoiseMesh();
            SetMeshData();
        }
        void CreateTriangles()
        {
            // Num triangles in the mesh
            triangles = new SubdividableTriangle[originalTris.Length / 3];
            for (int i = 0; i < originalTris.Length; i += 3)                             //Create array of each triangle in the quad
            {
                int index1 = originalTris[i + 0];
                int index2 = originalTris[i + 1];
                int index3 = originalTris[i + 2];

                Vector3 v1 = originalVerts[index1];
                Vector3 v2 = originalVerts[index2];
                Vector3 v3 = originalVerts[index3];

                Vector3 n1 = originalNormals[index1];
                Vector3 n2 = originalNormals[index2];
                Vector3 n3 = originalNormals[index3];

                Color c1 = originalColors[index1];
                Color c2 = originalColors[index2];
                Color c3 = originalColors[index3];

                SubdividableTriangle tri = new SubdividableTriangle(v1, v2, v3, n1, n2, n3, c1, c2, c3);
                triangles[i / 3] = tri;
            }
        }
        Vector3 GetMousePosInWorld()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                return hit.point;
            }
            else
            {
                return Vector3.zero;
            }
        }
        void GetMeshData()
        {
            originalVerts = currentMesh.vertices;
            originalTris = currentMesh.triangles;
            originalNormals = currentMesh.normals;

            // Include this in actual code
            //originalColors = currentMesh.colors;
        }
        void SetMeshData()
        {
            currentMesh.Clear();
            currentMesh.SetVertices(parent.newVerts2);
            currentMesh.triangles = parent.newTris.ToArray();
            //currentMesh.SetNormals(parent.newNormals);
            //currentMesh.SetColors(parent.newColors);
        }
        void Restore()
        {
            currentMesh.Clear();
            currentMesh.SetVertices(originalVerts);
            currentMesh.SetNormals(originalNormals);
            currentMesh.SetColors(originalColors);
            currentMesh.SetIndices(originalTris, MeshTopology.Triangles, 0, true);
        }
        void DebugNoiseMesh()
        {
            for (int i = 0; i < parent.newVerts.Count; i++)
            {
                Vector3 vert = parent.newVerts[i];
                vert.y += Mathf.PerlinNoise(vert.x, vert.z);
                parent.newVerts[i] = vert;
            }
        }
        void OnDrawGizmos()
        {
            Vector3 mousePos = GetMousePosInWorld();
            Gizmos.DrawWireSphere(mousePos, 5.0f);
        }
        void OnDestroy()
        {
            Restore();
        }
    }
}
