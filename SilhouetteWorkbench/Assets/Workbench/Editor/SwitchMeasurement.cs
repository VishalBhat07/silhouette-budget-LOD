namespace Workbench
{
    public static class SwitchMeasurement
    {
        public readonly struct Result
        {
            public readonly int ChangedIdPixels;
            public readonly float WeightedError;

            public Result(int changedIdPixels, float weightedError)
            {
                ChangedIdPixels = changedIdPixels;
                WeightedError = weightedError;
            }
        }

        public static Result Measure(byte[] before, byte[] after, int parts)
        {
            var stats = PartMaskMetrics.Measure(before, after, parts);
            var changed = 0;
            for (var i = 0; i < before.Length; i++)
                if (before[i] != after[i]) changed++;
            var xor = 0;
            var union = 0;
            foreach (var mask in stats)
            {
                xor += mask.Reference + mask.Candidate - 2 * mask.Intersection;
                union += mask.Union;
            }
            return new Result(changed, union == 0 ? 0f : (float)xor / union);
        }
    }
}
