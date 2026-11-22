using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Workbench.Tests
{
    public class SilhouetteBakerTests
    {
        [Test]
        public void IdenticalMasksHaveZeroLoss()
        {
            var mask = new byte[] { 0, 255, 255, 0 };
            Assert.That(SilhouetteBaker.MaskLoss(mask, mask), Is.Zero);
        }

        [Test]
        public void InteriorHoleIsCountedAsMissingForeground()
        {
            var reference = Enumerable.Repeat((byte)255, 100).ToArray();
            var candidate = (byte[])reference.Clone();
            candidate[55] = 0;
            Assert.That(SilhouetteBaker.MaskLoss(reference, candidate), Is.EqualTo(.01f).Within(1e-6f));
        }

        [Test]
        public void ExtraAndMissingPixelsBothContribute()
        {
            var reference = new byte[] { 255, 255, 0, 0 };
            var candidate = new byte[] { 0, 255, 255, 0 };
            Assert.That(SilhouetteBaker.MaskLoss(reference, candidate), Is.EqualTo(2f / 3f).Within(1e-6f));
        }

        [Test]
        public void EmptyUnionIsNotReportedAsAPerfectMatch()
        {
            var empty = new byte[] { 0 };
            Assert.Throws<ArgumentException>(() => SilhouetteBaker.MaskLoss(empty, empty));
        }

        [Test]
        public void SampleDirectionsAreDeterministicDistinctAndNormalized()
        {
            var directions = SilhouetteBaker.SampleDirections(32);
            Assert.That(directions, Has.Length.EqualTo(32));
            Assert.That(directions.Distinct().Count(), Is.EqualTo(32));
            Assert.That(directions.All(d => Mathf.Abs(d.sqrMagnitude - 1) < 1e-5f), Is.True);
            Assert.That(SilhouetteBaker.SampleDirections(32), Is.EqualTo(directions));
        }

        [Test]
        public void RenderedWasherHasAnOpenBoreAndAnUnclippedBinaryMask()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath("Assets/Workbench/Parts/washer/washer_level3.obj")
                .OfType<Mesh>().Single();
            var mask = SilhouetteBaker.RenderMask(mesh, mesh.bounds, Vector3.up, 128);
            Assert.That(mask, Has.Length.EqualTo(128 * 128));
            Assert.That(mask.All(pixel => pixel == 0 || pixel == 255), Is.True);
            Assert.That(mask[64 * 128 + 64], Is.Zero, "The bore must remain empty.");
            Assert.That(mask[64 * 128 + 96], Is.EqualTo(255), "The ring must be visible.");
            Assert.That(mask.Take(128).All(pixel => pixel == 0), Is.True);
            Assert.That(SilhouetteBaker.RenderMask(mesh, mesh.bounds, Vector3.up, 128), Is.EqualTo(mask));
        }
    }
}
