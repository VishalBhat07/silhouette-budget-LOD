using NUnit.Framework;
using UnityEngine;

namespace Workbench.Tests
{
    public class NativeLodProbeTests
    {
        [Test]
        public void NativeDrawIdentifiesFineAndCoarseMeshesAndRestoresQualitySettings()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var source = new GameObject("Native observation test camera");
            try
            {
                var cube = box.GetComponent<MeshFilter>().sharedMesh;
                var camera = source.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60;
                camera.aspect = 1;
                var bias = QualitySettings.lodBias;
                var maxLevel = QualitySettings.maximumLODLevel;
                using (var probe = new NativeLodProbe(128, 128))
                {
                    probe.AddPart(new[] { cube, cube }, new Vector3(0, 0, 4), Quaternion.identity, Vector3.one);
                    probe.SetThresholds(new[] { .3f, 0f });
                    source.transform.position = new Vector3(0, 0, 2.5f);
                    var close = probe.ReadLevels(camera);
                    source.transform.position = new Vector3(0, 0, -4f);
                    var far = probe.ReadLevels(camera);
                    Assert.That(close, Is.EqualTo(new[] { 1 }));
                    Assert.That(far, Is.EqualTo(new[] { 0 }));
                }
                Assert.That(QualitySettings.lodBias, Is.EqualTo(bias));
                Assert.That(QualitySettings.maximumLODLevel, Is.EqualTo(maxLevel));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(box);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
