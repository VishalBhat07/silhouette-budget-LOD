# Native Unity LODGroup pilot

Unity `6000.6.9f1` · `Metal` · 1280×720 · 36 unseen poses.

Unity selected each mesh in a real URP render with level-coded IDs. The observed meshes were then scored using the same perspective ID-mask evaluator as the other policies. Cross-fades were off, LOD bias was 1, maximum LOD level was 0, and each group used its finest mesh's local bounds (center and largest dimension). The last threshold was zero; no whole-part culling was requested.

| Threshold scale | Fine-to-coarse thresholds | Trace | Mean triangles | Triangles min–max | Mean silhouette error | Poses |
|---:|---|---|---:|---:|---:|---:|
| 0.5× | 0.14, 0.08, 0.0425, 0 | all | 4,066.7 | 3,360–4,800 | 0.166% | 36 |
| 0.5× | 0.14, 0.08, 0.0425, 0 | dolly | 4,040.0 | 3,360–4,800 | 0.151% | 12 |
| 0.5× | 0.14, 0.08, 0.0425, 0 | grazing | 3,840.0 | 3,840–3,840 | 0.207% | 12 |
| 0.5× | 0.14, 0.08, 0.0425, 0 | orbit | 4,320.0 | 3,840–4,800 | 0.139% | 12 |
| 1× | 0.28, 0.16, 0.085, 0 | all | 2,360.0 | 1,680–3,360 | 0.810% | 36 |
| 1× | 0.28, 0.16, 0.085, 0 | dolly | 2,440.0 | 1,680–3,360 | 0.856% | 12 |
| 1× | 0.28, 0.16, 0.085, 0 | grazing | 1,880.0 | 1,680–1,920 | 0.887% | 12 |
| 1× | 0.28, 0.16, 0.085, 0 | orbit | 2,760.0 | 1,920–2,880 | 0.686% | 12 |
| 2× | 0.56, 0.32, 0.17, 0 | all | 1,013.3 | 960–1,440 | 2.398% | 36 |
| 2× | 0.56, 0.32, 0.17, 0 | dolly | 1,120.0 | 960–1,440 | 2.262% | 12 |
| 2× | 0.56, 0.32, 0.17, 0 | grazing | 960.0 | 960–960 | 2.500% | 12 |
| 2× | 0.56, 0.32, 0.17, 0 | orbit | 960.0 | 960–960 | 2.433% | 12 |

These are quality-versus-actual-cost points, not comparisons at the same budget. The thresholds were swept rather than picked after seeing the scores. This pilot does not measure time, account for render-pass triangle amplification, or establish generalization to new meshes.

## Same-pose, same-spend subset

Native poses are included only when their observed spend matches a target in the offline silhouette-aware exact-cost replay. This subset is not the full native sweep.

| Threshold scale | Matched poses | Native loss | Silhouette-aware exact-cost loss |
|---:|---:|---:|---:|
| 0.5× | 0/36 | — | — |
| 1× | 0/36 | — | — |
| 2× | 36/36 | 2.398% | 2.098% |
