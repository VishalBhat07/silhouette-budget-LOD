using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Workbench
{
    public static class SalvageStudy
    {
        const string PoseMenu = "Workbench/Orbital Salvage/Measure Current View";
        const string RouteMenu = "Workbench/Orbital Salvage/Measure Dock Camera Route";
        const string CaptureMenu = "Workbench/Orbital Salvage/Render Policy Comparison";
        const int Height = 360;

        [MenuItem(PoseMenu, true), MenuItem(RouteMenu, true), MenuItem(CaptureMenu, true)]
        static bool Available() => Application.isPlaying && SceneManager.GetActiveScene().name == "OrbitalSalvage";

        [MenuItem(PoseMenu)]
        static void Current() => Measure(false);

        [MenuItem(RouteMenu)]
        static void Route() => Measure(true);

        [MenuItem(CaptureMenu)]
        public static void RenderComparison()
        {
            var camera = Camera.main;
            var controller = UnityEngine.Object.FindAnyObjectByType<WorkbenchController>();
            var parts = controller.PrepareEvaluation();
            var filters = parts.Select(part => part.GetComponent<MeshFilter>()).ToArray();
            var originalMeshes = filters.Select(filter => filter.sharedMesh).ToArray();
            var levels = new int[parts.Count];
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600, 900, 24);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var directory = Path.Combine(ResultsDirectory(), "comparison-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                camera.targetTexture = target;
                using var output = new StreamWriter(Path.Combine(directory, "selection.csv"));
                output.WriteLine("policy,cap,triangles,feasible,part,level");
                foreach (LodPolicy policy in Enum.GetValues(typeof(LodPolicy)))
                {
                    var decision = controller.SelectForPose(camera, policy, controller.TriangleBudget, levels);
                    for (int i = 0; i < parts.Count; i++)
                    {
                        filters[i].sharedMesh = levels[i] < 0 ? originalMeshes[i] : parts[i].MeshAt(levels[i]);
                        output.WriteLine($"{policy},{controller.TriangleBudget},{decision.Spent},{decision.Feasible},{parts[i].Id},{levels[i]}");
                    }
                    Save(policy.ToString());
                }
                for (int i = 0; i < parts.Count; i++)
                    filters[i].sharedMesh = parts[i].MeshAt(parts[i].FinestLevel);
                Save("FullDetail");
            }
            finally
            {
                for (int i = 0; i < filters.Length; i++)
                    filters[i].sharedMesh = originalMeshes[i];
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(pixels);
            }
            Debug.Log($"Orbital Salvage comparison saved to {directory}");

            void Save(string title)
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                File.WriteAllBytes(Path.Combine(directory, title + ".png"), pixels.EncodeToPNG());
            }
        }

        // Each route segment has a fixed gaze target. All policies see identical geometry,
        // including the current NPC, cargo and hatch state, throughout a synchronous capture.
        static readonly (Vector3 start, Vector3 end, Vector3 target)[] Segments =
        {
            (new Vector3(-4, 4, -10), new Vector3(4, 4, -8), new Vector3(1, 4, 10)),
            (new Vector3(-3, 1.7f, 7), new Vector3(-3, 1.7f, 12), new Vector3(-2.8f, 1.9f, 14.4f)),
            (new Vector3(-3, 2.1f, 16), new Vector3(.5f, 2.1f, 17.4f), new Vector3(2, 2.4f, 19.8f))
        };

        static void Measure(bool route)
        {
            var camera = Camera.main;
            var controller = UnityEngine.Object.FindAnyObjectByType<WorkbenchController>();
            var parts = controller.PrepareEvaluation();
            var scene = SceneManager.GetActiveScene();
            var geometry = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(filter => filter.gameObject.scene == scene && filter.sharedMesh &&
                    filter.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled &&
                    (camera.cullingMask & (1 << filter.gameObject.layer)) != 0).ToArray();
            var indices = new int[parts.Count];
            var levels = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                indices[i] = Array.IndexOf(geometry, parts[i].GetComponent<MeshFilter>());
                if (indices[i] < 0) throw new InvalidOperationException($"Missing rendered part {parts[i].name}");
            }

            var directory = ResultsDirectory();
            var filename = (route ? "dock-route-" : "current-view-") + Guid.NewGuid().ToString("N") + ".csv";
            var path = Path.Combine(directory, filename);
            var originalPosition = camera.transform.position;
            var originalRotation = camera.transform.rotation;
            var skinned = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>()
                .Where(renderer => renderer.gameObject.scene == scene && renderer.enabled && renderer.sharedMesh &&
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0).ToArray();
            var poseMeshes = new Mesh[skinned.Length];
            try
            {
                // The same animated actor pose writes depth into every policy's mask.
                for (int i = 0; i < skinned.Length; i++)
                {
                    poseMeshes[i] = new Mesh();
                    skinned[i].BakeMesh(poseMeshes[i]);
                }
                var transforms = geometry.Select(filter => filter.transform.localToWorldMatrix)
                    .Concat(skinned.Select(renderer => renderer.transform.localToWorldMatrix)).ToArray();
                var meshes = geometry.Select(filter => filter.sharedMesh).Concat(poseMeshes).ToArray();
                var ids = new byte[meshes.Length];
                for (int i = 0; i < parts.Count; i++)
                    ids[indices[i]] = checked((byte)(i + 1));
                using var output = new StreamWriter(path);
                using var capture = new PerspectiveIdCapture(Mathf.Max(1, Mathf.RoundToInt(Height * camera.aspect)), Height);
                output.WriteLine("segment,sample,policy,cap,actual_triangles,feasible,part,level,reference_pixels,candidate_pixels,intersection,union,error_1_minus_iou");
                if (!route) Sample(0, 0);
                else for (int segment = 0; segment < Segments.Length; segment++)
                {
                    var leg = Segments[segment];
                    for (int i = 0; i < 12; i++)
                    {
                        camera.transform.position = Vector3.Lerp(leg.start, leg.end, i / 11f);
                        camera.transform.LookAt(leg.target);
                        Sample(segment, i);
                    }
                }

                void Sample(int segment, int sample)
                {
                    for (int i = 0; i < parts.Count; i++) meshes[indices[i]] = parts[i].MeshAt(parts[i].FinestLevel);
                    var reference = capture.Capture(camera, meshes, transforms, ids);
                    foreach (LodPolicy policy in Enum.GetValues(typeof(LodPolicy)))
                    {
                        var decision = controller.SelectForPose(camera, policy, controller.TriangleBudget, levels);
                        for (int i = 0; i < parts.Count; i++)
                            meshes[indices[i]] = levels[i] < 0 ? null : parts[i].MeshAt(levels[i]);
                        var scored = PartMaskMetrics.Measure(reference, capture.Capture(camera, meshes, transforms, ids), parts.Count);
                        for (int i = 0; i < parts.Count; i++)
                        {
                            var c = scored[i];
                            var error = c.Union == 0 ? "" : c.Error.ToString("R", CultureInfo.InvariantCulture);
                            output.WriteLine($"{segment},{sample},{policy},{controller.TriangleBudget},{decision.Spent},{decision.Feasible},{parts[i].Id},{levels[i]},{c.Reference},{c.Candidate},{c.Intersection},{c.Union},{error}");
                        }
                    }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                foreach (var mesh in poseMeshes)
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
            }
            Debug.Log($"Orbital Salvage measurements saved to {path}");
        }

        static string ResultsDirectory()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Results/orbital-study"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
