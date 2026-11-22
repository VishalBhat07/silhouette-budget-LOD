using NUnit.Framework;

namespace Workbench.Tests
{
    public class LodSelectorTests
    {
        static (bool feasible, int spent, int[] levels) Run(int[][] costs, float[][] losses,
            bool[] active, int cap)
        {
            var levels = new int[costs.Length];
            var decision = LodSelector.Select(costs, losses, active, cap, levels);
            return (decision.Feasible, decision.Spent, levels);
        }

        [Test]
        public void ChoosesHighestGainPerTriangleWithoutOverspending()
        {
            var choice = Run(new[] { new[] { 10, 20, 40 }, new[] { 10, 20, 40 } },
                new[] { new[] { .6f, .2f, 0f }, new[] { .4f, .35f, 0f } }, new[] { true, true }, 30);
            Assert.That(choice.feasible, Is.True);
            Assert.That(choice.spent, Is.EqualTo(30));
            Assert.That(choice.levels, Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void FlagsImpossibleCapAndReportsCostOfCoarsestFallback()
        {
            var choice = Run(new[] { new[] { 10, 20 }, new[] { 10, 20 } },
                new[] { new[] { .3f, 0f }, new[] { .2f, 0f } }, new[] { true, true }, 19);
            Assert.That(choice.feasible, Is.False);
            Assert.That(choice.spent, Is.EqualTo(20));
            Assert.That(choice.levels, Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void CanSkipAWorseIntermediateLevel()
        {
            var choice = Run(new[] { new[] { 10, 20, 30 } },
                new[] { new[] { .3f, .4f, 0f } }, new[] { true }, 30);
            Assert.That(choice.levels, Is.EqualTo(new[] { 2 }));
            Assert.That(choice.spent, Is.EqualTo(30));
        }

        [Test]
        public void EqualGainPerTrianglePrefersFirstPart()
        {
            var choice = Run(new[] { new[] { 10, 20, 30 }, new[] { 10, 20, 30 } },
                new[] { new[] { 1f, .5f, 0f }, new[] { 1f, .5f, 0f } }, new[] { true, true }, 40);
            Assert.That(choice.levels, Is.EqualTo(new[] { 2, 0 }));
        }

        [Test]
        public void ExcludesInactivePartsAndLeavesUnhelpfulBudgetUnspent()
        {
            var choice = Run(new[] { new[] { 10, 20 }, new[] { 10, 20 } },
                new[] { new[] { .5f, 0f }, new[] { 0f, 0f } }, new[] { false, true }, 25);
            Assert.That(choice.levels, Is.EqualTo(new[] { -1, 0 }));
            Assert.That(choice.spent, Is.EqualTo(10));
        }

        [Test]
        public void EmptyActiveSetIsFeasibleAtZeroCap()
        {
            var choice = Run(new[] { new[] { 10 } }, new[] { new[] { 0f } }, new[] { false }, 0);
            Assert.That(choice.feasible, Is.True);
            Assert.That(choice.spent, Is.Zero);
            Assert.That(choice.levels, Is.EqualTo(new[] { -1 }));
        }
    }
}
