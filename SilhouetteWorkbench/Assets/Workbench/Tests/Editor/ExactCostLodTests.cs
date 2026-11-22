using NUnit.Framework;

namespace Workbench.Tests
{
    public class ExactCostLodTests
    {
        [Test]
        public void CanFindAnExactSolutionWhereGreedyLeavesSlack()
        {
            var costs = new[] { new[] { 1, 4 }, new[] { 1, 3 }, new[] { 1, 3 } };
            var losses = new[] { new[] { .6f, 0f }, new[] { .39f, 0f }, new[] { .39f, 0f } };
            var levels = new int[3];
            var greedy = LodSelector.Select(costs, losses, new[] { true, true, true }, 7, levels);
            Assert.That(greedy.Spent, Is.EqualTo(6));
            var found = ExactCostLod.TrySelect(costs, losses, 7, levels);
            Assert.That(found, Is.True);
            Assert.That(levels, Is.EqualTo(new[] { 0, 1, 1 }));
        }

        [Test]
        public void SkipsCostsWithoutCompetingAssignmentsAndPrefersLowerCostOnEqualDistance()
        {
            var costs = new[] { new[] { 1, 2, 4 }, new[] { 1, 2, 4 } };
            var chosen = ExactCostLod.MatchTargets(costs, new[] { 2, 4, 8 });
            Assert.That(chosen, Is.EqualTo(new[] { 3, 3, 6 }));
        }

        [Test]
        public void UnreachableTargetIsNotReportedAsAnExactMatch()
        {
            var levels = new int[1];
            var found = ExactCostLod.TrySelect(new[] { new[] { 1, 3 } }, new[] { new[] { .6f, 0f } }, 2, levels);
            Assert.That(found, Is.False);
        }

        [Test]
        public void TiePrefersHigherLevelOnFirstPart()
        {
            var costs = new[] { new[] { 1, 2 }, new[] { 1, 2 } };
            var losses = new[] { new[] { .3f, 0f }, new[] { .3f, 0f } };
            var levels = new int[2];
            Assert.That(ExactCostLod.TrySelect(costs, losses, 3, levels), Is.True);
            Assert.That(levels, Is.EqualTo(new[] { 1, 0 }));
        }
    }
}
