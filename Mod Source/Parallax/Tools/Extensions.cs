using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Tools
{
    public static class ColorExtensions
    {
        public static float SqrDistance(this Color a, Color b)
        {
            return (b.r - a.r) * (b.r - a.r) + (b.g - a.g) * (b.g - a.g) + (b.b - a.b) * (b.b - a.b);
        }
    }
}
