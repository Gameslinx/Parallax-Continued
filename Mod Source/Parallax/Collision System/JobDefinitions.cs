using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using static Kopernicus.ConfigParser.ParserOptions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;

namespace Parallax
{
    [BurstCompile]
    public struct InitalizeArrayJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> array;
        [ReadOnly] public float initializeTo;
        void IJobParallelFor.Execute(int index)
        {
            array[index] = initializeTo;
        }
    }

    /// <summary>
    /// Holds the PositionData gpu data as well as the quad ID it belongs to.
    /// quadID is not hashed in dictionaries.
    /// </summary>
    public struct PositionDataQuadID : IEquatable<PositionDataQuadID>
    {
        public readonly float3 localPos;
        public readonly float3 localScale;
        public readonly float rotation;
        public readonly uint index;
        public readonly int quadID;
        public PositionDataQuadID(PositionData transform, int quadID)
        {
            this.quadID = quadID;
            this.localPos = transform.localPos;
            this.localScale = transform.localScale;
            this.rotation = transform.rotation;
            this.index = transform.index;
        }
        public override bool Equals(object obj)
        {
            if (obj is PositionDataQuadID)
            {
                return Equals((PositionDataQuadID)obj);
            }
            return false;
        }
        /////////////////
        //  IMPORTANT  //
        /////////////////
        
        // Override equals and gethashcode to prevent quadID from being used when hashing for dictionaries
        // This is because quadID can change and must retain parity with the quad collider data IDs
        public bool Equals(PositionDataQuadID other)
        {
            return localPos.Equals(other.localPos) &&
                   localScale.Equals(other.localScale) &&
                   rotation == other.rotation &&
                   index == other.index;
        }

        public override int GetHashCode()
        {
            unchecked // Allow overflow
            {
                int hash = 17;
                hash = hash * 23 + localPos.GetHashCode();
                hash = hash * 23 + localScale.GetHashCode();
                hash = hash * 23 + rotation.GetHashCode();
                hash = hash * 23 + index.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PositionDataQuadID left, PositionDataQuadID right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PositionDataQuadID left, PositionDataQuadID right)
        {
            return !(left == right);
        }
    }
    [BurstCompile]
    public struct DetermineQuadsForEvaluationJob : IJob
    {
        // Vessel data
        [ReadOnly] public NativeList<float3> vesselPositions;
        [ReadOnly] public NativeList<float> vesselBounds;
        [ReadOnly] public int vesselCount;

        // Quad data
        [ReadOnly] public NativeList<float3> quadPositions;
        [ReadOnly] public NativeList<float> sqrQuadBounds;

        // The quad IDs to evaluate
        [WriteOnly] public NativeList<int> quadIndices;
        [ReadOnly] public int count;

        // Index is quad ID
        void IJob.Execute()
        {
            for (int index = 0; index < count; index++)
            {
                float quadSqrDistance = SqrQuadDistanceToNearestCraft(quadPositions[index]);
                float quadSqrWidth = sqrQuadBounds[index];

                // In range, add quad index and evaluate it on the main thread
                if (quadSqrDistance - quadSqrWidth < 0)
                {
                    quadIndices.Add(index);
                }
            }
            
        }
        public float SqrQuadDistanceToNearestCraft(in float3 worldPos)
        {
            float closest = float.MaxValue;
            float distance;
            for (int i = 0; i < vesselCount; i++)
            {
                // Generous distance calculation that assumes worst case mesh and craft alignment
                distance = SqrMagnitude(vesselPositions[i] - worldPos) - vesselBounds[i];
                if (distance < closest)
                {
                    closest = distance;
                }
            }
            return closest;
        }
        public static float SqrMagnitude(in float3 a)
        {
            return math.dot(a, a);
        }
    }
    [BurstCompile]
    public struct ProcessColliderJob : IJob
    {
        // Input GPU data
        [ReadOnly] public NativeArray<PositionData> positions;
        public NativeArray<float> lastDistances;

        // Vessel data
        [ReadOnly] public NativeList<float3> vesselPositions;
        [ReadOnly] public NativeList<float> vesselBounds;
        [ReadOnly] public NativeList<float4> vesselVelocitiesMagnitude;
        [ReadOnly] public int vesselCount;

        // Quad data
        [ReadOnly] public float3 quadPosition;
        [ReadOnly] public float sqrQuadBound;
        [ReadOnly] public float4x4 localToWorldMatrix;
        [ReadOnly] public int quadID;

        // Scatter data
        [ReadOnly] public float scatterSqrMeshBound;
        [ReadOnly] public int collideableScatterIndex;

        // Outputs
        [WriteOnly] public NativeStream.Writer collidersToAdd;
        [WriteOnly] public NativeStream.Writer collidersToRemove;

        [ReadOnly] public int count;
        [ReadOnly] public int stream;

        // Index of ScatterColliderData
        void IJob.Execute()
        {
            collidersToAdd.BeginForEachIndex(stream);
            collidersToRemove.BeginForEachIndex(stream);

            for (int index = 0; index < count; index++)
            {
                PositionData transform = positions[index];

                float3 localPos = transform.localPos;
                float3 localScale = transform.localScale;

                float4 local = new float4(localPos, 1);

                // Evaluate distance to nearest craft
                float3 worldPos = math.mul(localToWorldMatrix, local).xyz;
                float meshSize = math.max(localScale.x * localScale.x, math.max(localScale.y * localScale.y, localScale.z * localScale.z)) * scatterSqrMeshBound;
                float sqrDistance = SqrDistanceToNearestCraft(worldPos, meshSize);

                // Just come into range, add the collider
                if (Hint.Unlikely(sqrDistance < 0 && lastDistances[index] >= 0))
                {
                    collidersToAdd.Write(new PositionDataQuadID(transform, quadID));
                }
                // Just gone out of range, remove the collider
                if (Hint.Unlikely(sqrDistance >= 0 && lastDistances[index] < 0))
                {
                    collidersToRemove.Write(new PositionDataQuadID(transform, quadID));
                }
                lastDistances[index] = sqrDistance;
            }

            collidersToAdd.EndForEachIndex();
            collidersToRemove.EndForEachIndex();
        }
        public float SqrDistanceToNearestCraft(in float3 worldPos, in float sqrMeshSize)
        {
            float closest = float.MaxValue;
            float distance;
            for (int i = 0; i < vesselCount; i++)
            {
                // Generous distance calculation that assumes worst case mesh and craft alignment
                distance = SqrMagnitude(vesselPositions[i] - worldPos) - vesselBounds[i] - sqrMeshSize;

                // Evaluate look-ahead
                // Vessel to position vector
                float3 colliderDir = math.normalizesafe(worldPos - vesselPositions[i]);
                float3 vesselVelocityDir = vesselVelocitiesMagnitude[i].xyz;

                // Bias heading slightly
                float heading = math.pow(math.saturate(math.dot(vesselVelocityDir, colliderDir)), 4);

                // Distance we're about to travel in the next frame scaled by heading (in direction of rock = 1x)
                // Scaled by how many frames we want to look ahead by (5 seconds)
                float lookAheadDistance = heading * vesselVelocitiesMagnitude[i].w;
                distance -= lookAheadDistance * lookAheadDistance;

                if (distance < closest)
                {
                    closest = distance;
                }
            }
            return closest;
        }
        public static float SqrMagnitude(in float3 a)
        {
            return math.dot(a, a);
        }
    }
}
