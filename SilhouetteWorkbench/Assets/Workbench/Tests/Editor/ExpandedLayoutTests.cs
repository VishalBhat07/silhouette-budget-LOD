using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workbench.Tests
{
    public class ExpandedLayoutTests
    {
        [TestCase("InspectionExpanded", false)]
        [TestCase("InspectionClutter", true)]
        public void SavedLayoutUsesEightSharedLaddersAndHasTheIntendedOcclusion(string name, bool clutter)
        {
            var path = $"Assets/Workbench/Scenes/{name}.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.Single(root => root.name == "Inspection Parts").GetComponent<WorkbenchController>();
                var parts = controller.PrepareEvaluation();
                Assert.That(parts.Count, Is.EqualTo(8));
                Assert.That(parts.All(part => part.FinestLevel == 3 &&
                    part.GetComponent<MeshFilter>().sharedMesh == part.MeshAt(3)), Is.True);
                var camera = roots.Single(root => root.CompareTag("MainCamera")).GetComponent<Camera>();
                camera.aspect = 16f / 9f;
                var occluders = roots.Where(root => root.name == "Ground" || root.name == "Workbench")
                    .SelectMany(root => root.GetComponentsInChildren<MeshFilter>()).ToArray();
                var transforms = parts.Select(part => part.transform.localToWorldMatrix)
                    .Concat(occluders.Select(item => item.transform.localToWorldMatrix)).ToArray();
                var meshes = parts.Select(part => part.MeshAt(part.FinestLevel))
                    .Concat(occluders.Select(item => item.sharedMesh)).ToArray();
                var ids = Enumerable.Range(1, parts.Count).Select(i => (byte)i)
                    .Concat(new byte[occluders.Length]).ToArray();
                using var capture = new PerspectiveIdCapture(640, 360);
                var combined = capture.Capture(camera, meshes, transforms, ids);
                var occlusion = parts.Select((part, i) =>
                {
                    var single = capture.Capture(camera, new[] { part.MeshAt(3) },
                        new[] { transforms[i] }, new[] { ids[i] });
                    var alone = single.Count(pixel => pixel == ids[i]);
                    Assert.That(alone, Is.GreaterThan(16), $"{part.Id} was outside the view.");
                    var visible = combined.Count(pixel => pixel == ids[i]);
                    return 1 - (float)visible / alone;
                }).ToArray();
                if (clutter)
                    Assert.That(occlusion.Max(), Is.GreaterThan(.40f), "The clutter pose must actually hide part of an object.");
                else
                    Assert.That(occlusion.Max(), Is.LessThan(.08f), "The separated pose should expose every part.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
