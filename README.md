# Silhouette-Budget LOD

A desktop Unity graphics research project on how to spend a fixed triangle budget across several objects. No VR hardware is required.

A large toolbox may look nearly unchanged at a lower level of detail, while a small gear can lose the teeth that distinguish it from a washer. This project asks whether a renderer should spend triangles where they preserve the most silhouette detail rather than where objects look biggest on screen.

## What we'll test

- **Screen-size baseline:** Allocate triangles using projected object size.
- **Silhouette-aware allocation:** Use baked silhouette error to estimate how much a finer mesh would improve the current view.
- **View-averaged comparison:** Remove the camera-direction signal to test whether it helps beyond knowing which objects generally need more detail.

The benchmark will replay unseen camera paths and compare silhouette error, LOD popping, and frame time. It will report both results under a shared budget cap and comparisons at exactly matched triangle counts.

## Start here

Open `SilhouetteWorkbench/` in Unity Hub with editor version **6000.6.9f1**, then open `Assets/Workbench/Scenes/Inspection.unity`. The scene has an original gear, washer, and toolbox under `Pilot Parts`. Each starts at its full-resolution reference.

Level 0 is coarsest and level 3 is finest. All three parts have **240, 480, 960, and 1,920 triangles** per level, as recorded in the [pilot mesh manifest](Experiments/pilot-meshes.csv).

### Compare the runtime policies

Open `Inspection.unity`, select `Pilot Parts`, and use **Compare Policies At Current Pose** from the **Workbench Controller** component menu. The Console prints the selected level per part and actual spend for all three policies, without changing the scene. At the saved camera pose and a cap of **960 triangles**, screen size upgrades the toolbox to L1; the silhouette and view-averaged policies upgrade the gear to L1 instead. All three spend exactly 960 triangles.

In Play Mode, change **Policy** or **Triangle Budget** in the same Inspector to preview the mesh swaps. The shared allocator considers only enabled parts intersecting the camera frustum. If even the coarsest visible levels exceed the cap, it displays those levels as a fallback and logs a warning rather than claiming a feasible result. The default screen-size exponent is 2 for the pilot; it is not yet tuned on development traces.

This is a decision comparison at one pose. The replay below measures the actual visible masks instead.

### Regenerate the meshes

The pilot uses original procedural geometry and the same pinned quadric-error simplifier for every part. Each lower level is generated directly from its reference in the same coordinate frame. This replaces manual Blender decimation for the pilot and makes the inputs reproducible.

```sh
uv run --no-project --python 3.13 --with numpy==2.2.6 --with fast-simplification==0.1.13 python Tools/generate_pilot_meshes.py
uv run --no-project --python 3.13 --with numpy==2.2.6 --with fast-simplification==0.1.13 python -m unittest discover -s Tools -p 'test_*.py'
```

### Bake and inspect silhouettes

Run **Workbench → Bake Pilot Silhouettes** in Unity. The baker renders each part from 32 fixed orthographic directions at 256 × 256 pixels, with each LOD framed by the full-resolution reference bounds. Each part folder gets a `silhouettes.json` containing local directions and `1 − IoU` per level, plus mesh and bake provenance. Zero means identical projected masks, not identical geometry.

Open the generated PNGs under `Results/pilot-silhouettes/`. Each sheet shows **reference**, **coarsest LOD**, and **difference** from left to right, with top and side views. Red pixels are missing from the coarse mesh; cyan pixels are extra. The images are local diagnostics and are not part of the held-out benchmark.

Run the `Workbench.Silhouettes.Tests` assembly in Unity's **EditMode** Test Runner to check the allocator, mask metric, view sampling, and a GPU-rendered washer bore.

### Run the perspective replay

With `Inspection.unity` saved and outside Play Mode, run **Workbench → Run Pilot Perspective Replay**. It renders the three policies and a full-detail reference from the 36 poses in the [pilot protocol](Experiments/pilot-replay-config.json). Objects use depth-tested IDs, so holes and occlusion count in the visible masks. The bake tables drive selection; they are not used to score the images.

```sh
python3 Tools/analyze_pilot_replay.py
```

The [greedy replay report](Reports/pilot-replay.md) separates same-cap results from pose-paired exact-spend results.

