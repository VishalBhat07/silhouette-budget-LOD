using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Workbench
{
    [DisallowMultipleComponent]
    public sealed class WorkbenchController : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] PartLods[] parts;
        [SerializeField, Min(0)] int triangleBudget = 960;
        [SerializeField] LodPolicy policy = LodPolicy.Silhouette;
        [SerializeField, Range(1, 2)] int screenSizeExponent = 2;

        readonly Plane[] frustum = new Plane[6];
        PartLods[] ordered;
        int[][] costs;
        float[][] losses;
        bool[] active;
        int[] levels;
        bool warnedInfeasible;

        public LodSelector.Decision CurrentDecision { get; private set; }
        public int ScreenSizeExponent => screenSizeExponent;

        void Awake() => Initialize();

        void Initialize()
        {
            if (viewCamera == null || parts == null || parts.Length == 0)
                throw new InvalidOperationException("Assign the inspection camera and LOD parts.");

            ordered = (PartLods[])parts.Clone();
            foreach (var part in ordered)
            {
                if (part == null) throw new InvalidOperationException("A workbench part is missing.");
                part.Initialize();
            }
            Array.Sort(ordered, (a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            costs = new int[ordered.Length][];
            losses = new float[ordered.Length][];
            active = new bool[ordered.Length];
            levels = new int[ordered.Length];
            for (var i = 0; i < ordered.Length; i++)
            {
                if (i > 0 && ordered[i].Id == ordered[i - 1].Id)
                    throw new InvalidOperationException($"Duplicate part ID: {ordered[i].Id}.");
                costs[i] = ordered[i].Costs;
                losses[i] = new float[costs[i].Length];
            }
        }

        public IReadOnlyList<PartLods> PrepareEvaluation()
        {
            Initialize();
            return ordered;
        }

        // Scores a camera pose without swapping the rendered meshes.
        public LodSelector.Decision SelectForPose(Camera camera, LodPolicy selected, int cap, int[] selection)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            var cameraTransform = camera.transform;
            var eye = cameraTransform.position;
            var forward = cameraTransform.forward;
            var projectionScale = Mathf.Abs(camera.projectionMatrix.m11);
            for (var i = 0; i < ordered.Length; i++)
            {
                var part = ordered[i];
                var center = part.WorldCenter;
                var radius = part.WorldRadius;
                active[i] = part.CanRender &&
                    GeometryUtility.TestPlanesAABB(frustum, new Bounds(center, Vector3.one * (2 * radius)));
                if (!active[i]) continue;
                var objectToCamera = eye - center;
                var eyeDepth = Mathf.Max(camera.nearClipPlane, Vector3.Dot(-objectToCamera, forward));
                var height = Mathf.Clamp01(radius * projectionScale / (camera.orthographic ? 1f : eyeDepth));
                part.FillLoss(losses[i], selected, objectToCamera, height, screenSizeExponent);
            }
            return LodSelector.Select(costs, losses, active, cap, selection);
        }

        void LateUpdate()
        {
            CurrentDecision = SelectForPose(viewCamera, policy, triangleBudget, levels);
            for (var i = 0; i < ordered.Length; i++)
                if (levels[i] >= 0) ordered[i].ApplyLevel(levels[i]);

            if (!CurrentDecision.Feasible && !warnedInfeasible)
                Debug.LogWarning($"Triangle cap {triangleBudget} is infeasible; coarsest visible levels cost " +
                    $"{CurrentDecision.Spent}. This frame is not budget-compliant.", this);
            warnedInfeasible = !CurrentDecision.Feasible;
        }

        public string CompareAtCurrentPose()
        {
            Initialize();
            var output = new StringBuilder();
            output.AppendLine($"Shared workbench cap: {triangleBudget} triangles");
            foreach (LodPolicy item in Enum.GetValues(typeof(LodPolicy)))
            {
                var decision = SelectForPose(viewCamera, item, triangleBudget, levels);
                output.Append($"{item}: {decision.Spent} triangles");
                if (!decision.Feasible) output.Append(" (infeasible fallback)");
                for (var i = 0; i < ordered.Length; i++)
                    if (levels[i] >= 0) output.Append($"  {ordered[i].Id}=L{levels[i]}");
                output.AppendLine();
            }
            return output.ToString().TrimEnd();
        }

        [ContextMenu("Compare Policies At Current Pose")]
        void LogComparison() => Debug.Log(CompareAtCurrentPose(), this);
    }
}
