using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Workbench
{
    public static class TemporalReplay
    {
        [MenuItem("Workbench/Measure LOD Popping in Active Layout")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            var layout = scene.name == "InspectionExpanded" ? "separated" :
                scene.name == "InspectionClutter" ? "clutter" : "";
            if (Application.isPlaying || scene.isDirty || layout.Length == 0)
                throw new InvalidOperationException("Open and save an expanded scene in Edit Mode before measuring.");
            var config = ReplayMotion.Load();
            var controller = GameObject.Find("Inspection Parts").GetComponent<WorkbenchController>();
            var parts = controller.PrepareEvaluation();
            var occluders = scene.GetRootGameObjects()
                .Where(root => root.name == "Workbench" || root.name == "Ground")
                .SelectMany(root => root.GetComponentsInChildren<MeshFilter>())
                .Where(item => item.sharedMesh != null && item.TryGetComponent<MeshRenderer>(out var r) && r.enabled)
                .ToArray();
            var meshes = new Mesh[parts.Count + occluders.Length];
            var transforms = parts.Select(part => part.transform.localToWorldMatrix)
                .Concat(occluders.Select(item => item.transform.localToWorldMatrix)).ToArray();
            var ids = Enumerable.Range(1, parts.Count).Select(i => checked((byte)i))
                .Concat(new byte[occluders.Length]).ToArray();
            for (var i = 0; i < occluders.Length; i++)
                meshes[parts.Count + i] = occluders[i].sharedMesh;

            var rows = new StringBuilder("trace,frame,policy,spent,switched_parts,changed_id_pixels,weighted_error\n");
            var previous = new int[parts.Count];
            var current = new int[parts.Count];
            using var capture = new PerspectiveIdCapture(config.width, config.height);
            var cameraObject = new GameObject("Temporal replay camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                config.Configure(camera);
                foreach (LodPolicy policy in Enum.GetValues(typeof(LodPolicy)))
                foreach (var trace in config.traces)
                for (var frame = 0; frame < config.framesPerTrace; frame++)
                {
                    config.SetPose(camera, trace, frame);
                    var decision = controller.SelectForPose(camera, policy, config.triangleBudget, current);
                    if (!decision.Feasible || current.Any(level => level < 0))
                        throw new InvalidOperationException($"Invalid temporal pose {trace.id}/{frame}.");
                    var changes = 0;
                    if (frame > 0)
                        for (var i = 0; i < current.Length; i++)
                            if (current[i] != previous[i]) changes++;
                    var score = default(SwitchMeasurement.Result);
                    if (changes != 0)
                    {
                        // Both assignments are drawn at the NEW pose, so camera motion is not part of the metric.
                        for (var i = 0; i < parts.Count; i++) meshes[i] = parts[i].MeshAt(previous[i]);
                        var before = capture.Capture(camera, meshes, transforms, ids);
                        for (var i = 0; i < parts.Count; i++) meshes[i] = parts[i].MeshAt(current[i]);
                        var after = capture.Capture(camera, meshes, transforms, ids);
                        score = SwitchMeasurement.Measure(before, after, parts.Count);
                    }
                    rows.Append(FormattableString.Invariant(
                        $"{trace.id},{frame},{policy},{decision.Spent},{changes},{score.ChangedIdPixels},{score.WeightedError:R}\n"));
                    (previous, current) = (current, previous);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            var folder = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,
                "Results", "temporal-replay", layout);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "transitions.csv"), rows.ToString());
            File.WriteAllText(Path.Combine(folder, "config.json"), ReplayMotion.Source().text);
            File.WriteAllText(Path.Combine(folder, "run.txt"),
                $"{Application.unityVersion}\n{SystemInfo.graphicsDeviceType}\n{parts.Count}\n{controller.ScreenSizeExponent}\n");
            Debug.Log($"Measured same-pose LOD transitions in {layout}: {folder}");
        }

        public static void RunAllBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the Workbench menu for the active layout.");
            foreach (var name in new[] { "InspectionExpanded", "InspectionClutter" })
            {
                EditorSceneManager.OpenScene($"Assets/Workbench/Scenes/{name}.unity", OpenSceneMode.Single);
                Run();
            }
        }
    }
}
