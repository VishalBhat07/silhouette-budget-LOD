using System;
using UnityEngine;

namespace Workbench.Demo
{
    public sealed class DemoState
    {
        readonly int frameCount;
        public int Minimum { get; }
        public int Maximum { get; }
        public int Cap { get; private set; }
        public int Frame { get; private set; }
        public bool Playing { get; private set; }
        public LodPolicy Policy { get; private set; } = LodPolicy.Silhouette;

        public DemoState(int frameCount, int min, int max, int initial)
        {
            this.frameCount = frameCount;
            Minimum = min;
            Maximum = max;
            SetCap(initial);
        }

        public static bool ShouldStart(string scene, string[] args) =>
            (scene == "InspectionExpanded" || scene == "InspectionClutter") &&
            Array.IndexOf(args, "--workbench-timing") < 0;

        public void ChoosePolicy(LodPolicy policy)
        {
            Policy = policy;
            Playing = false;
        }

        public void SetCap(int value)
        {
            Cap = Mathf.Clamp(value, Minimum, Maximum);
            Playing = false;
        }

        public void Scrub(int frame)
        {
            Frame = Mathf.Clamp(frame, 0, frameCount - 1);
            Playing = false;
        }

        public void TogglePlayback() => Playing = !Playing;
        public void Advance(int count) => Frame = (Frame + count) % frameCount;
    }
}
