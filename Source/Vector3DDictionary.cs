using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProceduralCities
{
    public class Vector3Dictionary : Dictionary<Vector3, ushort>
    {
        public Vector3Dictionary()
            : base(new Vector3EqualityComparer())
        {
        }
    }

    public class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private const float precision = 100f;

        public bool Equals(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && Mathf.Approximately(a.z, b.z);
        }

        public int GetHashCode(Vector3 v)
        {
            int xi = Mathf.RoundToInt(v.x * precision);
            int yi = Mathf.RoundToInt(v.y * precision);
            int zi = Mathf.RoundToInt(v.z * precision);

            return xi.GetHashCode() ^ yi.GetHashCode() << 2 ^ zi.GetHashCode() >> 2;
        }
    }

}
