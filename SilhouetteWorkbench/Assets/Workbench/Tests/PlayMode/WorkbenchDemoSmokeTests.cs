#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Workbench.Demo;

namespace Workbench.Tests
{
    public class WorkbenchDemoSmokeTests
    {
        [UnityTest]
        public IEnumerator BudgetControlsAndLayoutChangesPreserveTheLockedPose()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Workbench/Scenes/InspectionExpanded.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var obj = GameObject.Find("Silhouette Workbench Demo");
            if (obj == null)
            {
                obj = new GameObject("Silhouette Workbench Demo");
                Object.DontDestroyOnLoad(obj);
                obj.AddComponent<WorkbenchDemo>();
            }
            yield return null;
            var root = obj.GetComponent<UIDocument>().rootVisualElement;
            var budget = root.Q<SliderInt>("budget");
            var timeline = root.Q<SliderInt>("timeline");
            Assert.That(budget.value, Is.EqualTo(6400));
            Assert.That(root.Query<Label>(className: "part-stat").ToList().Count, Is.EqualTo(8));
            Assert.That(Button("Directional").ClassListContains("chosen"), Is.True);

            timeline.value = 37;
            var locked = Camera.main.transform.localToWorldMatrix;
            var oldSpend = root.Q<Label>(className: "spend").text;
            Click(Button("Screen size"));
            Assert.That(Camera.main.transform.localToWorldMatrix, Is.EqualTo(locked));
            Assert.That(timeline.value, Is.EqualTo(37));
            Assert.That(Button("Screen size").ClassListContains("chosen"), Is.True);
            Assert.That(root.Q<Label>(className: "spend").text, Is.Not.EqualTo(oldSpend));

            budget.value = budget.lowValue;
            Assert.That(root.Query<Label>(className: "part-stat").ToList().All(l => l.text.StartsWith("L0")), Is.True);
            Click(Button("Cluttered"));
            for (var attempts = 0; attempts < 300 && SceneManager.GetActiveScene().name != "InspectionClutter"; attempts++)
                yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("InspectionClutter"));
            Assert.That(timeline.value, Is.EqualTo(37));
            Assert.That(budget.value, Is.EqualTo(budget.lowValue));
            Assert.That(Button("Screen size").ClassListContains("chosen"), Is.True);

            Button Button(string text) => root.Query<Button>().ToList().Single(b => b.text == text);
        }

        static void Click(Button button)
        {
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }
    }
}
#endif
