using System;
using System.Collections.Generic;
using System.Linq;

namespace Workbench
{
    // Offline control. A state represents one exact sum, not a cap.
    public static class ExactCostLod
    {
        readonly struct State
        {
            public readonly double Loss;
            public readonly int[] Levels;

            public State(double loss, int[] levels)
            {
                Loss = loss;
                Levels = levels;
            }
        }

        public static bool TrySelect(int[][] costs, float[][] losses, int target, int[] destination)
        {
            var states = new Dictionary<int, State> { [0] = new State(0, Array.Empty<int>()) };
            for (var part = 0; part < costs.Length; part++)
            {
                var next = new Dictionary<int, State>();
                foreach (var entry in states)
                for (var level = 0; level < costs[part].Length; level++)
                {
                    var sum = entry.Key + costs[part][level];
                    if (sum > target) continue;
                    var loss = entry.Value.Loss + losses[part][level];
                    if (next.TryGetValue(sum, out var incumbent) &&
                        (loss > incumbent.Loss || loss == incumbent.Loss &&
                            !PrefersEarlierParts(entry.Value.Levels, level, incumbent.Levels)))
                        continue;
                    var levels = new int[part + 1];
                    Array.Copy(entry.Value.Levels, levels, part);
                    levels[part] = level;
                    next[sum] = new State(loss, levels);
                }
                states = next;
            }
            if (!states.TryGetValue(target, out var result)) return false;
            Array.Copy(result.Levels, destination, result.Levels.Length);
            return true;
        }

        static bool PrefersEarlierParts(int[] prefix, int final, int[] incumbent)
        {
            for (var i = 0; i < prefix.Length; i++)
                if (prefix[i] != incumbent[i]) return prefix[i] > incumbent[i];
            return final > incumbent[prefix.Length];
        }

        public static int[] MatchTargets(int[][] costs, int[] requested)
        {
            var ways = new Dictionary<int, int> { [0] = 1 };
            foreach (var row in costs)
            {
                var next = new Dictionary<int, int>();
                foreach (var entry in ways)
                foreach (var triangles in row)
                {
                    var sum = entry.Key + triangles;
                    next.TryGetValue(sum, out var count);
                    next[sum] = Math.Min(2, count + entry.Value);
                }
                ways = next;
            }
            var eligible = ways.Where(pair => pair.Value >= 2).Select(pair => pair.Key).ToArray();
            if (eligible.Length == 0)
                throw new InvalidOperationException("No exact triangle sum admits multiple assignments.");
            return requested.Select(target => eligible.OrderBy(sum => Math.Abs(sum - target))
                .ThenBy(sum => sum).First()).ToArray();
        }
    }
}
