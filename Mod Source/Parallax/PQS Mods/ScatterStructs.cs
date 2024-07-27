using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using System.Threading.Tasks;

namespace Parallax
{
    // Output from the distribute points kernel
    public struct PositionData
    {
        public Vector3 localPos;
        public Vector3 localScale;
        public float rotation;
        public uint index;

        public static int Size()
        {
            return 7 * sizeof(float) + 1 * sizeof(uint);
        }
    }
    // Output from the evaluate points kernel
    // And sent to shader for rendering
    public struct TransformData
    {
        public Matrix4x4 objectToWorld;
        public static int Size()
        {
            return sizeof(float) * 16;
        }
    };
}
