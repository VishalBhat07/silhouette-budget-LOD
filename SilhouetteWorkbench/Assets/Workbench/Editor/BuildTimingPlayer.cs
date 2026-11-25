using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Workbench
{
    public static class BuildTimingPlayer
    {
        [MenuItem("Workbench/Build Linux Orbital Salvage")]
        public static void BuildSalvage()
        {
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var folder = Path.Combine(root, "Results", "orbital-salvage");
            Directory.CreateDirectory(folder);
            string previousName = PlayerSettings.productName;
            int previousWidth = PlayerSettings.defaultScreenWidth;
            int previousHeight = PlayerSettings.defaultScreenHeight;
            var previousMode = PlayerSettings.fullScreenMode;
            bool previousNativeResolution = PlayerSettings.defaultIsNativeResolution;
            bool previousResizable = PlayerSettings.resizableWindow;
            BuildReport report;
            try
            {
                PlayerSettings.productName = "Orbital Salvage";
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = false;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Workbench/Scenes/OrbitalSalvage.unity", FlightScene.Path },
                    target = BuildTarget.StandaloneLinux64,
                    locationPathName = Path.Combine(folder, "OrbitalSalvage.x86_64")
                });
            }
            finally
            {
                PlayerSettings.productName = previousName;
                PlayerSettings.defaultScreenWidth = previousWidth;
                PlayerSettings.defaultScreenHeight = previousHeight;
                PlayerSettings.fullScreenMode = previousMode;
                PlayerSettings.defaultIsNativeResolution = previousNativeResolution;
                PlayerSettings.resizableWindow = previousResizable;
            }
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Orbital Salvage build: {report.summary.result}.");
            Debug.Log($"Built Orbital Salvage: {report.summary.outputPath}");
        }

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
