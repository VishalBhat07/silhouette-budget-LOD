# Eight-part layout replay

Unity `6000.6.9f1` · `Metal` · 1280×720 · screen-size exponent 2.
The two layouts share 36 unseen camera poses. Requested costs: 4064, 6400, 10300; matched reachable costs: 4064, 6400, 10298.

Mean error is per-part `1 − IoU`, averaged first over parts with at least 16 visible reference pixels and then over poses. Positive gains favor the silhouette-aware policy. Hidden objects are retained in the budget; their visibility is not fed to any policy.

## Exact-cost control

| Layout | Exact triangles | Screen loss | View-averaged loss | Directional loss | Gain vs. screen | Gain vs. view average |
|---|---:|---:|---:|---:|---:|---:|
| separated | 4,064 | 2.989% | 2.939% | 2.953% | +0.037 pp | -0.013 pp |
| separated | 6,400 | 2.911% | 0.902% | 0.902% | +2.009 pp | +0.000 pp |
| separated | 10,298 | 9.985% | 9.956% | 9.956% | +0.029 pp | +0.000 pp |
| clutter | 4,064 | 5.098% | 6.640% | 6.497% | -1.399 pp | +0.143 pp |
| clutter | 6,400 | 4.550% | 1.374% | 1.374% | +3.176 pp | +0.000 pp |
| clutter | 10,298 | 17.546% | 17.597% | 17.597% | -0.052 pp | +0.000 pp |

### Which assignments can spend exactly that much?

Requiring an exact total can force some parts to coarse levels even at a higher cost. Quality differences between rows are therefore not a monotonic budget curve.

| Exact triangles | Feasible assignments | Levels forced by cost alone |
|---:|---:|---|
| 4,064 | 3 | embossed_plate=L1, handwheel=L1, hex_nut=L1, pulley=L1, slotted_bracket=L0 |
| 6,400 | 56 | None |
| 10,298 | 3 | embossed_plate=L2, handwheel=L3, hex_nut=L0, pulley=L0, slotted_bracket=L0 |

### Exact-cost gains by trace

| Layout | Exact triangles | Trace | Gain vs. screen | Gain vs. view average |
|---|---:|---|---:|---:|
| separated | 4,064 | dolly | +0.005 pp | -0.007 pp |
| separated | 4,064 | grazing | +0.101 pp | -0.028 pp |
| separated | 4,064 | high-orbit | +0.004 pp | -0.006 pp |
| separated | 6,400 | dolly | +2.064 pp | +0.000 pp |
| separated | 6,400 | grazing | +2.360 pp | +0.000 pp |
| separated | 6,400 | high-orbit | +1.604 pp | +0.000 pp |
| separated | 10,298 | dolly | +0.031 pp | +0.000 pp |
| separated | 10,298 | grazing | +0.040 pp | +0.000 pp |
| separated | 10,298 | high-orbit | +0.017 pp | +0.000 pp |
| clutter | 4,064 | dolly | -0.676 pp | +0.000 pp |
| clutter | 4,064 | grazing | -2.443 pp | +0.429 pp |
| clutter | 4,064 | high-orbit | -1.078 pp | +0.000 pp |
| clutter | 6,400 | dolly | +2.885 pp | +0.000 pp |
| clutter | 6,400 | grazing | +3.935 pp | +0.000 pp |
| clutter | 6,400 | high-orbit | +2.709 pp | +0.000 pp |
| clutter | 10,298 | dolly | -0.043 pp | +0.000 pp |
| clutter | 10,298 | grazing | -0.016 pp | +0.000 pp |
| clutter | 10,298 | high-orbit | -0.096 pp | +0.000 pp |

## Runtime greedy allocation

| Layout | Cap | Comparator | Mean spend (comparator / directional) | Shared-cap gain | Matched gain | Exact pairs |
|---|---:|---|---:|---:|---:|---:|
| separated | 4,064 | ScreenSize | 4,052.0 / 3,904.0 | +6.972 pp | — | 0/36 |
| separated | 4,064 | ViewAverage | 3,904.0 / 3,904.0 | -0.013 pp | -0.013 pp | 36/36 |
| separated | 6,400 | ScreenSize | 6,352.0 / 6,276.7 | +2.666 pp | — | 0/36 |
| separated | 6,400 | ViewAverage | 6,248.0 / 6,276.7 | -0.042 pp | +0.000 pp | 13/36 |
| separated | 10,300 | ScreenSize | 10,242.0 / 9,989.3 | +0.902 pp | — | 0/36 |
| separated | 10,300 | ViewAverage | 10,000.0 / 9,989.3 | -0.001 pp | +0.000 pp | 32/36 |
| clutter | 4,064 | ScreenSize | 4,052.0 / 3,904.0 | +10.968 pp | — | 0/36 |
| clutter | 4,064 | ViewAverage | 3,904.0 / 3,904.0 | +0.153 pp | +0.153 pp | 36/36 |
| clutter | 6,400 | ScreenSize | 6,397.5 / 6,287.8 | +4.329 pp | — | 0/36 |
| clutter | 6,400 | ViewAverage | 6,248.0 / 6,287.8 | -0.308 pp | +0.000 pp | 5/36 |
| clutter | 10,300 | ScreenSize | 10,259.9 / 10,000.0 | +6.364 pp | — | 0/36 |
| clutter | 10,300 | ViewAverage | 10,000.0 / 10,000.0 | +0.000 pp | +0.000 pp | 36/36 |

## Occlusion cost in the exact-cost control

Hidden means fewer than 16 visible reference pixels, including fully occluded objects. Extra triangles are those above the coarsest mesh for those objects. False exposure counts candidate pixels only for objects with **zero** visible reference pixels.

| Layout | Exact triangles | Policy | Hidden parts / pose | Triangles on hidden parts / pose | Extra above coarsest / pose | False-exposure pixels / pose |
|---|---:|---|---:|---:|---:|---:|
| separated | 4,064 | ScreenSize | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 4,064 | ViewAverage | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 4,064 | Silhouette | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 6,400 | ScreenSize | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 6,400 | ViewAverage | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 6,400 | Silhouette | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 10,298 | ScreenSize | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 10,298 | ViewAverage | 0.00 | 0.0 | 0.0 | 0.0 |
| separated | 10,298 | Silhouette | 0.00 | 0.0 | 0.0 | 0.0 |
| clutter | 4,064 | ScreenSize | 1.67 | 600.0 | 200.0 | 0.1 |
| clutter | 4,064 | ViewAverage | 1.67 | 800.0 | 400.0 | 0.1 |
| clutter | 4,064 | Silhouette | 1.67 | 753.3 | 353.3 | 0.1 |
| clutter | 6,400 | ScreenSize | 1.67 | 1800.0 | 1400.0 | 0.0 |
| clutter | 6,400 | ViewAverage | 1.67 | 1600.0 | 1200.0 | 0.0 |
| clutter | 6,400 | Silhouette | 1.67 | 1600.0 | 1200.0 | 0.0 |
| clutter | 10,298 | ScreenSize | 1.67 | 1600.0 | 1200.0 | 0.0 |
| clutter | 10,298 | ViewAverage | 1.67 | 2400.0 | 2000.0 | 0.0 |
| clutter | 10,298 | Silhouette | 1.67 | 2400.0 | 2000.0 | 0.0 |

This is a held-out-view test on the same eight mesh ladders in two arrangements, not a test on unseen assets or independent human observations. Timing and temporal popping are not measured here. Native LODGroup results remain in the original three-part pilot.
