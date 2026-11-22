using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Workbench
{
    public static class ExpandedLayouts
    {
        const string Scenes = "Assets/Workbench/Scenes/";
        const string Pilot = Scenes + "Inspection.unity";

        [Serializable]
        sealed class Config
        {
            public int previewBudget;
            public Part[] parts;
            public Layout[] layouts;
        }

        [Serializable]
        sealed class Part
        {
            public string id, name, rgb;
        }

        [Serializable]
        sealed class Layout
        {
            public string id, scene;
            public float[] cameraPosition, cameraTarget;
            public Placement[] placements;
        }

        [Serializable]
        sealed class Placement
        {
            public string id;
            public float x, z, yaw;
        }

        // Unity -batchmode -executeMethod Workbench.ExpandedLayouts.BakeAndBuild
        public static void BakeAndBuild()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the individual Workbench menu commands in the interactive editor.");
            EditorSceneManager.OpenScene(Pilot, OpenSceneMode.Single);
            SilhouetteBaker.BakeAdditional();
            Build();
        }

        [MenuItem("Workbench/Build Missing Expanded Scenes")]
        public static void Build()
        {
            var open = SceneManager.GetActiveScene();
            if (Application.isPlaying || !open.IsValid() || open.isDirty || open.path.Length == 0 ||
                SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Open a single saved scene in Edit Mode first.");

            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var text = File.ReadAllText(Path.Combine(root, "Experiments", "expanded-layouts.json"));
            var config = JsonUtility.FromJson<Config>(text);
            var specs = config.parts.ToDictionary(part => part.id, StringComparer.Ordinal);
            var meshes = specs.Values.ToDictionary(part => part.id, part => Enumerable.Range(0, 4)
                .Select(i => AssetDatabase.LoadAllAssetsAtPath(
                    $"{PartFolder(part.id)}/{part.id}_level{i}.obj").OfType<Mesh>().Single()).ToArray());
            var tables = specs.Values.ToDictionary(part => part.id,
                part => AssetDatabase.LoadAssetAtPath<TextAsset>($"{PartFolder(part.id)}/silhouettes.json"));
            if (tables.Values.Any(table => table == null))
                throw new InvalidOperationException("Bake all five additional parts before creating the layouts.");

            foreach (var layout in config.layouts)
                if (layout.placements.Length != specs.Count ||
                    !layout.placements.Select(placement => placement.id).ToHashSet().SetEquals(specs.Keys) ||
                    layout.cameraPosition.Length != 3 || layout.cameraTarget.Length != 3)
                    throw new InvalidOperationException($"Invalid placement table for {layout.id}.");

            var previous = open.path;
            var created = 0;
            try
            {
                foreach (var layout in config.layouts)
                {
                    var path = Scenes + layout.scene + ".unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) continue;
                    if (!AssetDatabase.CopyAsset(Pilot, path))
                        throw new InvalidOperationException($"Could not copy the pilot layout to {path}.");
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var partRoot = scene.GetRootGameObjects().Single(go => go.name == "Pilot Parts");
                    partRoot.name = "Inspection Parts";
                    var bench = scene.GetRootGameObjects().Single(go => go.name == "Workbench");
                    var baseMaterial = bench.transform.Find("Bench Top").GetComponent<MeshRenderer>().sharedMaterial;
                    var camera = scene.GetRootGameObjects().Single(go => go.CompareTag("MainCamera")).GetComponent<Camera>();
                    var eye = Vec3(layout.cameraPosition);
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Vec3(layout.cameraTarget) - eye));
                    var parts = new PartLods[layout.placements.Length];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var placement = layout.placements[i];
                        var part = specs[placement.id];
                        var child = partRoot.transform.Find(part.name);
                        var obj = child != null ? child.gameObject : new GameObject(part.name);
                        obj.transform.SetParent(partRoot.transform, false);
                        obj.transform.localPosition = new Vector3(placement.x, 1, placement.z);
                        obj.transform.localRotation = Quaternion.Euler(0, placement.yaw, 0);
                        var lod = obj.GetComponent<PartLods>() ?? obj.AddComponent<PartLods>();
                        var binding = new SerializedObject(lod);
                        SetArray(binding.FindProperty("meshes"), meshes[part.id]);
                        binding.FindProperty("silhouetteTable").objectReferenceValue = tables[part.id];
                        binding.ApplyModifiedPropertiesWithoutUndo();
                        obj.GetComponent<MeshFilter>().sharedMesh = meshes[part.id][3];
                        obj.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(part, baseMaterial);
                        parts[i] = lod;
                    }

                    var controller = partRoot.GetComponent<WorkbenchController>();
                    var settings = new SerializedObject(controller);
                    SetArray(settings.FindProperty("parts"), parts);
                    settings.FindProperty("triangleBudget").intValue = config.previewBudget;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    controller.PrepareEvaluation();
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException($"Could not save {path}.");
                    created++;
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (SceneManager.GetActiveScene().path != previous)
                    EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
            Debug.Log($"Created {created} expanded scenes. Existing scenes were not overwritten.");
        }

        static Material GetMaterial(Part part, Material baseMaterial)
        {
            var path = $"{PartFolder(part.id)}/{part.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (!ColorUtility.TryParseHtmlString(part.rgb, out var color))
                throw new InvalidOperationException($"Invalid color for {part.id}.");
            var material = new Material(baseMaterial) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetArray(SerializedProperty property, UnityEngine.Object[] assets)
        {
            property.arraySize = assets.Length;
            for (var i = 0; i < assets.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
        }

        static string PartFolder(string id) => $"Assets/Workbench/Parts/{id}";
        static Vector3 Vec3(float[] xyz) => new Vector3(xyz[0], xyz[1], xyz[2]);
    }
}
