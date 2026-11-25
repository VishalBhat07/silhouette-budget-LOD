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

### Interactive desktop demo

Open either expanded scene and press **Play**, or choose **Workbench → Build macOS Interactive Demo** to create `Results/demo/SilhouetteWorkbench.app`. The demo starts paused so you can inspect a single pose before playing.

- Switch between **Separated** and **Cluttered**; the replay pose and budget stay fixed.
- Play a path or scrub the timeline. Select a policy or move the triangle budget slider to pause on the current pose for comparison.
- Read **selected triangles / cap**, unused budget, and individual part LODs. The actual spend can differ between policies at the same cap.
- Keyboard: **P** plays or pauses, **1–3** selects a policy, and **L** switches layouts. The controls are also reachable with Tab.

The interactive HUD is bypassed by the standalone timing flag. The Unity Play Mode smoke test is in `Workbench.Demo.Play.Tests`.

### Play Orbital Salvage

In Unity Hub, open the `SilhouetteWorkbench` project with Unity **6000.6.9f1**. Open `Assets/Workbench/Scenes/OrbitalSalvage.unity` and press **Play**.

For a Linux x86-64 standalone player, install **Linux Build Support (Mono)** for that Unity version and choose **Workbench → Build Linux Orbital Salvage**. From the repository root, run:

```sh
cd Results/orbital-salvage
./OrbitalSalvage.x86_64
```

Keep the executable with its companion files and data folder. The player opens in a 1280 × 720 window.

Walk down from the observation gantry and retrieve the glowing cell on the port cargo stack. Use it to cycle the cargo lift, take it back, then carry it to engineering. Installing the cell restores emergency power. Collect the navigation core, follow the service drone through the open hatch, and use **E** at the extraction and boarding prompts.

The shuttle flight lasts 7.5 seconds. Press **1** for the cockpit, **2** for the chase camera, or **Space** to pause. After landing, use **E** to disembark, follow the rover to the relay, and use **E** to install the navigation core.

The 32 × 48 m dock has two routes down from the arrival overlook. Use the central stairs for the shortest route to the mission or take the starboard ramp and follow the marked service apron around the tug. Guardrails and solid machinery block movement with collision geometry that remains consistent across visual LODs.

The technician uses the animated [Quaternius Astronaut](https://poly.pizza/m/3hC2i0CTuO) from the [Ultimate Space Kit](https://quaternius.com/packs/ultimatespacekit.html), released under CC0. The suit palette was adapted for the salvage crew.

| Control | Action |
|---|---|
| WASD or arrows, Shift | Walk or sprint |
| Hold right mouse and drag | Orbit the third-person camera |
| Mouse wheel | Adjust follow distance |
| E | Pick up, install, or use the nearby mission object |
| F | Freeze or resume the comparison pose |
| 1 / 2 / 3 | Screen size / directional silhouette / view average |
| [ / ] | Decrease or increase the triangle cap |
| R | Restart the mission |

The allocator manages 32 placed assemblies from 12 asset families, with 13,452 triangles across their coarsest meshes and 159,484 at full detail. The scene starts with a 24,000-triangle cap for selected managed meshes in the camera frustum. The player, drone, cargo, hull cladding, architecture, and distant scenery have their own geometry. See the [asset manifest](Experiments/salvage-lod-assets.csv) for the four measured mesh costs per instance. Regenerate the meshes and tables with `python3 Tools/generate_salvage_meshes.py` and Pillow.

The cutaway turbines, cargo grilles, docking clamps, finned compressors, and open heat exchangers add geometry whose gaps and profiles change at lower LOD. The fan and antenna pairs show how a different angle changes the silhouette of the same mesh.

For real hardware references, see the [NASA RS-25 hardware photos on page 19](https://www.nasa.gov/wp-content/uploads/2021/05/osbp_htdb_stennis_final_may19_2021.pdf) for pipes and nozzle bands, [NASA's ISS radiator study](https://ntrs.nasa.gov/api/citations/20260005820/downloads/ICES_MARVL_STARK_v3.pdf) for truss-mounted panels, and the [Voyager antenna construction photo](https://science.nasa.gov/photojournal/voyager-antenna-dish-construction-2/) for dish profile, supports, and scale.

In Editor Play Mode, **Workbench → Orbital Salvage → Measure Current View** compares all three policies at the same camera pose and cap. **Measure Dock Camera Route** samples 12 poses in each of the three areas with the current world state held fixed. Both write per-object CSVs in `Results/orbital-study/`, including actual spend, selected LOD, reference and candidate pixel counts, and mask error. An empty error field means neither image contained pixels for that part. **Render Policy Comparison** saves fixed-camera shaded PNGs for all three policies and the full-detail reference, plus the selected levels and triangle counts. For a Linux standalone player use **Workbench → Build Linux Orbital Salvage**.

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
