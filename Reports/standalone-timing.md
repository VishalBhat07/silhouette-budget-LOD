# Standalone player timing

Unity `6000.6.9f1` · Mac OS X 26.0.1 · Mac14,2 · Apple M2 / Apple M2 (`Metal`).
Non-development windowed player, 1280×720; quality level 0; VSync off; target frame rate uncapped. Power source: AC Power. Thermal state was not logged.

363 rendered poses per block, 5 interleaved repeats per layout and policy, 60 unreported warm-up frames per block, shared cap 6,400. No image readback, overlays or per-frame disk writes.

Values are the median of per-block medians and the median of per-block p95s. The wall-frame time includes player scheduling; selector and mesh-apply times use CPU stopwatch intervals. GPU timings are delivered asynchronously and reported per block, not matched to poses.

| Layout | Policy | Wall frame ms median / p95 | Repeat median range (ms) | Selection µs median / p95 | Apply µs median / p95 | GPU ms median / p95 | GPU samples |
|---|---|---:|---:|---:|---:|---:|---:|
| separated | ScreenSize | 1.957 / 2.579 | 1.118–2.106 | 15.400 / 24.800 | 1.100 / 3.200 | 4.388 / 5.773 | 1,815 (5 repeats) |
| separated | ViewAverage | 1.970 / 2.768 | 1.398–2.075 | 16.200 / 28.100 | 0.900 / 3.300 | 4.344 / 5.726 | 1,815 (5 repeats) |
| separated | Silhouette | 1.965 / 2.615 | 1.253–2.088 | 24.900 / 41.200 | 1.000 / 3.600 | 4.343 / 5.762 | 1,815 (5 repeats) |
| clutter | ScreenSize | 1.961 / 2.814 | 1.856–2.144 | 14.900 / 26.100 | 1.500 / 3.200 | 4.287 / 5.892 | 1,815 (5 repeats) |
| clutter | ViewAverage | 1.919 / 2.642 | 1.702–2.098 | 19.800 / 30.500 | 1.800 / 3.500 | 4.252 / 5.712 | 1,815 (5 repeats) |
| clutter | Silhouette | 1.943 / 2.638 | 1.584–2.096 | 28.600 / 39.900 | 2.000 / 3.500 | 4.215 / 5.755 | 1,815 (5 repeats) |

This is one host and a small workbench scene, at equal caps (not necessarily equal actual spend). Do not read small frame-time differences as a hardware-independent speedup.
