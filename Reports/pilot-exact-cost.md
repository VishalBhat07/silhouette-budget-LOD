# Pilot exact-cost control

Unity `6000.6.9f1` · `Metal` · 1280×720 · screen-size exponent 2.
3 traces × 12 poses; nearest baked view was 2.93–23.65° away.

Offline dynamic programming forced every policy to spend exactly one of 960, 1440, 2160 triangles. It minimizes the policy’s predicted loss; actual perspective-mask loss is scored afterward.

Loss is mean per-object `1 − IoU`, then averaged across poses. A positive difference means silhouette-aware allocation had lower loss. Exact-spend matching is paired per pose, not on average.

| Cap | Comparator | Mean spend (comparator / silhouette) | Comparator loss | Silhouette loss | Shared-cap gain | Exact-spend gain | Exact pairs | Different assignments |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 960 | ScreenSize | 960.0 / 960.0 | 2.446% | 2.191% | +0.255 pp | +0.255 pp | 36/36 | 36/36 |
| 960 | ViewAverage | 960.0 / 960.0 | 2.340% | 2.191% | +0.149 pp | +0.149 pp | 36/36 | 6/36 |
| 1440 | ScreenSize | 1440.0 / 1440.0 | 1.997% | 1.224% | +0.773 pp | +0.773 pp | 36/36 | 36/36 |
| 1440 | ViewAverage | 1440.0 / 1440.0 | 1.224% | 1.224% | +0.000 pp | +0.000 pp | 36/36 | 0/36 |
| 2160 | ScreenSize | 2160.0 / 2160.0 | 0.961% | 0.926% | +0.035 pp | +0.035 pp | 36/36 | 6/36 |
| 2160 | ViewAverage | 2160.0 / 2160.0 | 0.961% | 0.926% | +0.035 pp | +0.035 pp | 36/36 | 6/36 |

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
| 1440 | ScreenSize | grazing | +0.582 pp | 12/12 |
| 1440 | ScreenSize | orbit | +0.950 pp | 12/12 |
| 1440 | ViewAverage | dolly | +0.000 pp | 12/12 |
| 1440 | ViewAverage | grazing | +0.000 pp | 12/12 |
| 1440 | ViewAverage | orbit | +0.000 pp | 12/12 |
| 2160 | ScreenSize | dolly | +0.000 pp | 12/12 |
| 2160 | ScreenSize | grazing | +0.106 pp | 12/12 |
| 2160 | ScreenSize | orbit | +0.000 pp | 12/12 |
| 2160 | ViewAverage | dolly | +0.000 pp | 12/12 |
| 2160 | ViewAverage | grazing | +0.106 pp | 12/12 |
| 2160 | ViewAverage | orbit | +0.000 pp | 12/12 |

Descriptive pilot only: the same three meshes recur in every trace. Nearby poses are not independent samples, and the replay does not measure frame time, popping, or human recognition.
