using System;
using UnityEngine;

namespace Workbench
{
    public enum LodPolicy { ScreenSize, Silhouette, ViewAverage }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PartLods : MonoBehaviour
    {
        [SerializeField] Mesh[] meshes;
        [SerializeField] TextAsset silhouetteTable;

        [Serializable]
        sealed class ViewSample
        {
            public Vector3 direction;
            public float[] errorByLevel;
        }

        [Serializable]
        sealed class Table
        {
            public int schemaVersion;
            public string partId;
            public int referenceLevel;
            public Vector3 referenceCenter;
            public float referenceRadius;
            public int[] triangles;
            public ViewSample[] views;
        }

        Table table;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        float[] meanErrors;

        public string Id => table.partId;
        public int FinestLevel => table.referenceLevel;
        public Mesh MeshAt(int level) => meshes[level];
        public int TrianglesAt(int level) => Costs[level];
        internal int[] Costs => table.triangles;
        internal Vector3 WorldCenter => transform.TransformPoint(table.referenceCenter);
        internal float WorldRadius => table.referenceRadius * Mathf.Abs(transform.lossyScale.x);
        internal bool CanRender => isActiveAndEnabled && meshRenderer.enabled;

        internal void Initialize()
        {
            if (silhouetteTable == null)
                throw new InvalidOperationException($"{name}: assign a silhouette table.");
            table = JsonUtility.FromJson<Table>(silhouetteTable.text);
            if (table == null || table.schemaVersion != 1 || string.IsNullOrEmpty(table.partId) ||
                !(table.referenceRadius > 0) || table.triangles == null || meshes == null ||
                meshes.Length < 2 || table.referenceLevel != meshes.Length - 1 ||
                table.triangles.Length != meshes.Length || table.views == null || table.views.Length == 0)
                throw new InvalidOperationException($"{name}: invalid silhouette table or mesh ladder.");

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            var scale = transform.lossyScale;
            if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > 1e-4f || Mathf.Abs(scale.x - scale.z) > 1e-4f)
                throw new InvalidOperationException($"{name}: use positive uniform scale.");

            for (var level = 0; level < meshes.Length; level++)
            {
                if (meshes[level] == null)
                    throw new InvalidOperationException($"{name}: mesh missing at level {level}.");
                var sum = 0L;
                for (var sub = 0; sub < meshes[level].subMeshCount; sub++)
                {
                    var indices = meshes[level].GetIndexCount(sub);
                    if (meshes[level].GetTopology(sub) != MeshTopology.Triangles || indices % 3 != 0)
                        throw new InvalidOperationException($"{name}: expected triangle topology at level {level}.");
                    sum += indices / 3;
                }
                if (sum <= 0 || sum != Costs[level] || (level > 0 && Costs[level] <= Costs[level - 1]))
                    throw new InvalidOperationException($"{name}: triangle counts differ from the baked ladder.");
            }

            meanErrors = new float[meshes.Length];
            foreach (var sample in table.views)
            {
                if (sample == null || !(Mathf.Abs(sample.direction.sqrMagnitude - 1f) < 1e-4f) ||
                    sample.errorByLevel == null || sample.errorByLevel.Length != meshes.Length ||
                    sample.errorByLevel[table.referenceLevel] != 0)
                    throw new InvalidOperationException($"{name}: malformed view in silhouette table.");
                for (var level = 0; level < meshes.Length; level++)
                {
                    var error = sample.errorByLevel[level];
                    if (!(error >= 0f && error <= 1f))
                        throw new InvalidOperationException($"{name}: invalid silhouette error.");
                    meanErrors[level] += error;
                }
            }
            for (var level = 0; level < meanErrors.Length; level++)
                meanErrors[level] /= table.views.Length;
        }

        internal void FillLoss(float[] destination, LodPolicy policy, Vector3 objectToCamera,
            float relativeScreenHeight, int exponent)
        {
            switch (policy)
            {
                case LodPolicy.ScreenSize:
                    var heightWeight = exponent == 2
                        ? relativeScreenHeight * relativeScreenHeight
                        : relativeScreenHeight;
                    var finest = Costs[table.referenceLevel];
                    for (var i = 0; i < destination.Length; i++)
                        destination[i] = heightWeight * (1f - (float)Costs[i] / finest);
                    break;
                case LodPolicy.ViewAverage:
                    Array.Copy(meanErrors, destination, destination.Length);
                    break;
                case LodPolicy.Silhouette:
                    var best = NearestView(objectToCamera);
                    Array.Copy(table.views[best].errorByLevel, destination, destination.Length);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }

        int NearestView(Vector3 objectToCamera)
        {
            var local = Quaternion.Inverse(transform.rotation) * objectToCamera;
            var bestDot = float.NegativeInfinity;
            var best = 0;
            for (var i = 0; i < table.views.Length; i++)
            {
                var dot = Vector3.Dot(local, table.views[i].direction);
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        public float NearestBakeAngle(Vector3 cameraPosition)
        {
            var direction = cameraPosition - WorldCenter;
            return Vector3.Angle(direction, transform.rotation * table.views[NearestView(direction)].direction);
        }

        internal void ApplyLevel(int level)
        {
            if (meshFilter.sharedMesh != meshes[level])
                meshFilter.sharedMesh = meshes[level];
        }
    }
}
