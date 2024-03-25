using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Parallax
{
    public struct SubdividableTriangle
    {
        public float3 v1, v2, v3;
        public float3 n1, n2, n3;
        public float4 c1, c2, c3;
        public SubdividableTriangle(float3 v1, float3 v2, float3 v3, float3 n1, float3 n2, float3 n3, float4 c1, float4 c2, float4 c3)
        {
            this.v1 = v1; this.v2 = v2; this.v3 = v3;
            this.n1 = n1; this.n2 = n2; this.n3 = n3;
            this.c1 = c1; this.c2 = c2; this.c3 = c3;
        }
        public void Subdivide(ref NativeStream.Writer tris, in int level, in float3 target, in int maxSubdivisionLevel, in float subdivisionRange, in float4x4 objectToWorld)
        {
            if (level == maxSubdivisionLevel) { return; }

            float3 worldPosV1 = math.mul(objectToWorld, new float4(v1, 1)).xyz;
            float3 worldPosV2 = math.mul(objectToWorld, new float4(v2, 1)).xyz;
            float3 worldPosV3 = math.mul(objectToWorld, new float4(v3, 1)).xyz;

            // Get which verts are actually in range
            int subdivisionLevelv1 = (int)math.lerp(maxSubdivisionLevel, 0, CalculateDistance(worldPosV1, target, subdivisionRange));
            int subdivisionLevelv2 = (int)math.lerp(maxSubdivisionLevel, 0, CalculateDistance(worldPosV2, target, subdivisionRange));
            int subdivisionLevelv3 = (int)math.lerp(maxSubdivisionLevel, 0, CalculateDistance(worldPosV3, target, subdivisionRange));

            //
            //  Mathematically this subdivision scheme works because there will never be a fully subdivided triangle with an edge that borders a triangle
            //  that has two vertices out of range.
            //  
            //  This means we don't have a t junction when two vertices are out of range, but we do when we have one out of range.
            //  And to remove that T junction with 1 vertex out of range, we just need to connect that far vertex to the midpoint of the edge opposite it
            //
            if (AreTwoVertsOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                tris.Write(this);
                return;
            }
            else if (IsOneVertexOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                if (subdivisionLevelv1 < subdivisionLevelv2)
                {
                    float3 midPointV = GetVertexBetween(v2, v3);
                    float3 midPointN = GetNormalBetween(n2, n3);
                    float4 midPointC = GetColorBetween(c2, c3);
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPointV, n3, n1, midPointN, c3, c1, midPointC);
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPointV, n1, n2, midPointN, c1, c2, midPointC);
                    tris.Write(v3v1midPoint);
                    tris.Write(v1v2midPoint);
                    return;
                }
                if (subdivisionLevelv2 < subdivisionLevelv3)
                {
                    float3 midPointV = GetVertexBetween(v1, v3);
                    float3 midPointN = GetNormalBetween(n1, n3);
                    float4 midPointC = GetColorBetween(c1, c3);
                    SubdividableTriangle v1v2midPoint = new SubdividableTriangle(v1, v2, midPointV, n1, n2, midPointN, c1, c2, midPointC);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPointV, n2, n3, midPointN, c2, c3, midPointC);
                    tris.Write(v1v2midPoint);
                    tris.Write(v2v3midPoint);
                    return;
                }
                if (subdivisionLevelv3 < subdivisionLevelv1)
                {
                    float3 midPointV = GetVertexBetween(v1, v2);
                    float3 midPointN = GetNormalBetween(n1, n2);
                    float4 midPointC = GetColorBetween(c1, c2);
                    SubdividableTriangle v3v1midPoint = new SubdividableTriangle(v3, v1, midPointV, n3, n1, midPointN, c3, c1, midPointC);
                    SubdividableTriangle v2v3midPoint = new SubdividableTriangle(v2, v3, midPointV, n2, n2, midPointN, c2, c3, midPointC);
                    tris.Write(v3v1midPoint);
                    tris.Write(v2v3midPoint);
                    return;
                }
            }
            else if (AllThreeOutOfRange(level, subdivisionLevelv1, subdivisionLevelv2, subdivisionLevelv3))
            {
                tris.Write(this);
                return;
            }

            // Divide triangle into 4 new triangles

            // Top tri
            float3 tv1 = GetVertexBetween(v1, v3);
            float3 tv2 = GetVertexBetween(v3, v2);
            float3 tv3 = v3;

            float3 tn1 = GetNormalBetween(n1, n3);
            float3 tn2 = GetNormalBetween(n3, n2);
            float3 tn3 = n3;

            float4 tc1 = GetColorBetween(c1, c3);
            float4 tc2 = GetColorBetween(c3, c2);
            float4 tc3 = c3;

            SubdividableTriangle t = new SubdividableTriangle(tv1, tv2, tv3, tn1, tn2, tn3, tc1, tc2, tc3);
            t.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);

            // Lower left
            float3 blv1 = v1;
            float3 blv2 = GetVertexBetween(v1, v2);
            float3 blv3 = GetVertexBetween(v1, v3);

            float3 bln1 = n1;
            float3 bln2 = GetNormalBetween(n1, n2);
            float3 bln3 = GetNormalBetween(n1, n3);

            float4 blc1 = c1;
            float4 blc2 = GetColorBetween(c1, c2);
            float4 blc3 = GetColorBetween(c1, c3);

            SubdividableTriangle bl = new SubdividableTriangle(blv1, blv2, blv3, bln1, bln2, bln3, blc1, blc2, blc3);
            bl.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);

            // Lower right
            float3 brv1 = GetVertexBetween(v1, v2);
            float3 brv2 = v2;
            float3 brv3 = GetVertexBetween(v3, v2);

            float3 brn1 = GetNormalBetween(n1, n2);
            float3 brn2 = n2;
            float3 brn3 = GetNormalBetween(n3, n2);

            float4 brc1 = GetColorBetween(c1, c2);
            float4 brc2 = c2;
            float4 brc3 = GetColorBetween(c3, c2);

            SubdividableTriangle br = new SubdividableTriangle(brv1, brv2, brv3, brn1, brn2, brn3, brc1, brc2, brc3);
            br.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);

            // Center tri
            float3 cv1 = GetVertexBetween(v1, v2);
            float3 cv2 = GetVertexBetween(v2, v3);
            float3 cv3 = GetVertexBetween(v3, v1);

            float3 cn1 = GetNormalBetween(n1, n2);
            float3 cn2 = GetNormalBetween(n2, n3);
            float3 cn3 = GetNormalBetween(n3, n1);

            float4 cc1 = GetColorBetween(c1, c2);
            float4 cc2 = GetColorBetween(c2, c3);
            float4 cc3 = GetColorBetween(c3, c1);

            SubdividableTriangle c = new SubdividableTriangle(cv1, cv2, cv3, cn1, cn2, cn3, cc1, cc2, cc3);
            c.Subdivide(ref tris, level + 1, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);

            if (level + 1 == subdivisionLevelv1 && level + 1 == subdivisionLevelv2 && level + 1 == subdivisionLevelv3)
            {
                tris.Write(t);
                tris.Write(bl);
                tris.Write(br);
                tris.Write(c);
            }
        }
        float CalculateDistance(in float3 pos, in float3 target, in float maxRange)
        {
            float log2SqrMaxRange = math.log2(maxRange * maxRange);
            float dist = math.distance(pos, target);
            float log2SqrDist = math.log2(dist * dist);
            return math.pow(math.saturate(log2SqrDist / log2SqrMaxRange), 1.6f);
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
        public float3 GetVertexBetween(in float3 v1, in float3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public float3 GetNormalBetween(in float3 v1, in float3 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public float4 GetColorBetween(in float4 v1, in float4 v2)
        {
            return (v1 + v2) * 0.5f;
        }
        public float GetFloatBetween(in float v1, in float v2)
        {
            return (v1 + v2) * 0.5f;
        }
    }

    // Used in frustum culling
    public struct ParallaxPlane
    {
        float3 normal;
        float distance;
        public ParallaxPlane(float3 normal, float distance)
        {
            this.normal = normal;
            this.distance = distance;
        }
        // Allow cast from Plane to ParallaxPlane
        public static implicit operator ParallaxPlane(Plane plane)
        {
            return new ParallaxPlane(plane.normal, plane.distance);
        }
        // Is this position on the positive side of the plane
        public bool GetSide(in Vector3 pos)
        {
            return math.dot(pos, normal) + distance > 0;
        }
    }
}
