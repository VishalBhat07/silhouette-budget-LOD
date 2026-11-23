using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Workbench.Demo
{
    // An inspection instrument, not a splash screen: inspect the same pose under different policies.
    public sealed class WorkbenchDemo : MonoBehaviour
    {
        static readonly string[] SceneNames = { "InspectionExpanded", "InspectionClutter" };
        static readonly string[] LayoutNames = { "Separated", "Cluttered" };
        static readonly string[] TraceNames = { "High orbit", "Grazing", "Dolly" };
        static readonly string[] PolicyNames = { "Screen size", "Directional", "View averaged" };
        static readonly string[] Descriptions = {
            "Gives more detail to parts that occupy more of the screen.",
            "Uses the closest baked view to estimate the outline damage from each mesh.",
            "Uses each part’s average outline damage, regardless of camera direction."
        };

        ReplayMotion motion;
        DemoState state;
        Camera sceneCamera;
        WorkbenchController controller;
        IReadOnlyList<PartLods> parts;
        int[] selection;
        int traceIndex;
        int layoutIndex;
        float partialFrame;
        bool loading;
        RenderTexture render;
        PanelSettings panel;
        SliderInt budget, timeline;
        Label spent, budgetDetail, policyDetail, layoutHeading, time, layoutDescription;
        Button play;
        Button[] layouts, policies, traces;
        VisualElement ui, side, transport, partsList;
        Label[] partStats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartDemo()
        {
            if (!DemoState.ShouldStart(SceneManager.GetActiveScene().name, Environment.GetCommandLineArgs())) return;
            var obj = new GameObject("Silhouette Workbench Demo");
            DontDestroyOnLoad(obj);
            obj.AddComponent<WorkbenchDemo>();
        }

        void Start()
        {
            motion = ReplayMotion.Load();
            var theme = Resources.Load<ThemeStyleSheet>("InspectionRuntimeTheme");
            var stylesheet = Resources.Load<StyleSheet>("InspectionDemo");
            if (theme == null || stylesheet == null)
                throw new InvalidOperationException("The inspection demo's UI styles did not import.");
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(motion.width, motion.height);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;

            render = new RenderTexture(motion.width, motion.height, 24, RenderTextureFormat.ARGB32)
                { name = "Inspection viewport" };
            render.Create();
            var background = new GameObject("Demo display camera");
            background.transform.SetParent(transform, false);
            var outputCamera = background.AddComponent<Camera>();
            outputCamera.cullingMask = 0;
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = new Color32(24, 35, 41, 255);
            outputCamera.depth = 10;

            ui = document.rootVisualElement;
            ui.styleSheets.Add(stylesheet);
            BuildInterface();
            SceneManager.sceneLoaded += SceneLoaded;
            BindScene();
        }

        static VisualElement Box(VisualElement parent, string css)
        {
            var item = new VisualElement();
            item.AddToClassList(css);
            parent.Add(item);
            return item;
        }

        static Label Text(VisualElement parent, string copy, string css)
        {
            var item = new Label(copy);
            item.AddToClassList(css);
            parent.Add(item);
            return item;
        }

        static Button ActionButton(VisualElement parent, string name, Action action)
        {
            var button = new Button(action) { text = name };
            button.AddToClassList("control");
            parent.Add(button);
            return button;
        }

        static Button[] ChoiceRow(VisualElement parent, string[] names, Action<int> action)
        {
            var row = Box(parent, "choice-row");
            return names.Select((name, index) => ActionButton(row, name, () => action(index))).ToArray();
        }

        void BuildInterface()
        {
            var frame = Box(ui, "workspace");
            var scroll = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("sidebar");
            frame.Add(scroll);
            side = scroll.contentContainer;
            side.AddToClassList("controls");
            Text(side, "Silhouette Workbench", "title");
            Text(side, "Compare mesh choices under one triangle budget.", "intro");

            Text(side, "Triangle budget", "section");
            budget = new SliderInt { name = "budget", tooltip = "Maximum selected triangles across the bench parts" };
            side.Add(budget);
            spent = Text(side, "", "spend");
            budgetDetail = Text(side, "", "detail");
            budget.RegisterValueChangedCallback(e =>
            {
                if (loading || state == null) return;
                state.SetCap(e.newValue);
                UpdateSelection();
            });

            Text(side, "Allocation policy", "section");
            policies = ChoiceRow(side, PolicyNames, SetPolicy);
            policyDetail = Text(side, "", "detail");
            Text(side, "Layout", "section");
            layouts = ChoiceRow(side, LayoutNames, SwitchLayout);
            Text(side, "Selected geometry", "section");
            partsList = Box(side, "parts");

            var stage = Box(frame, "stage");
            var top = Box(stage, "stage-head");
            layoutHeading = Text(top, "", "stage-title");
            layoutDescription = Text(top, "", "stage-detail");
            var well = Box(stage, "viewport");
            var image = new Image { image = render, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("viewport-image");
            well.Add(image);

            transport = Box(stage, "transport");
            var transportTop = Box(transport, "transport-top");
            play = ActionButton(transportTop, "Play", TogglePlayback);
            play.name = "play";
            traces = ChoiceRow(transportTop, TraceNames, ChooseTrace);
            time = Text(transportTop, "", "clock");
            timeline = new SliderInt(0, motion.framesPerTrace - 1)
                { name = "timeline", tooltip = "Scrub to a specific replay pose" };
            transport.Add(timeline);
            timeline.RegisterValueChangedCallback(e => { state.Scrub(e.newValue); UpdateSelection(); });
            Text(transport, "P  Play / pause     1–3  Select policy     L  Change layout", "shortcuts");
        }

        void BindScene()
        {
            layoutIndex = Array.IndexOf(SceneNames, SceneManager.GetActiveScene().name);
            if (layoutIndex < 0) return;
            loading = true;
            controller = GameObject.Find("Inspection Parts").GetComponent<WorkbenchController>();
            controller.enabled = false;
            parts = controller.PrepareEvaluation();
            selection = new int[parts.Count];
            sceneCamera = Camera.main;
            motion.Configure(sceneCamera);
            sceneCamera.targetTexture = render;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color32(61, 74, 79, 255);
            sceneCamera.depth = 0;
            var old = state;
            state = new DemoState(motion.framesPerTrace, parts.Sum(p => p.TrianglesAt(0)),
                parts.Sum(p => p.TrianglesAt(p.FinestLevel)), old?.Cap ?? motion.triangleBudget);
            if (old != null) state.ChoosePolicy(old.Policy);
            state.Scrub(old?.Frame ?? motion.framesPerTrace / 2);
            budget.highValue = state.Maximum;
            budget.lowValue = state.Minimum;
            budget.SetValueWithoutNotify(state.Cap);
            partsList.Clear();
            partStats = new Label[parts.Count];
            for (var i = 0; i < parts.Count; i++)
            {
                var row = Box(partsList, "part");
                Text(row, parts[i].name, "part-name");
                partStats[i] = Text(row, "", "part-stat");
            }
            layoutHeading.text = LayoutNames[layoutIndex] + " bench";
            layoutDescription.text = layoutIndex == 0 ? "Eight parts, spaced apart." :
                "Same parts. The toolbox hides detail behind it.";
            loading = false;
            side.SetEnabled(true);
            transport.SetEnabled(true);
            UpdateSelection();
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) => BindScene();

        void SwitchLayout(int next)
        {
            if (loading || next == layoutIndex) return;
            loading = true;
            side.SetEnabled(false);
            transport.SetEnabled(false);
            layoutHeading.text = "Opening " + LayoutNames[next].ToLowerInvariant() + " bench…";
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                $"Assets/Workbench/Scenes/{SceneNames[next]}.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadSceneAsync(SceneNames[next]);
#endif
        }

        void SetPolicy(int index) { state.ChoosePolicy((LodPolicy)index); UpdateSelection(); }
        void ChooseTrace(int index) { traceIndex = index; state.Scrub(motion.framesPerTrace / 2); UpdateSelection(); }
        void TogglePlayback() { state.TogglePlayback(); partialFrame = 0; UpdateSelection(); }

        void Update()
        {
            if (state == null || loading) return;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.digit1Key.wasPressedThisFrame) SetPolicy(0);
                if (keys.digit2Key.wasPressedThisFrame) SetPolicy(1);
                if (keys.digit3Key.wasPressedThisFrame) SetPolicy(2);
                if (keys.pKey.wasPressedThisFrame) TogglePlayback();
                if (keys.lKey.wasPressedThisFrame) { SwitchLayout(1 - layoutIndex); return; }
            }
            if (!state.Playing) return;
            partialFrame += Time.unscaledDeltaTime * motion.framesPerSecond;
            var steps = (int)partialFrame;
            if (steps == 0) return;
            partialFrame -= steps;
            state.Advance(steps);
            UpdateSelection();
        }

        void UpdateSelection()
        {
            if (state == null || loading) return;
            motion.SetPose(sceneCamera, motion.traces[traceIndex], state.Frame);
            var decision = controller.SelectForPose(sceneCamera, state.Policy, state.Cap, selection);
            controller.ApplySelection(selection);
            spent.text = $"{decision.Spent:N0} / {state.Cap:N0}  triangles";
            budgetDetail.text = decision.Feasible ? $"{state.Cap - decision.Spent:N0} unused · {state.Minimum:N0}–{state.Maximum:N0} range" :
                "Cap is below the cheapest available meshes.";
            policyDetail.text = Descriptions[(int)state.Policy];
            play.text = state.Playing ? "Pause" : "Play";
            timeline.SetValueWithoutNotify(state.Frame);
            time.text = $"{(float)state.Frame / motion.framesPerSecond:0.00}s  /  {(float)(motion.framesPerTrace - 1) / motion.framesPerSecond:0.00}s";
            for (var i = 0; i < parts.Count; i++)
                partStats[i].text = selection[i] < 0 ? "Out of view" :
                    $"L{selection[i]}   {parts[i].TrianglesAt(selection[i]):N0}";
            Mark(policies, (int)state.Policy);
            Mark(layouts, layoutIndex);
            Mark(traces, traceIndex);
        }

        static void Mark(Button[] items, int selected)
        {
            for (var i = 0; i < items.Length; i++) items[i].EnableInClassList("chosen", i == selected);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            if (render != null) { render.Release(); Destroy(render); }
            if (panel != null) Destroy(panel);
        }
    }
}
