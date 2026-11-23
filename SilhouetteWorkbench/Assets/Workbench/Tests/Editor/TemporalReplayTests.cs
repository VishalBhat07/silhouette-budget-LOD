using NUnit.Framework;

namespace Workbench.Tests
{
    public class TemporalReplayTests
    {
        [Test]
        public void IdenticalMeshesAtTheNewPoseHaveNoPopping()
        {
            var ids = new byte[] { 0, 1, 1, 2 };
            var result = SwitchMeasurement.Measure(ids, ids, 2);
            Assert.That(result.ChangedIdPixels, Is.Zero);
            Assert.That(result.WeightedError, Is.Zero);
        }

        [Test]
        public void AChangedIdCountsOnceAsAPixelButAffectsTwoObjectMasks()
        {
            var before = new byte[] { 0, 1, 1, 2, 0 };
            var after = new byte[] { 0, 2, 1, 0, 2 };
            var result = SwitchMeasurement.Measure(before, after, 2);
            Assert.That(result.ChangedIdPixels, Is.EqualTo(3));
            Assert.That(result.WeightedError, Is.EqualTo(.8f).Within(1e-6f));
        }
    }
}
