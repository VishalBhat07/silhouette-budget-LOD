using System;

namespace Workbench
{
    public static class PartMaskMetrics
    {
        public struct Counts
        {
            public int Reference;
            public int Candidate;
            public int Intersection;
            public int Union => Reference + Candidate - Intersection;
            public float Error => Union == 0 ? float.NaN : 1f - (float)Intersection / Union;
        }

        public static Counts[] Measure(byte[] reference, byte[] candidate, int partCount)
        {
            if (reference.Length != candidate.Length || partCount < 1 || partCount > 255)
                throw new ArgumentException("Expected equally sized ID buffers and one or more objects.");
            var counts = new Counts[partCount + 1];
            for (var i = 0; i < reference.Length; i++)
            {
                var a = reference[i];
                var b = candidate[i];
                if (a > partCount || b > partCount)
                    throw new ArgumentException($"Unknown object ID at pixel {i}: {a}, {b}.");
                counts[a].Reference++;
                counts[b].Candidate++;
                if (a == b) counts[a].Intersection++;
            }
            var result = new Counts[partCount];
            Array.Copy(counts, 1, result, 0, partCount);
            return result;
        }
    }
}
