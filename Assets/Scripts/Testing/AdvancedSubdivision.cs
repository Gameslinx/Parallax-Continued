using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeEditor;
using UnityEngine;

namespace Parallax
{
    public struct SubdividableTriangle
    {
        public Vector3 center;
        public Vector3 v1, v2, v3;
        public Vector3 n1, n2, n3;
        public Color c1, c2, c3;
        float dist1, dist2, dist3;
        public SubdividableTriangle(Vector3 center, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, Color c1, Color c2, Color c3, float dist1, float dist2, float dist3)
        {
            this.center = center;
            this.v1 = v1; this.v2 = v2; this.v3 = v3;
            this.n1 = n1; this.n2 = n2; this.n3 = n3;
            this.c1 = c1; this.c2 = c2; this.c3 = c3;
            this.dist1 = dist1;
            this.dist2 = dist2;
            this.dist3 = dist3;
        }
        public void SetupInitialDistances(Vector3 target)
        {
            // Only set for the initial set of triangles
            // Then linearly interpolated across the rest when subdivided, to avoid calculating the vertex distances at each stage
            dist1 = Vector3.Distance(v1, target);
            dist2 = Vector3.Distance(v2, target);
            dist3 = Vector3.Distance(v3, target);
        }
        public void Subdivide(List<SubdividableTriangle> tris, int level, Vector3 target, int maxSubdivisionLevel)
        {
            if (level == maxSubdivisionLevel) { return; }

            // Get which verts are actually in range
            //float distancev1 = Vector3.Distance(v1, target);
            int subdivisionLevelv1 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, Mathf.Clamp01(dist1 / 10.0f));

            //float distancev2 = Vector3.Distance(v2, target);
            int subdivisionLevelv2 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, Mathf.Clamp01(dist2 / 10.0f));

