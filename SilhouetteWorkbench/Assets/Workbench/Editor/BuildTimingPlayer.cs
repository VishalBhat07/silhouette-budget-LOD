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
        public static void Build()
        {
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var folder = Path.Combine(root, "Results", "standalone");
            Directory.CreateDirectory(folder);
            var oldStats = PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Workbench/Scenes/InspectionExpanded.unity",
                        "Assets/Workbench/Scenes/InspectionClutter.unity"
                    },
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None,
                    locationPathName = Path.Combine(folder, "SilhouetteTiming.app")
                });
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = oldStats;
            }
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Standalone timing build: {report.summary.result}.");
            Debug.Log($"Built non-development timing player: {report.summary.outputPath}");
        }
    }
}
