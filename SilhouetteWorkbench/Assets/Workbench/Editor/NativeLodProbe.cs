using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Workbench
{
    // GPU color readback reports the LOD Unity actually draws, without guessing its thresholds.
    public sealed class NativeLodProbe : IDisposable
    {
        static readonly int LevelId = Shader.PropertyToID("_LevelId");
        readonly Scene preview;
        readonly Camera camera;
        readonly Shader shader;
        readonly RenderTexture target;
        readonly Texture2D readback;
        readonly RenderPipeline.StandardRequest request;
        readonly List<LODGroup> groups = new List<LODGroup>();
        readonly List<Renderer[]> levelsByPart = new List<Renderer[]>();
        readonly List<(int part, int level)> ids = new List<(int, int)> { (-1, -1) };
        readonly List<Material> materials = new List<Material>();

        public NativeLodProbe(int width, int height)
        {
            shader = Shader.Find("Hidden/Workbench/NativeLodId");
            if (width <= 0 || height <= 0 || shader == null || !shader.isSupported)
                throw new InvalidOperationException("Native LOD probe requires its URP shader and positive dimensions.");
            preview = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("LOD observation camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            target.filterMode = FilterMode.Point;
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            request = new RenderPipeline.StandardRequest { destination = target };
        }

        public void AddPart(Mesh[] coarseToFine, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (coarseToFine.Length < 2 || ids.Count + coarseToFine.Length > 256)
                throw new ArgumentException("Native LOD probe needs at least two and at most 255 total mesh codes.");
            var root = new GameObject($"Native LOD part {groups.Count}");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = scale;
            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;
            var bounds = coarseToFine[coarseToFine.Length - 1].bounds;
            group.localReferencePoint = bounds.center;
            group.size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var renderers = new Renderer[coarseToFine.Length];
            for (var level = 0; level < coarseToFine.Length; level++)
            {
                var child = new GameObject($"LOD {level}");
                SceneManager.MoveGameObjectToScene(child, preview);
                child.transform.SetParent(root.transform, false);
                var mesh = coarseToFine[level];
                var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetFloat(LevelId, ids.Count / 255f);
                ids.Add((groups.Count, level));
                materials.Add(material);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                var submeshMaterials = new Material[mesh.subMeshCount];
                for (var i = 0; i < submeshMaterials.Length; i++) submeshMaterials[i] = material;
                renderer.sharedMaterials = submeshMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderers[level] = renderer;
            }
            groups.Add(group);
            levelsByPart.Add(renderers);
        }

        public void SetThresholds(float[] finestToCoarsest)
        {
            if (finestToCoarsest.Length < 2 || finestToCoarsest[finestToCoarsest.Length - 1] != 0)
                throw new ArgumentException("Native thresholds need a zero coarsest threshold.");
            for (var i = 0; i < finestToCoarsest.Length; i++)
                if (!(finestToCoarsest[i] >= 0 && finestToCoarsest[i] <= 1) ||
                    (i > 0 && finestToCoarsest[i] >= finestToCoarsest[i - 1]))
                    throw new ArgumentException("Native thresholds must be strictly descending in [0, 1].");
            for (var part = 0; part < groups.Count; part++)
            {
                var renderers = levelsByPart[part];
                if (renderers.Length != finestToCoarsest.Length)
                    throw new ArgumentException("Thresholds must match the native mesh ladder.");
                var lods = new LOD[renderers.Length];
                for (var i = 0; i < lods.Length; i++)
                    lods[i] = new LOD(finestToCoarsest[i], new[] { renderers[lods.Length - 1 - i] });
                groups[part].SetLODs(lods);
            }
        }

        public int[] ReadLevels(Camera source)
        {
            camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            camera.fieldOfView = source.fieldOfView;
            camera.aspect = source.aspect;
            camera.nearClipPlane = source.nearClipPlane;
            camera.farClipPlane = source.farClipPlane;
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("The active pipeline does not support a camera render request.");
            var bias = QualitySettings.lodBias;
            var maxLevel = QualitySettings.maximumLODLevel;
            var previousTarget = RenderTexture.active;
            try
            {
                QualitySettings.SetLODSettings(1f, 0, false);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                var selected = new int[groups.Count];
                Array.Fill(selected, -1);
                var colors = readback.GetRawTextureData<Color32>();
                for (var i = 0; i < colors.Length; i++)
                {
                    var color = colors[i];
                    if (color.g != 0 || color.b != 0 || color.r >= ids.Count)
                        throw new InvalidOperationException($"Unexpected native LOD pixel {color}.");
                    if (color.r == 0) continue;
                    var id = ids[color.r];
                    if (selected[id.part] >= 0 && selected[id.part] != id.level)
                        throw new InvalidOperationException($"More than one LOD was drawn for part {id.part}.");
                    selected[id.part] = id.level;
                }
                if (Array.IndexOf(selected, -1) >= 0)
                    throw new InvalidOperationException("A pilot part could not be observed in the native pass.");
                return selected;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                QualitySettings.SetLODSettings(bias, maxLevel, false);
            }
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(preview);
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(readback);
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