            //float distancev3 = Vector3.Distance(v3, target);
            int subdivisionLevelv3 = (int)Mathf.Lerp(maxSubdivisionLevel, 0, Mathf.Clamp01(dist3 / 10.0f));

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
                    Vector3 midPoint = (v2 + v3) * 0.5f;
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(Center(ref v3, ref v1, ref midPoint), v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(Center(ref v1, ref v2, ref midPoint), v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
                    tris.Add(v3v1midPoint);
                    tris.Add(v1v2midPoint);
                    return;
                }
                if (subdivisionLevelv2 < subdivisionLevelv3)
                {
                    Vector3 midPoint = (v1 + v3) * 0.5f;
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(Center(ref v1, ref v2, ref midPoint), v1, v2, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(Center(ref v2, ref v3, ref midPoint), v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
                    tris.Add(v1v2midPoint);
                    tris.Add(v2v3midPoint);
                    return;
                }
                if (subdivisionLevelv3 < subdivisionLevelv1)
                {
                    Vector3 midPoint = (v1 + v2) * 0.5f;
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(Center(ref v3, ref v1, ref midPoint), v3, v1, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(Center(ref v2, ref v3, ref midPoint), v2, v3, midPoint, Vector3.zero, Vector3.zero, Vector3.zero, Color.black, Color.black, Color.black, 0, 0, 0);
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
            Vector3 tv1 = GetVertexBetween(ref v1, ref v3);
            Vector3 tv2 = GetVertexBetween(ref v3, ref v2);
            Vector3 tv3 = v3;
                    
            Vector3 tn1 = GetNormalBetween(ref n1, ref n3);
            Vector3 tn2 = GetNormalBetween(ref n3, ref n2);
            Vector3 tn3 = n3;

            Color tc1 = GetColorBetween(ref c1, ref c3);
            Color tc2 = GetColorBetween(ref c3, ref c2);
            Color tc3 = c3;

            float td1 = GetFloatBetween(ref dist1, ref dist3);
            float td2 = GetFloatBetween(ref dist3, ref dist2);
            float td3 = dist3;

            SubdividableTriangle t = new SubdividableTriangle(Center(ref tv1, ref tv2, ref tv3), tv1, tv2, tv3, tn1, tn2, tn3, tc1, tc2, tc3, td1, td2, td3);
            t.Subdivide(tris, level + 1, target, maxSubdivisionLevel);

            // Lower left
            Vector3 blv1 = v1;
            Vector3 blv2 = GetVertexBetween(ref v1, ref v2);
            Vector3 blv3 = GetVertexBetween(ref v1, ref v3);

            Vector3 bln1 = n1;
            Vector3 bln2 = GetNormalBetween(ref n1, ref n2);
            Vector3 bln3 = GetNormalBetween(ref n1, ref n3);

            Color blc1 = c1;
            Color blc2 = GetColorBetween(ref c1, ref c2);
            Color blc3 = GetColorBetween(ref c1, ref c3);

            float bld1 = dist1;
            float bld2 = GetFloatBetween(ref dist1, ref dist2);
            float bld3 = GetFloatBetween(ref dist1, ref dist3);

            SubdividableTriangle bl = new SubdividableTriangle(Center(ref blv1, ref blv2, ref blv3), blv1, blv2, blv3, bln1, bln2, bln3, blc1, blc2, blc3, bld1, bld2, bld3);
            bl.Subdivide(tris, level + 1, target, maxSubdivisionLevel);

            // Lower right
            Vector3 brv1 = GetVertexBetween(ref v1, ref v2);
            Vector3 brv2 = v2;
            Vector3 brv3 = GetVertexBetween(ref v3, ref v2);

            Vector3 brn1 = GetNormalBetween(ref n1, ref n2);
            Vector3 brn2 = n2;
            Vector3 brn3 = GetNormalBetween(ref n3, ref n2);

            Color brc1 = GetColorBetween(ref c1, ref c2);
            Color brc2 = c2;
            Color brc3 = GetColorBetween(ref c3, ref c2);

            float brd1 = GetFloatBetween(ref dist1, ref dist2);
            float brd2 = dist2;
            float brd3 = GetFloatBetween(ref dist3, ref dist2);

            SubdividableTriangle br = new SubdividableTriangle(Center(ref brv1, ref brv2, ref brv3), brv1, brv2, brv3, brn1, brn2, brn3, brc1, brc2, brc3, brd1, brd2, brd3);
            br.Subdivide(tris, level + 1, target, maxSubdivisionLevel);

            // Center tri
            Vector3 cv1 = GetVertexBetween(ref v1, ref v2);
            Vector3 cv2 = GetVertexBetween(ref v2, ref v3);
            Vector3 cv3 = GetVertexBetween(ref v3, ref v1);

            Vector3 cn1 = GetNormalBetween(ref n1, ref n2);
            Vector3 cn2 = GetNormalBetween(ref n2, ref n3);
            Vector3 cn3 = GetNormalBetween(ref n3, ref n1);

            Color cc1 = GetColorBetween(ref c1, ref c2);    
            Color cc2 = GetColorBetween(ref c2, ref c3);
            Color cc3 = GetColorBetween(ref c3, ref c1);

            float cd1 = GetFloatBetween(ref dist1, ref dist2);
            float cd2 = GetFloatBetween(ref dist2, ref dist3);
            float cd3 = GetFloatBetween(ref dist3, ref dist1);

            SubdividableTriangle c = new SubdividableTriangle(Center(ref cv1, ref cv2, ref cv3), cv1, cv2, cv3, cn1, cn2, cn3, cc1, cc2, cc3, cd1, cd2, cd3);
            c.Subdivide(tris, level + 1, target, maxSubdivisionLevel);

            if (level + 1 == subdivisionLevelv1 && level + 1 == subdivisionLevelv2 && level + 1 == subdivisionLevelv3)
            {
                tris.Add(t);
                tris.Add(bl);
                tris.Add(br);
                tris.Add(c);
            }
        }
        bool AreTwoVertsOutOfRange(int thisLevel, int level1, int level2, int level3)
        {
            if (level1 == level2 && level3 > level2 && thisLevel + 0 == level2)
                return true;
            if (level2 == level3 && level1 > level3 && thisLevel + 0 == level3)
                return true;
            if (level3 == level1 && level2 > level1 && thisLevel + 0 == level1)
                return true;

            return false;
        }
        bool IsOneVertexOutOfRange(int thisLevel, int level1, int level2, int level3)
        {
            //if (thisLevel > level1 && thisLevel > level2 && thisLevel > level3) { return false; }

            if (level1 == level2 && level3 < level2 && thisLevel + 0 == level3)
                return true;
            if (level2 == level3 && level1 < level3 && thisLevel + 0 == level1)
                return true;
            if (level3 == level1 && level2 < level1 && thisLevel + 0 == level2)
                return true;

            return false;
        }
        bool AllThreeOutOfRange(int thisLevel, int level1, int level2, int level3)
        {
            if (thisLevel == level1 && thisLevel == level2 && thisLevel == level3)
            {
                return true;
            }
            return false;
        }
        Vector3 CalculateIncenter(ref Vector3 pointA, ref Vector3 pointB, ref Vector3 pointC)
        {
            // Calculate the lengths of the sides of the triangle
            float sideA = Vector3.Distance(pointB, pointC);
            float sideB = Vector3.Distance(pointA, pointC);
            float sideC = Vector3.Distance(pointA, pointB);

            // Calculate the coordinates of the incenter using the lengths of the sides
            Vector3 incenter = (sideA * pointA + sideB * pointB + sideC * pointC) / (sideA + sideB + sideC);

            return incenter;
        }
        public Vector3 GetVertexBetween(ref Vector3 v1, ref Vector3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public Vector3 GetNormalBetween(ref Vector3 v1, ref Vector3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public Color GetColorBetween(ref Color v1, ref Color v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public float GetFloatBetween(ref float v1, ref float v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public Vector3 Center(ref Vector3 v1, ref Vector3 v2, ref Vector3 v3)
        {
            return (v1 + v2 + v3) * 0.3333333f;
        }
        public Color Center(ref Color v1, ref Color v2, ref Color v3)
        {
            return (v1 + v2 + v3) * 0.3333333f;
        }
    }
    public class ASQuad
    {
        public Dictionary<Vector3, int> newVertexIndices = new Dictionary<Vector3, int>(22000);
        public HashSet<Vector3> newHashVerts = new HashSet<Vector3>();
        public List<int> newTris = new List<int>(22000);
        public List<Vector3> newVerts = new List<Vector3>(22000);
        public List<Vector3> newNormals = new List<Vector3>(22000);
        public List<Color> newColors = new List<Color>(22000);
        public void AppendTriangle(SubdividableTriangle tri)
        {
            int index1;
            int index2;
            int index3;

            if (newHashVerts.Add(tri.v1)) { index1 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v1, index1); newVerts.Add(tri.v1); newNormals.Add(tri.n1); newColors.Add(tri.c1); } else { index1 = newVertexIndices[tri.v1]; }
            if (newHashVerts.Add(tri.v2)) { index2 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v2, index2); newVerts.Add(tri.v2); newNormals.Add(tri.n2); newColors.Add(tri.c2); } else { index2 = newVertexIndices[tri.v2]; }
            if (newHashVerts.Add(tri.v3)) { index3 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v3, index3); newVerts.Add(tri.v3); newNormals.Add(tri.n3); newColors.Add(tri.c3); } else { index3 = newVertexIndices[tri.v3]; }

            newTris.Add(index1);
            newTris.Add(index2);
            newTris.Add(index3);
        }
        List<SubdividableTriangle> subdividedTris = new List<SubdividableTriangle>();
        public void SubdivideAndAppendTriangle(SubdividableTriangle triangle, int subdivisionLevel, Vector3 target, int maxSubdivisionLevel)
        {
            subdividedTris.Clear();
            triangle.SetupInitialDistances(target);
            triangle.Subdivide(subdividedTris, subdivisionLevel, target, maxSubdivisionLevel);

            for (int i = 0; i < subdividedTris.Count; i++)
            {
                SubdividableTriangle tri = subdividedTris[i];
                int index1;
                int index2;
                int index3;
                if (newHashVerts.Add(tri.v1)) { index1 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v1, index1); newVerts.Add(tri.v1); newNormals.Add(tri.n1); newColors.Add(tri.c1); } else { index1 = newVertexIndices[tri.v1]; }
                if (newHashVerts.Add(tri.v2)) { index2 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v2, index2); newVerts.Add(tri.v2); newNormals.Add(tri.n2); newColors.Add(tri.c2); } else { index2 = newVertexIndices[tri.v2]; }
                if (newHashVerts.Add(tri.v3)) { index3 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v3, index3); newVerts.Add(tri.v3); newNormals.Add(tri.n3); newColors.Add(tri.c3); } else { index3 = newVertexIndices[tri.v3]; }
                newTris.Add(index1);
                newTris.Add(index2);
                newTris.Add(index3);
            }
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
            float distance = 0;
            Vector3 mousePos = transform.InverseTransformPoint(GetMousePosInWorld());
            for (int i = 0; i < triangles.Length; i++)
            {
                tri = triangles[i];
                distance = Vector3.Distance(tri.center, mousePos);
                int maxSubdivisionLevel = 6;
                int subdivisionLevel = (int)Mathf.Lerp(maxSubdivisionLevel, 0, Mathf.Clamp01(distance / 10.0f));
                if (subdivisionLevel > 0)
                {
                    // With reducing distance from center, level starts at maxSubdivisionLevel and goes down
                    parent.SubdivideAndAppendTriangle(tri, 0, mousePos, maxSubdivisionLevel);
                }
                else
                {
                    parent.AppendTriangle(tri);
                }
            }
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

                Vector3 center = (v1 + v2 + v3) / 3;

                SubdividableTriangle tri = new SubdividableTriangle(center, v1, v2, v3, n1, n2, n3, c1, c2, c3, originalTris[i], originalTris[i + 1], originalTris[i + 2]);
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
            currentMesh.SetVertices(parent.newVerts);
            currentMesh.triangles = parent.newTris.ToArray();
            currentMesh.SetNormals(parent.newNormals);
            currentMesh.SetColors(parent.newColors);
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
