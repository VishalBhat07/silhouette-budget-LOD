using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Workbench
{
    public sealed class StandaloneTiming : MonoBehaviour
    {
        const string Flag = "--workbench-timing";
        static readonly string[] Scenes = { "InspectionExpanded", "InspectionClutter" };
        static readonly string[] Layouts = { "separated", "clutter" };
        static readonly LodPolicy[] Policies = { LodPolicy.ScreenSize, LodPolicy.Silhouette, LodPolicy.ViewAverage };
        static readonly double MillisecondsPerTick = 1000d / Stopwatch.Frequency;
        string output;

        struct Sample
        {
            public int repeat, layout, frame, spent;
            public LodPolicy policy;
            public string trace;
            public double selectorMs, swapMs;
            public float frameMs;
        }

        struct GpuSample
        {
            public int repeat, layout;
            public LodPolicy policy;
            public double ms;
        }

        [Serializable]
        sealed class Metadata
        {
            public string unity, os, cpu, gpu, graphicsApi, device;
            public int width, height, qualityLevel, vSync, targetFrameRate;
            public bool development, frameTimingEnabled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, Flag);
            if (Application.isEditor || index < 0) return;
            if (index + 1 >= args.Length)
            {
                Debug.LogError($"{Flag} needs an output directory.");
                Application.Quit(2);
                return;
            }
            var obj = new GameObject("Standalone timing runner");
            DontDestroyOnLoad(obj);
            obj.AddComponent<StandaloneTiming>().output = Path.GetFullPath(args[index + 1]);
        }

        IEnumerator Start()
        {
            var config = ReplayMotion.Load();
            if (Debug.isDebugBuild)
            {
                Debug.LogError("Timing requires a non-development player.");
                Application.Quit(2);
                yield break;
            }
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(config.width, config.height, FullScreenMode.Windowed);
            for (var attempt = 0; attempt < 120 &&
                (Screen.width != config.width || Screen.height != config.height); attempt++)
                yield return null;
            if (Screen.width != config.width || Screen.height != config.height)
            {
                Debug.LogError($"Required {config.width}×{config.height}, got {Screen.width}×{Screen.height}.");
                Application.Quit(2);
                yield break;
            }

            var capacity = config.repeats * Scenes.Length * Policies.Length * config.traces.Length * config.framesPerTrace;
            var samples = new List<Sample>(capacity);
            var gpu = new List<GpuSample>(capacity);
            var timings = new FrameTiming[1];
            var gpuEnabled = FrameTimingManager.IsFeatureEnabled();
            ulong lastTimestamp = 0;
            for (var repeat = 0; repeat < config.repeats; repeat++)
            for (var order = 0; order < Scenes.Length; order++)
            {
                var layout = (repeat + order) % Scenes.Length;
                if (SceneManager.GetActiveScene().name != Scenes[layout])
                    yield return SceneManager.LoadSceneAsync(Scenes[layout]);
                var controller = GameObject.Find("Inspection Parts").GetComponent<WorkbenchController>();
                controller.enabled = false;
                var selection = new int[controller.PrepareEvaluation().Count];
                var camera = Camera.main;
                config.Configure(camera);

                for (var slot = 0; slot < Policies.Length; slot++)
                {
                    var policy = Policies[(slot + repeat + layout) % Policies.Length];
                    for (var frame = 0; frame < config.warmupFrames; frame++)
                    {
                        config.SetPose(camera, config.traces[0], frame % config.framesPerTrace);
                        controller.SelectForPose(camera, policy, config.triangleBudget, selection);
                        controller.ApplySelection(selection);
                        if (gpuEnabled) FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                    }

                    foreach (var trace in config.traces)
                    for (var frame = 0; frame < config.framesPerTrace; frame++)
                    {
                        config.SetPose(camera, trace, frame);
                        var start = Stopwatch.GetTimestamp();
                        var decision = controller.SelectForPose(camera, policy, config.triangleBudget, selection);
                        var selected = Stopwatch.GetTimestamp();
                        controller.ApplySelection(selection);
                        var swapped = Stopwatch.GetTimestamp();
                        if (!decision.Feasible || decision.Spent > config.triangleBudget)
                        {
                            Debug.LogError("An infeasible timing frame invalidated the run.");
                            Application.Quit(2);
                            yield break;
                        }
                        if (gpuEnabled) FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        samples.Add(new Sample
                        {
                            repeat = repeat, layout = layout, policy = policy, trace = trace.id, frame = frame,
                            spent = decision.Spent, selectorMs = (selected - start) * MillisecondsPerTick,
                            swapMs = (swapped - selected) * MillisecondsPerTick, frameMs = Time.unscaledDeltaTime * 1000
                        });
                        // Frame timing is delivered asynchronously; identify unique samples, do not label them as poses.
                        if (gpuEnabled && FrameTimingManager.GetLatestTimings(1, timings) != 0 &&
                            timings[0].frameStartTimestamp > lastTimestamp && timings[0].gpuFrameTime > 0)
                        {
                            lastTimestamp = timings[0].frameStartTimestamp;
                            gpu.Add(new GpuSample { repeat = repeat, layout = layout, policy = policy, ms = timings[0].gpuFrameTime });
                        }
                    }
                }
            }

            var rows = new StringBuilder("repeat,layout,policy,trace,frame,spent,selector_ms,swap_ms,frame_ms\n");
            foreach (var row in samples)
                rows.Append(FormattableString.Invariant(
                    $"{row.repeat},{Layouts[row.layout]},{row.policy},{row.trace},{row.frame},{row.spent},{row.selectorMs:R},{row.swapMs:R},{row.frameMs:R}\n"));
            var gpuRows = new StringBuilder("repeat,layout,policy,gpu_ms\n");
            foreach (var row in gpu)
                gpuRows.Append(FormattableString.Invariant($"{row.repeat},{Layouts[row.layout]},{row.policy},{row.ms:R}\n"));
            File.WriteAllText(Path.Combine(output, "frames.csv"), rows.ToString());
            File.WriteAllText(Path.Combine(output, "gpu.csv"), gpuRows.ToString());
            File.WriteAllText(Path.Combine(output, "config.json"), ReplayMotion.Source().text);
            File.WriteAllText(Path.Combine(output, "environment.json"), JsonUtility.ToJson(new Metadata
            {
                unity = Application.unityVersion, os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                device = SystemInfo.deviceModel, width = Screen.width, height = Screen.height,
                vSync = QualitySettings.vSyncCount, qualityLevel = QualitySettings.GetQualityLevel(),
                targetFrameRate = Application.targetFrameRate, development = Debug.isDebugBuild, frameTimingEnabled = gpuEnabled
            }, true) + "\n");
            Debug.Log($"Timing complete: {samples.Count} render frames, {gpu.Count} valid GPU timing samples.");
            Application.Quit();
        }
    }
}
