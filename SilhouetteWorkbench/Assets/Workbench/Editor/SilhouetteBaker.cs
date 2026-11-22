using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Workbench
{
    public static class SilhouetteBaker
    {
        const int Resolution = 256;
        const int ViewCount = 32;
        const int LevelCount = 4;
        const int ReferenceLevel = LevelCount - 1;
        const string PartsFolder = "Assets/Workbench/Parts";
        const string MenuPath = "Workbench/Bake Pilot Silhouettes";
        static readonly string[] Parts = { "gear", "washer", "toolbox" };

        [Serializable]
        sealed class ViewSample
        {
            public Vector3 direction;
            public float[] errorByLevel;
        }

        [Serializable]
        sealed class PartTable
        {
            public int schemaVersion = 1;
            public string partId;
            public string unityVersion;
            public string graphicsApi;
            public int resolution;
            public int referenceLevel;
            public Vector3 referenceCenter;
            public float referenceRadius;
            public string bakeSettingsHash;
            public string[] meshPaths;
            public string[] sourceSha256;
            public string[] importDependencyHashes;
            public int[] triangles;
            public ViewSample[] views;
        }

        public static Vector3[] SampleDirections(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var directions = new Vector3[count];
            var goldenAngle = Math.PI * (3 - Math.Sqrt(5));
            for (var i = 0; i < count; i++)
            {
                var y = 1 - 2 * (i + .5) / count;
                var radial = Math.Sqrt(1 - y * y);
                var phi = i * goldenAngle;
                directions[i] = new Vector3((float)(radial * Math.Cos(phi)), (float)y,
                    (float)(radial * Math.Sin(phi)));
            }
            return directions;
        }

        public static float MaskLoss(byte[] reference, byte[] candidate)
        {
            if (reference.Length != candidate.Length)
                throw new ArgumentException("Masks must have the same number of pixels.");
            var difference = 0;
            var union = 0;
            for (var i = 0; i < reference.Length; i++)
            {
                var a = reference[i] != 0;
                var b = candidate[i] != 0;
                if (a || b) union++;
                if (a != b) difference++;
            }
            if (union == 0) throw new ArgumentException("Two empty masks have no defined IoU.");
            return (float)difference / union;
        }

        public static byte[] RenderMask(Mesh mesh, Bounds reference, Vector3 direction, int resolution)
        {
            using var renderer = new IsolatedMaskRenderer(resolution);
            return renderer.Capture(mesh, reference, direction);
        }

        [MenuItem(MenuPath)]
        public static void BakePilot()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var unityRoot = Directory.GetParent(Application.dataPath).FullName;
            var reportFolder = Path.Combine(unityRoot, "..", "Results", "pilot-silhouettes");
            var directions = SampleDirections(ViewCount);
            var config = string.Join("|", Resolution, ViewCount, ReferenceLevel, Application.unityVersion,
                SystemInfo.graphicsDeviceType,
                Sha256(File.ReadAllBytes(Path.Combine(unityRoot, IsolatedMaskRenderer.ShaderPath))),
                Sha256(File.ReadAllBytes(Path.Combine(unityRoot, "Assets/Workbench/Editor/IsolatedMaskRenderer.cs"))),
                Sha256(File.ReadAllBytes(Path.Combine(unityRoot, "Assets/Workbench/Editor/SilhouetteBaker.cs"))));
            var settingsHash = Sha256(Encoding.UTF8.GetBytes(config));
            using var renderer = new IsolatedMaskRenderer(Resolution);
            Directory.CreateDirectory(reportFolder);
            foreach (var partId in Parts)
            {
                var table = BakePart(renderer, unityRoot, partId, directions, settingsHash, reportFolder);
                var path = Path.Combine(unityRoot, PartsFolder, partId, "silhouettes.json");
                File.WriteAllText(path, JsonUtility.ToJson(table, true) + "\n");
            }
            AssetDatabase.Refresh();
            Debug.Log($"Baked {Parts.Length} parts × {ViewCount} views at {Resolution}px in " +
                $"{timer.Elapsed.TotalSeconds:F2}s. Diagnostic sheets: {Path.GetFullPath(reportFolder)}");
        }

        static PartTable BakePart(IsolatedMaskRenderer renderer, string unityRoot, string partId,
            Vector3[] directions, string settingsHash, string reportFolder)
        {
            var paths = Enumerable.Range(0, LevelCount)
                .Select(level => $"{PartsFolder}/{partId}/{partId}_level{level}.obj").ToArray();
            var meshes = paths.Select(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single()).ToArray();
            var triangles = meshes.Select(TriangleCount).ToArray();
            for (var level = 1; level < LevelCount; level++)
                if (triangles[level] <= triangles[level - 1])
                    throw new InvalidOperationException($"{partId}: mesh costs must increase at every level.");

            var bounds = meshes[ReferenceLevel].bounds;
            var views = new ViewSample[directions.Length];
            for (var view = 0; view < directions.Length; view++)
            {
                var direction = directions[view];
                var reference = renderer.Capture(meshes[ReferenceLevel], bounds, direction);
                ValidateMask(reference, true, $"{partId} reference view {view}");
                var errors = new float[LevelCount];
                for (var level = 0; level < LevelCount; level++)
                {
                    var candidate = renderer.Capture(meshes[level], bounds, direction);
                    ValidateMask(candidate, false, $"{partId} level {level} view {view}");
                    errors[level] = MaskLoss(reference, candidate);
                }
                if (errors[ReferenceLevel] != 0)
                    throw new InvalidOperationException($"{partId}: reference render changed on repeat.");
                views[view] = new ViewSample { direction = direction, errorByLevel = errors };
            }

            WriteContactSheet(renderer, meshes[ReferenceLevel], meshes[0], bounds,
                Path.Combine(reportFolder, partId + ".png"));
            return new PartTable
            {
                partId = partId,
                unityVersion = Application.unityVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                resolution = Resolution,
                referenceLevel = ReferenceLevel,
                referenceCenter = bounds.center,
                referenceRadius = bounds.extents.magnitude,
                bakeSettingsHash = settingsHash,
                meshPaths = paths,
                sourceSha256 = paths.Select(path => Sha256(File.ReadAllBytes(Path.Combine(unityRoot, path)))).ToArray(),
                importDependencyHashes = paths.Select(path => AssetDatabase.GetAssetDependencyHash(path).ToString()).ToArray(),
                triangles = triangles,
                views = views
            };
        }

        static int TriangleCount(Mesh mesh)
        {
            var count = 0;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndexCount(submesh);
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles || indices % 3 != 0)
                    throw new InvalidOperationException($"{mesh.name}: expected triangle indices.");
                count += (int)(indices / 3);
            }
            return count > 0 ? count : throw new InvalidOperationException($"{mesh.name}: empty mesh.");
        }

        static void ValidateMask(byte[] mask, bool requireForeground, string label)
        {
            if (requireForeground && Array.IndexOf(mask, (byte)255) < 0)
                throw new InvalidOperationException($"Empty mask: {label}.");
            for (var i = 0; i < Resolution; i++)
            {
                if (mask[i] != 0 || mask[i + (Resolution - 1) * Resolution] != 0 ||
                    mask[i * Resolution] != 0 || mask[i * Resolution + Resolution - 1] != 0)
                    throw new InvalidOperationException($"Mask touches its frame: {label}.");
            }
        }

        static void WriteContactSheet(IsolatedMaskRenderer renderer, Mesh referenceMesh, Mesh coarseMesh,
            Bounds bounds, string path)
        {
            var width = 3 * Resolution;
            var height = 2 * Resolution;
            var pixels = new Color32[width * height];
            var black = new Color32(0, 0, 0, 255);
            var white = new Color32(255, 255, 255, 255);
            var missing = new Color32(255, 92, 78, 255);
            var extra = new Color32(54, 219, 197, 255);
            var directions = new[] { Vector3.right, Vector3.up }; // bottom edge; top broad face
            for (var row = 0; row < directions.Length; row++)
            {
                var reference = renderer.Capture(referenceMesh, bounds, directions[row]);
                var candidate = renderer.Capture(coarseMesh, bounds, directions[row]);
                for (var y = 0; y < Resolution; y++)
                for (var x = 0; x < Resolution; x++)
                {
                    var i = y * Resolution + x;
                    var target = (row * Resolution + y) * width + x;
                    var a = reference[i] != 0;
                    var b = candidate[i] != 0;
                    pixels[target] = a ? white : black;
                    pixels[target + Resolution] = b ? white : black;
                    pixels[target + 2 * Resolution] = a ? (b ? white : missing) : (b ? extra : black);
                }
            }
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                sheet.SetPixels32(pixels);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        static string Sha256(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
