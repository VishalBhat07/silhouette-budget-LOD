# Silhouette-Budget LOD

A desktop Unity graphics research project on how to spend a fixed triangle budget across several objects. No VR hardware is required.

A large toolbox may look nearly unchanged at a lower level of detail, while a small gear can lose the teeth that distinguish it from a washer. This project asks whether a renderer should spend triangles where they preserve the most silhouette detail rather than where objects look biggest on screen.

## What we'll test

- **Screen-size baseline:** Allocate triangles using projected object size.
- **Silhouette-aware allocation:** Use baked silhouette error to estimate how much a finer mesh would improve the current view.
- **View-averaged comparison:** Remove the camera-direction signal to test whether it helps beyond knowing which objects generally need more detail.

The benchmark will replay unseen camera paths and compare silhouette error, LOD popping, and frame time. It will report both results under a shared budget cap and comparisons at exactly matched triangle counts.

## Start here

This repository currently contains the project plan, not a runnable Unity build. Start with the [implementation and research plan](docs/superpowers/plans/2026-11-22-silhouette-budget-lod.md). The first milestone is a three-object pilot with a gear, washer, and toolbox.

**Planned stack:** Unity 6 LTS, URP, C#, Blender, and Python for offline analysis.
