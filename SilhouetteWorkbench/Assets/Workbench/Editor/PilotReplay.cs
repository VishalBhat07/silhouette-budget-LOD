using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Workbench
{
    public static class PilotReplay
    {
        const string ScenePath = "Assets/Workbench/Scenes/Inspection.unity";

        [Serializable]
        sealed class Trace
        {
            public string id;
            public float firstYaw, lastYaw, firstRadius, lastRadius, firstHeight, lastHeight;
        }

        [Serializable]
        sealed class Config
        {
            public int width, height, samplesPerTrace, minimumReferencePixels;
            public float verticalFieldOfView, targetY;
            public int[] caps;
            public Trace[] traces;
        }

        [Serializable]
        sealed class RunInfo
        {
            public string unityVersion, graphicsApi, sceneDependencyHash, allocator;
            public int screenSizeExponent;
            public int[] effectiveCaps;
        }

        [MenuItem("Workbench/Run Pilot Perspective Replay")]
        public static void Run() => Run(false);

        [MenuItem("Workbench/Run Pilot Exact-Cost Replay")]
        public static void RunExact() => Run(true);

        static void Run(bool exact)
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save Inspection in Edit Mode before running the replay.");

            var controller = GameObject.Find("Pilot Parts").GetComponent<WorkbenchController>();
            var parts = controller.PrepareEvaluation();
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var configText = File.ReadAllText(Path.Combine(root, "Experiments/pilot-replay-config.json"));
            var config = JsonUtility.FromJson<Config>(configText);
            if (config.samplesPerTrace < 2 || config.minimumReferencePixels < 1 ||
                config.traces == null || config.traces.Length == 0 || config.caps == null || config.caps.Length == 0)
                throw new InvalidOperationException("Invalid replay trace configuration.");

            var costs = parts.Select(part => Enumerable.Range(0, part.FinestLevel + 1)
                .Select(part.TrianglesAt).ToArray()).ToArray();
            var effectiveCaps = (exact ? ExactCostLod.MatchTargets(costs, config.caps) : config.caps)
                .Distinct().ToArray();
            var scoreBuffer = costs.Select(row => new float[row.Length]).ToArray();
            var visibility = new bool[parts.Count];

            var occluders = new[] { GameObject.Find("Ground"), GameObject.Find("Workbench") }
                .SelectMany(go => go.GetComponentsInChildren<MeshFilter>())
                .Where(filter => filter.sharedMesh != null && filter.TryGetComponent<MeshRenderer>(out var r) && r.enabled)
                .ToArray();
            var meshes = new Mesh[parts.Count + occluders.Length];
            var transforms = new Matrix4x4[meshes.Length];
            var ids = new byte[meshes.Length];
            for (var part = 0; part < parts.Count; part++)
            {
                transforms[part] = parts[part].transform.localToWorldMatrix;
                ids[part] = checked((byte)(part + 1));
            }
            for (var i = 0; i < occluders.Length; i++)
            {
                meshes[parts.Count + i] = occluders[i].sharedMesh;
                transforms[parts.Count + i] = occluders[i].transform.localToWorldMatrix;
            }

            var observations = new StringBuilder("trace,frame,cap,policy,spent,part_id,level,triangles," +
                "reference_pixels,candidate_pixels,intersection_pixels,union_pixels,nearest_bake_angle_deg\n");
            var poses = new StringBuilder("trace,frame,px,py,pz,qx,qy,qz,qw,vertical_fov_deg\n");
            var levels = new int[parts.Count];
            var policies = (LodPolicy[])Enum.GetValues(typeof(LodPolicy));
            using var capture = new PerspectiveIdCapture(config.width, config.height);
            var cameraObject = new GameObject("Pilot replay camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = config.verticalFieldOfView;
                camera.aspect = (float)config.width / config.height;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 50f;
                foreach (var trace in config.traces)
                for (var frame = 0; frame < config.samplesPerTrace; frame++)
                {
                    var t = (float)frame / (config.samplesPerTrace - 1);
                    var yaw = Mathf.Lerp(trace.firstYaw, trace.lastYaw, t) * Mathf.Deg2Rad;
                    var radius = Mathf.Lerp(trace.firstRadius, trace.lastRadius, t);
                    var position = new Vector3(radius * Mathf.Sin(yaw),
                        Mathf.Lerp(trace.firstHeight, trace.lastHeight, t), -radius * Mathf.Cos(yaw));
                    var rotation = Quaternion.LookRotation(new Vector3(0, config.targetY, 0) - position);
                    camera.transform.SetPositionAndRotation(position, rotation);
                    poses.Append(FormattableString.Invariant(
                        $"{trace.id},{frame},{position.x:R},{position.y:R},{position.z:R},{rotation.x:R},{rotation.y:R},{rotation.z:R},{rotation.w:R},{camera.fieldOfView:R}\n"));

                    for (var part = 0; part < parts.Count; part++)
                        meshes[part] = parts[part].MeshAt(parts[part].FinestLevel);
                    var reference = capture.Capture(camera, meshes, transforms, ids);
                    foreach (var cap in effectiveCaps)
                    foreach (var policy in policies)
                    {
                        int spent;
                        if (exact)
                        {
                            controller.ScorePose(camera, policy, scoreBuffer, visibility);
                            if (visibility.Any(visible => !visible) ||
                                !ExactCostLod.TrySelect(costs, scoreBuffer, cap, levels))
                                throw new InvalidOperationException($"Cannot match exact cost at {trace.id}/{frame}.");
                            spent = costs.Select((row, part) => row[levels[part]]).Sum();
                            if (spent != cap)
                                throw new InvalidOperationException($"Incorrect exact triangle cost at {trace.id}/{frame}.");
                        }
                        else
                        {
                            var decision = controller.SelectForPose(camera, policy, cap, levels);
                            if (!decision.Feasible || decision.Spent > cap || levels.Any(level => level < 0))
                                throw new InvalidOperationException($"Infeasible or out-of-frustum pilot part at {trace.id}/{frame}.");
                            spent = decision.Spent;
                        }
                        for (var part = 0; part < parts.Count; part++)
                            meshes[part] = parts[part].MeshAt(levels[part]);
                        var actual = capture.Capture(camera, meshes, transforms, ids);
                        var metrics = PartMaskMetrics.Measure(reference, actual, parts.Count);
                        for (var part = 0; part < parts.Count; part++)
                        {
                            var m = metrics[part];
                            var angle = parts[part].NearestBakeAngle(position);
                            if (m.Reference < config.minimumReferencePixels || angle < .05f)
                                throw new InvalidOperationException(
                                    $"Reference part too small or not held out: {trace.id}/{frame}/{parts[part].Id}.");
                            observations.Append(FormattableString.Invariant(
                                $"{trace.id},{frame},{cap},{policy},{spent},{parts[part].Id},{levels[part]},{parts[part].TrianglesAt(levels[part])},{m.Reference},{m.Candidate},{m.Intersection},{m.Union},{angle:F4}\n"));
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            var folder = Path.Combine(root, "Results", exact ? "pilot-exact-replay" : "pilot-replay");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "observations.csv"), observations.ToString());
            File.WriteAllText(Path.Combine(folder, "poses.csv"), poses.ToString());
            File.WriteAllText(Path.Combine(folder, "config.json"), configText);
            File.WriteAllText(Path.Combine(folder, "run.json"), JsonUtility.ToJson(new RunInfo
            {
                unityVersion = Application.unityVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                sceneDependencyHash = AssetDatabase.GetAssetDependencyHash(scene.path).ToString(),
                screenSizeExponent = controller.ScreenSizeExponent,
                allocator = exact ? "exact-cost-dp" : "greedy",
                effectiveCaps = effectiveCaps
            }, true) + "\n");
            Debug.Log($"Pilot {(exact ? "exact-cost" : "perspective")} replay: " +
                $"{config.traces.Length * config.samplesPerTrace} poses → {folder}");
        }
    }
}
