using System;
using NUnit.Framework;
using Workbench.Demo;

namespace Workbench.Tests
{
    public class DemoStateTests
    {
        [Test]
        public void APolicyChangeLocksThePoseForComparison()
        {
            var state = new DemoState(121, 2506, 18080, 6400);
            state.Scrub(37);
            state.TogglePlayback();
            state.ChoosePolicy(LodPolicy.ScreenSize);
            Assert.That(state.Frame, Is.EqualTo(37));
            Assert.That(state.Playing, Is.False);
            Assert.That(state.Policy, Is.EqualTo(LodPolicy.ScreenSize));
        }

        [Test]
        public void APerformanceRunNeverStartsTheDemo()
        {
            Assert.That(DemoState.ShouldStart("InspectionExpanded", new[] { "--workbench-timing", "some-path" }), Is.False);
            Assert.That(DemoState.ShouldStart("InspectionClutter", Array.Empty<string>()), Is.True);
            Assert.That(DemoState.ShouldStart("Inspection", Array.Empty<string>()), Is.False);
        }

        [Test]
        public void BudgetAndTimelineStayInRange()
        {
            var state = new DemoState(121, 2506, 18080, 6400);
            state.SetCap(2499);
            state.Scrub(150);
            Assert.That(state.Cap, Is.EqualTo(2506));
            Assert.That(state.Frame, Is.EqualTo(120));
            state.Advance(1);
            Assert.That(state.Frame, Is.Zero);
        }
    }
}
