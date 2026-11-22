using NUnit.Framework;
using UnityEngine;

namespace Workbench.Tests
{
    public class PilotReplayTests
    {
        [Test]
        public void AChangeOfObjectIdCountsAgainstBothObjects()
        {
            var reference = new byte[] { 0, 1, 1, 2, 0 };
            var candidate = new byte[] { 0, 2, 1, 0, 2 };
            var result = PartMaskMetrics.Measure(reference, candidate, 2);
            Assert.That(result[0].Error, Is.EqualTo(.5f));
            Assert.That(result[1].Error, Is.EqualTo(1f));
        }

        [Test]
        public void PerspectiveCaptureUsesDepthForIdsAndBlackOccluders()
        {
            var cameraObject = new GameObject("Replay test camera");
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.aspect = 1;
                var mesh = box.GetComponent<MeshFilter>().sharedMesh;
                var matrices = new[]
                {
                    Matrix4x4.Translate(new Vector3(0, 0, 2)),
                    Matrix4x4.Translate(new Vector3(0, 0, 4))
                };
                using var capture = new PerspectiveIdCapture(48, 48);
                byte[] Render(Matrix4x4[] transforms, byte[] ids) =>
                    capture.Capture(camera, new[] { mesh, mesh }, transforms, ids);
                const int center = 24 * 48 + 24;
                Assert.That(Render(matrices, new byte[] { 1, 2 })[center], Is.EqualTo(1));
                Assert.That(Render(new[] { matrices[1], matrices[0] }, new byte[] { 2, 1 })[center], Is.EqualTo(1));
                Assert.That(Render(matrices, new byte[] { 0, 2 })[center], Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(box);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
