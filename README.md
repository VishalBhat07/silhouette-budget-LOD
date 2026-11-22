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

To inspect a different level, expand its OBJ under `Assets/Workbench/Parts/` and assign the mesh sub-asset to the corresponding part's **Mesh Filter**. Level 0 is coarsest and level 3 is finest. All three parts have **240, 480, 960, and 1,920 triangles** per level, as recorded in the [pilot mesh manifest](Experiments/pilot-meshes.csv). The offline silhouette baker is available; the runtime selector and held-out benchmark are not implemented yet.

### Regenerate the meshes

The pilot uses original procedural geometry and the same pinned quadric-error simplifier for every part. Each lower level is generated directly from its reference in the same coordinate frame. This replaces manual Blender decimation for the pilot and makes the inputs reproducible.

```sh
uv run --no-project --python 3.13 --with numpy==2.2.6 --with fast-simplification==0.1.13 python Tools/generate_pilot_meshes.py
uv run --no-project --python 3.13 --with numpy==2.2.6 --with fast-simplification==0.1.13 python -m unittest discover -s Tools -p 'test_*.py'
```

### Bake and inspect silhouettes

Run **Workbench → Bake Pilot Silhouettes** in Unity. The baker renders each part from 32 fixed orthographic directions at 256 × 256 pixels, with each LOD framed by the full-resolution reference bounds. Each part folder gets a `silhouettes.json` containing local directions and `1 − IoU` per level, plus mesh and bake provenance. Zero means identical projected masks, not identical geometry.

Open the generated PNGs under `Results/pilot-silhouettes/`. Each sheet shows **reference**, **coarsest LOD**, and **difference** from left to right, with top and side views. Red pixels are missing from the coarse mesh; cyan pixels are extra. The images are local diagnostics and are not part of the held-out benchmark.

Run the `Workbench.Silhouettes.Tests` assembly in Unity's **EditMode** Test Runner to check the mask metric, view sampling, and a GPU-rendered washer bore.

See the [implementation and research plan](docs/superpowers/plans/2026-11-22-silhouette-budget-lod.md) for the bake, allocator, and evaluation protocol.
