# LOD transition replay

Unity `6000.6.9f1` · `Metal` · 1280×720 · screen-size exponent 2 · cap 6,400.
3 traces with 121 poses each, indexed at 60 poses per second. Trace resets are not counted.

For each LOD switch, the old and new assignments are rendered at the **same new camera pose**. Magnitude is summed XOR divided by summed union across visible part masks. An object-ID change counts once in the changed-pixel column. Invisible swaps count toward the switch rate and have zero magnitude. Median and p95 are over switch frames, not all frames.

| Layout | Policy | Swaps / part-second | Switch frames | Visible switch frames | Median / p95 magnitude | Median / p95 changed pixels | Worst pose (magnitude) |
|---|---|---:|---:|---:|---:|---:|---|
| separated | ScreenSize | 0.000 | 0 | 0 | — / — | — / — | — (—) |
| separated | ViewAverage | 0.000 | 0 | 0 | — / — | — / — | — (—) |
| separated | Silhouette | 0.250 | 6 | 6 | 0.525% / 1.029% | 166 / 218 | grazing/34 (1.029%) |
| clutter | ScreenSize | 0.125 | 2 | 2 | 6.676% / 7.262% | 1,610 / 2,004 | high-orbit/20 (7.262%) |
| clutter | ViewAverage | 0.000 | 0 | 0 | — / — | — / — | — (—) |
| clutter | Silhouette | 0.250 | 5 | 5 | 0.268% / 1.121% | 58 / 258 | grazing/60 (1.121%) |

This is a geometric transition metric, not a perceptual study or a frame-time benchmark.