For a control with no leftover budget, run **Workbench → Run Pilot Exact-Cost Replay** and pass `--run Results/pilot-exact-replay` to the analyzer. This uses offline dynamic programming to find the best assignment according to each policy's own score at exactly the same triangle count. The [exact-cost report](Reports/pilot-exact-cost.md) scores those meshes against the actual perspective masks. The real-time allocator still uses the greedy algorithm.

To regenerate the reports without Unity, use `--run Experiments/replays/pilot-v1` for greedy or `--run Experiments/replays/pilot-exact-v1` for exact cost.

For the [native Unity LODGroup baseline](Reports/pilot-native-lod.md), run **Workbench → Run Pilot Native LODGroup Replay**. It observes the mesh Unity draws under three threshold settings and reports error versus actual spend, not a global cap.

```sh
python3 Tools/analyze_native_lod.py --run Experiments/replays/pilot-native-v1 \
  --exact Experiments/replays/pilot-exact-v1
```

These reports use the original three-part scene. They do not measure transition popping or performance.

### Expanded workbench

The [expanded layout fixture](Experiments/expanded-layouts.json) adds a hex nut, flanged pulley, slotted bracket, handwheel, and embossed plate. The separated and cluttered scenes use exactly the same eight parts. In the cluttered layout the toolbox blocks the camera's view of parts behind it without touching them. The embossed plate is a control: surface relief can change even when its outer silhouette barely moves.

To regenerate the additional OBJ meshes, append `--expanded` to the mesh-generator command above. The five additional parts all use the same simplifier settings; its collapse aggressiveness is 8 rather than the pilot's 7 so the hex nut yields distinct coarse levels. Use the [measured counts](Experiments/expanded-meshes.csv), not their target ratios.

Open `Assets/Workbench/Scenes/InspectionExpanded.unity` for the separated layout or `InspectionClutter.unity` for the occlusion case. Both use the same baked tables and mesh ladders. To rebuild missing scenes, run **Workbench → Bake Additional Part Silhouettes**, then **Workbench → Build Missing Expanded Scenes**. This leaves existing scenes and the original `Inspection.unity` alone.

Run **Workbench → Run Expanded Layout Replay** in each saved scene to generate greedy and exact-cost measurements for the [shared camera protocol](Experiments/expanded-replay-config.json). Then run `python3 Tools/analyze_expanded_replay.py`. The Unity batch entry point is `Workbench.PilotReplay.RunAllExpandedBatch` (graphics required).

The [eight-part report](Reports/expanded-replay.md) separates visible silhouette error, triangles spent on hidden parts, and levels forced by exact triangle sums. Regenerate it from the archived measurements without opening Unity:

```sh
python3 Tools/analyze_expanded_replay.py --run Experiments/replays/expanded-v1
```

### Measure LOD popping

In a saved expanded scene, run **Workbench → Measure LOD Popping in Active Layout**. The runner compares the previous and current assignments at the same new camera pose, so camera motion does not count as popping. Run both layouts, then use `python3 Tools/analyze_temporal_replay.py` to summarize the switches.

The [transition report](Reports/temporal-replay.md) can also be regenerated from the archive with `python3 Tools/analyze_temporal_replay.py --run Experiments/replays/temporal-v1`.

### Time the standalone player

Run **Workbench → Build macOS Timing Player** to generate the non-development app. From the repo root, run the real windowed player (do not pass `-batchmode` or `-nographics`):

```sh
Results/standalone/SilhouetteTiming.app/Contents/MacOS/SilhouetteWorkbench \
  --workbench-timing "$PWD/Results/standalone/timing" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720
python3 Tools/analyze_standalone_timing.py
```

The player warms up every policy, runs five interleaved repeats per policy and layout, writes the buffered measurements, and exits. It does not render mask captures or draw a benchmark HUD. GPU timing can be unavailable even when the player renders normally. Regenerate the [first standalone timing report](Reports/standalone-timing.md) with `python3 Tools/analyze_standalone_timing.py --run Experiments/replays/timing-v1`.

See the [implementation and research plan](docs/superpowers/plans/2026-11-22-silhouette-budget-lod.md) for the bake, allocator, and evaluation protocol.
