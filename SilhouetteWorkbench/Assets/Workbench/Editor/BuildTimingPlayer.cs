using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Workbench
{
    public static class BuildTimingPlayer
    {
        [MenuItem("Workbench/Build macOS Timing Player")]
        public static void Build() => BuildPlayer("standalone", "SilhouetteTiming.app", true);

        [MenuItem("Workbench/Build macOS Interactive Demo")]
        public static void BuildDemo() => BuildPlayer("demo", "SilhouetteWorkbench.app", false);

        static void BuildPlayer(string destination, string product, bool timing)
        {
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var folder = Path.Combine(root, "Results", destination);
            Directory.CreateDirectory(folder);
            var oldStats = PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                PlayerSettings.enableFrameTimingStats = timing;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Workbench/Scenes/InspectionExpanded.unity",
                        "Assets/Workbench/Scenes/InspectionClutter.unity"
                    },
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None,
                    locationPathName = Path.Combine(folder, product)
                });
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = oldStats;
            }
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Standalone build: {report.summary.result}.");
            Debug.Log($"Built non-development {(timing ? "timing player" : "interactive demo")}: {report.summary.outputPath}");
        }
    }
}
