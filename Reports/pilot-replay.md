# Pilot perspective replay

Unity `6000.6.9f1` · `Metal` · 1280×720 · screen-size exponent 2.
3 traces × 12 poses; nearest baked view was 2.93–23.65° away.

Loss is mean per-object `1 − IoU`, then averaged across poses. A positive difference means silhouette-aware allocation had lower loss. Exact-spend matching is paired per pose, not on average.

| Cap | Comparator | Mean spend (comparator / silhouette) | Comparator loss | Silhouette loss | Shared-cap gain | Exact-spend gain | Exact pairs | Different assignments |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 960 | ScreenSize | 960.0 / 960.0 | 2.446% | 2.191% | +0.255 pp | +0.255 pp | 36/36 | 36/36 |
| 960 | ViewAverage | 960.0 / 960.0 | 2.340% | 2.191% | +0.149 pp | +0.149 pp | 36/36 | 6/36 |
| 1440 | ScreenSize | 1440.0 / 1413.3 | 1.997% | 1.250% | +0.747 pp | +0.762 pp | 32/36 | 36/36 |
| 1440 | ViewAverage | 1440.0 / 1413.3 | 1.224% | 1.250% | -0.026 pp | +0.000 pp | 32/36 | 4/36 |
| 2160 | ScreenSize | 2160.0 / 1946.7 | 0.961% | 0.787% | +0.174 pp | +0.449 pp | 4/36 | 36/36 |
| 2160 | ViewAverage | 1920.0 / 1946.7 | 0.799% | 0.787% | +0.012 pp | +0.036 pp | 32/36 | 17/36 |

## Exact-spend gain by trace

| Cap | Comparator | Trace | Gain | Exact pairs |
|---:|---|---|---:|---:|
| 960 | ScreenSize | dolly | +0.251 pp | 12/12 |
| 960 | ScreenSize | grazing | +0.087 pp | 12/12 |
| 960 | ScreenSize | orbit | +0.427 pp | 12/12 |
| 960 | ViewAverage | dolly | +0.000 pp | 12/12 |
| 960 | ViewAverage | grazing | +0.447 pp | 12/12 |
| 960 | ViewAverage | orbit | +0.000 pp | 12/12 |
| 1440 | ScreenSize | dolly | +0.787 pp | 12/12 |
| 1440 | ScreenSize | grazing | +0.442 pp | 8/12 |
| 1440 | ScreenSize | orbit | +0.950 pp | 12/12 |
| 1440 | ViewAverage | dolly | +0.000 pp | 12/12 |
| 1440 | ViewAverage | grazing | +0.000 pp | 8/12 |
| 1440 | ViewAverage | orbit | +0.000 pp | 12/12 |
| 2160 | ScreenSize | dolly | — | 0/12 |
| 2160 | ScreenSize | grazing | +0.449 pp | 4/12 |
| 2160 | ScreenSize | orbit | — | 0/12 |
| 2160 | ViewAverage | dolly | +0.008 pp | 12/12 |
| 2160 | ViewAverage | grazing | +0.109 pp | 8/12 |
| 2160 | ViewAverage | orbit | +0.015 pp | 12/12 |

Descriptive pilot only: the same three meshes recur in every trace. Nearby poses are not independent samples, and the replay does not measure frame time, popping, or human recognition.
