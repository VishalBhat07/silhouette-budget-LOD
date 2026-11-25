using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Workbench.Demo;

namespace Workbench
{
    /// <summary>Creates the runtime-built shuttle flight scene used by the graphics expansion.</summary>
    public static class FlightScene
    {
        public const string Path = "Assets/Workbench/Scenes/ShuttleFlight.unity";

        [MenuItem("Workbench/Flight/Build Shuttle Flight Scene")]
        public static void Build()
        {
            var current = SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var flight = new GameObject("Shuttle flight");
            flight.AddComponent<SalvageFlight>();

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color32(255, 233, 203, 255);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(20, 22, 0);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(44, 62, 88, 255);
            RenderSettings.ambientEquatorColor = new Color32(74, 80, 86, 255);
            RenderSettings.ambientGroundColor = new Color32(42, 39, 36, 255);
            RenderSettings.skybox = null;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path) ?? "Assets/Workbench/Scenes");
            EditorSceneManager.SaveScene(scene, Path);
            AssetDatabase.Refresh();
            Debug.Log($"Shuttle flight scene saved to {Path}");
        }

        [MenuItem("Workbench/Flight/Open Shuttle Flight Scene")]
        public static void Open()
        {
            if (!File.Exists(Path))
            {
                Debug.LogWarning($"{Path} does not exist yet. Run Workbench/Flight/Build Shuttle Flight Scene first.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Path);
        }
    }
}
