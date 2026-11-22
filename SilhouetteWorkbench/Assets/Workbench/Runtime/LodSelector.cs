namespace Workbench
{
    public static class LodSelector
    {
        public readonly struct Decision
        {
            public readonly bool Feasible;
            public readonly int Spent;

            internal Decision(bool feasible, int spent)
            {
                Feasible = feasible;
                Spent = spent;
            }
        }

        // Rows are in stable part ID order; costs are positive and strictly increasing.
        // Output levels use -1 for inactive parts. Caller owns and reuses the arrays.
        public static Decision Select(int[][] costs, float[][] losses, bool[] active, int cap, int[] levels)
        {
            var spent = 0;
            for (var i = 0; i < costs.Length; i++)
            {
                levels[i] = active[i] ? 0 : -1;
                if (active[i]) spent += costs[i][0];
            }

            if (spent > cap) return new Decision(false, spent);

            while (true)
            {
                var bestPart = -1;
                var bestLevel = -1;
                var bestExtra = 0;
                var bestRatio = 0.0;
                var remaining = cap - spent;
                for (var i = 0; i < costs.Length; i++)
                {
                    var current = levels[i];
                    if (current < 0) continue;
                    for (var k = current + 1; k < costs[i].Length; k++)
                    {
                        var extra = costs[i][k] - costs[i][current];
                        if (extra > remaining) break;
                        var ratio = ((double)losses[i][current] - losses[i][k]) / extra;
                        // Strict comparison keeps the first part and level on ties.
                        if (ratio <= bestRatio) continue;
                        bestPart = i;
                        bestLevel = k;
                        bestExtra = extra;
                        bestRatio = ratio;
                    }
                }
                if (bestPart < 0) return new Decision(true, spent);
                levels[bestPart] = bestLevel;
                spent += bestExtra;
            }
        }
    }
}
