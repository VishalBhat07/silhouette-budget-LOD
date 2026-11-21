# Silhouette-Budget LOD: Unity implementation and research plan

> **For agentic workers:** Use the executing-plans skill for inline implementation, or subagent-driven-development if the project owner requests delegation. Track progress with the checkboxes.

**Goal:** Determine whether a cheap, view-dependent lookup can allocate a shared triangle budget across mechanical parts more effectively than screen size alone, and measure the price in runtime and visual stability.

**Architecture:** Build one desktop workbench with authored mesh levels. An editor-only baker measures each level's silhouette loss against its full-resolution mesh. A runtime controller chooses one level per object using only cached data and camera parameters. A separate benchmark renders the chosen meshes and measures actual errors on unseen views; it must never feed these measurements back to the controller.

**Tech stack:** Unity 6 LTS, C#, desktop URP, Blender for LOD generation, Unity Test Framework for deterministic logic, Python with NumPy, Pillow, SciPy and Matplotlib for offline analysis. Pin the exact installed editor, package, Blender and Python versions before collecting data. No headset required.

## Global constraints

- Start in a **new** Unity project named `SilhouetteWorkbench`. All implementation paths below are relative to that project. This plan does not depend on another Unity application's assets.
- Use one perspective camera, rigid objects, opaque triangle meshes, uniform object scale, hard LOD swaps and a fixed render resolution. Use the same source meshes, LOD chains, transforms, lighting, render settings and camera poses for every policy.
- Budget units are **selected mesh triangles in the workbench objects**, not draw calls, GPU time or all triangles drawn by the engine. Report these quantities separately. Keep the bench and background constant and exclude them from the allocation.
- Minimum deliverable: one desktop build, eight parts, four valid LODs per part, two main policies, one directional ablation, held-out replays and scripts that reproduce the figures from logs. Do not make cross-fades, custom mesh simplification or a user study dependencies of the first result.

---

## 1. Decide what would count as a result

**Primary hypothesis:** At comparable *actual* triangle counts, allocating detail by estimated silhouette damage reduces mean per-object silhouette error on unseen camera paths compared with allocating the same resources by screen size. The primary claim is about binary projected shape, including visible holes; it is **not** a claim about human recognition, total image quality or higher FPS.

**Secondary questions:** Does camera direction buy anything over knowing which objects are generally sensitive? How often do choices change? Is the per-frame selector cheap relative to the available frame time? Does an occluded gear receive triangles for teeth that nobody can see?

**Prior-art gate (before claiming novelty):** Read [Funkhouser & Séquin, adaptive allocation across objects](https://doi.org/10.1145/166117.166149), [Hoppe, view-dependent refinement](https://doi.org/10.1145/258734.258843), and [Garland & Heckbert, quadric mesh simplification](https://doi.org/10.1145/258734.258849). Search their citing papers for *silhouette-preserving LOD*, *image-based simplification metric*, *perceptual benefit/cost scheduling* and *multiview impostor/error tables*. In `Reports/related-work.csv`, record method, offline inputs, runtime signal, budget model, temporal metric, datasets, and difference from this experiment. The greedy rule and view-dependent LOD are not new. A plausible contribution is a measured accuracy–cost–stability trade-off for a compact lookup on mechanical assets, together with a reproducible benchmark. If a close method exists, reproduce its simplest comparable variant or narrow the claim.

**Pilot decision:** First build only a gear, washer and toolbox. On three unseen still views, inspect full-resolution, coarse and selected masks. Proceed when the coarse meshes genuinely change at least one outline, the two policies make different decisions at an interior budget, and the evaluator detects the change. If the policies differ but silhouette selection loses, keep that result and diagnose it; do not replace difficult objects to obtain a win.

## 2. Project map and data contracts

| Path | Responsibility |
|---|---|
| `Assets/Workbench/Scenes/Inspection.unity` | Fixed workbench, registered objects, camera and replay controller |
| `Assets/Workbench/Parts/<id>/` | Meshes, prefab, measured counts, bounding sphere and baked error table |
| `Assets/Workbench/Runtime/PartLods.cs` | Asset record: stable ID, meshes coarse → fine, counts, local center/radius, directions and errors |
| `Assets/Workbench/Editor/ValidateParts.cs` | Asset validation and generation of the mesh manifest |
| `Assets/Workbench/Editor/BakeSilhouettes.cs` | Offline orthographic mask capture and error-table generation |
| `Assets/Workbench/Shaders/ObjectId.shader` | Unlit opaque ID/mask pass shared by baker and evaluator |
| `Assets/Workbench/Runtime/LodSelector.cs` | Pure selection logic; no scene mutation or rendering |
| `Assets/Workbench/Runtime/WorkbenchController.cs` | Visibility, projections, policy inputs, mesh swaps and demo controls |
| `Assets/Workbench/Runtime/ReplayRunner.cs` | Pose playback, reference/candidate capture, performance logging |
| `Assets/Workbench/Tests/Editor/` | Logic fixtures for mask math, view lookup, costs, deterministic selection |
| `Tools/analyze.py`, `Tools/test_analysis.py` | Decode images, score frames, pair runs, make plots; independent of baker |
| `Experiments/` | Versioned `manifest.csv`, `config.json`, camera traces, splits and run instructions |
| `Results/<run-id>/`, `Reports/` | Generated captures/logs; curated tables, figures, limitations and draft |

Data contracts:

```text
Part: id, meshes[L], triangles[L], localCenter, localRadius,
      directions[V] (unit vectors, object→camera, local frame),
      errors[V,L] (unitless; full-res level = 0 error), sourceHash, bakeSettingsHash
Select(costs[N,L], losses[N,L], budget) → levels[N], spent, status
Camera pose CSV: trace, frame, time_s, px, py, pz, qx, qy, qz, qw, vertical_fov_deg
Frame CSV: run, mode, policy, trace, frame, cap, spent, active_count,
           upgrades_or_swaps, selector_ms, cpu_frame_ms, gpu_ms, gpu_timing_valid
Object CSV: run, trace, frame, part_id, level, triangles, projected_height_px,
            nearest_bake_angle_deg, predicted_loss, reference_visible_pixels
```

Join offline fields such as reference visibility after capture. Keep exact timing samples in their native frame order; if a timing API reports delayed results, log a sample ID or summarize by run rather than attaching the wrong GPU time to a pose.

## 3. Build milestones

### A. Make an asset ladder you can trust — week 1

- [ ] Create the scene and a manifest covering gear, washer, toolbox, hex nut, flanged pulley, slotted bracket, handwheel and embossed plate. Include thin outlines, holes, view-dependent features, flat silhouettes and a negative control. Record source/license per asset; procedural meshes you own simplify publication.
- [ ] In Blender, keep an untouched reference. Aim for approximately 12.5%, 25%, 50%, 100% of reference triangles, using one documented simplification procedure. Export or apply modifiers before measuring. Small meshes may need different ratios; use measured costs, never target ratios, in the selector. Do not intentionally destroy only the gear's teeth. Document feature loss even if the chosen simplifier preserves most teeth.
- [ ] Import every level in identical units with the same pivot/orientation. Use one mesh renderer and one opaque material per part in the primary scene. Match normals/materials where possible. Disable automatic LOD switching on manually managed renderers.
- [ ] Count triangles across triangle submeshes (index count divided by three). Reject empty meshes, non-triangle topology, non-increasing costs, mismatched pivots or stale hashes. Use the reference bounds for **all** LOD projections; using selected bounds creates feedback.
- [ ] Make two layouts: a separated, fully visible bench for the primary study; a cluttered bench with partial occlusion for stress testing. Persist transforms in the manifest. **Checkpoint:** switching all parts between finest and coarsest changes triangle counts without moving the parts. Save one annotated pilot close-up.

### B. Bake the silhouette predictor — weeks 2–3

- [ ] Implement the ID pass first. Black = background; white = isolated object when baking. Use linear, unlit output, no tone mapping, filtering, MSAA, TAA or compression. For scene evaluation, use distinct exact RGB codes for parts; black for non-study occluders that still write depth. Decode a round-trip fixture before trusting IDs.
- [ ] Generate 32 deterministic near-uniform sphere directions, e.g. `z = 1−2(i+0.5)/V`, `phi = i*pi*(3−sqrt(5))`, `d = (sqrt(1−z*z)*cos(phi), z, sqrt(1−z*z)*sin(phi))`. For each part and direction aim an orthographic camera at its reference center, with fixed margins covering its reference sphere and an alternate up axis near poles.
- [ ] Render 256×256 binary masks for the reference and each lower level with **identical** projection, clipping planes, culling rules and orientation. Do not crop each level to its own bounding box. For reference `R` and candidate `C`, store `e = count(R XOR C)/count(R OR C)` (one minus intersection-over-union). Count enclosed holes as background. Empty reference masks in the bake are an error.
- [ ] Test with identical masks (0), and a 100-pixel solid square with one interior pixel removed (0.01). Require finest-level error zero in every sampled direction. Save a contact sheet for gear front/edge, washer front/edge and plate front; visualize differences in color.
- [ ] At runtime transform `cameraPosition − worldReferenceCenter` into the part's local rotation frame, normalize it, and find the baked direction with largest dot product. Use nearest direction first; log its angular distance. Separately compute projected height from fixed reference bounds or a bounding sphere, using the same function for all policies. **Checkpoint:** rotate the part with the camera stationary; lookup must change in the expected direction.

### C. Implement allocation and the baselines — weeks 3–4

For the primary metric each visible object gets equal weight. Consequently the proposed runtime loss for object `i`, level `l`, is the unitless baked error `e[i,nearestView,l]`. Do **not** multiply it by screen area while claiming to optimize mean per-object error. Pixel-weighted results are a separate endpoint. The orthographic bake is a proxy for perspective performance; measure where that approximation fails.

- [ ] **Screen-only budgeted baseline:** use the same allocator but give it a screen-size loss proxy, e.g. `S(i,l) = (h_i/H)^p * (1 − triangles[i,l]/triangles[i,finest])`, where `h_i` is the projected reference height and `H` the viewport height. Choose `p` from `{1,2}` on development paths only by actual silhouette score; freeze it. This is a reproducible screen-only comparator, **not** Unity's built-in LODGroup.
- [ ] **Native Unity baseline:** separately configure ordinary LODGroups with shared relative-screen-height thresholds and hard transitions; prevent culling for visible test parts. Sweep thresholds on development data (e.g. multiply one fixed threshold vector by 0.5, 1 and 2). Disable the manual controller in this mode. Record actual selected triangles by duplicating and validating the configured threshold calculation against observed meshes/draws on boundary fixtures. Report native results as quality-versus-actual-cost curves, not as a hard-budget controller.
- [ ] **Directional ablation:** use the mean baked error across views for each object/level instead of the nearest-view entry. Same allocator, costs and cap. If this ties the proposed method, object identity is useful but directional lookup has not justified its extra cost.
- [ ] Implement the following deterministic greedy selector. Enumerate all higher levels, not just the next rung: a noisy lower rung may offer no gain even when a later one does.

```text
start all active objects at their coarsest level
if minimum cost > cap: return INFEASIBLE; never record as a successful budgeted frame
while true:
    examine each active object i and every level k finer than current level j
    extra = cost[i,k] - cost[i,j]; gain = loss[i,j] - loss[i,k]
    discard if extra exceeds remaining cap or gain <= 0
    choose maximum gain/extra; break ties by stable part ID then level index
    if no candidate fits: stop
    apply the chosen upgrade and subtract its extra cost
return levels and exact summed cost
```

- [ ] Unit fixture: costs `[[10,20,40],[10,20,40]]`, losses `[[.6,.2,0],[.4,.35,0]]`, cap `30` → levels `[1,0]`, spent `30`. Cap `19` → infeasible. Check ties, zero gains, an intermediate level worse than coarse, no active objects, and unused slack. Never describe greedy as globally optimal. On the three-part pilot, enumerate all `4³` assignments and log the greedy loss gap versus the best predicted assignment.
- [ ] Once per replay frame compute active objects using frustum intersection with **reference** bounds (do not remove occluded objects from proposed policy only). Select from scratch, update meshes only when a level changes, reuse arrays, and time selection separately from applying mesh swaps. **Checkpoint:** at identical pose/settings every replay produces identical level vectors and no feasible frame overspends.

### D. Make the experiment fair — week 5

- [ ] Create deterministic traces sampled at 60 poses/s: orbit, front-to-grazing sweep, dolly toward the gear, lateral move past several parts, and a slow move crossing LOD or lookup boundaries. Use separate development traces. Freeze a test set with at least three distinct paths and two layouts; never select parameters from the test frames. Store poses, not mouse recordings or wall-clock-driven motion.
- [ ] Bake on fixed directions, not trace frames. For a dedicated novel-view test, generate camera positions offset from baked directions and verify actual local directions per object; log the nearest-angle distribution and separately report near-sample and far-from-sample results. Include a close-up test to expose orthographic-versus-perspective mismatch.
- [ ] For the primary fixed-visible-set scene, compute absolute caps `Bmin + f*(Bmax−Bmin)` for `f ∈ {0.1,0.25,0.5,0.75}`, using summed coarsest/finest costs. Include all-coarse and all-fine anchors. Use identical caps for budgeted policies. On moving visibility stress traces, log active-set changes and any infeasible frames separately.
- [ ] **Equal cap is not equal spend.** Record per-frame slack. Plot achieved error against actual triangles. For direct paired comparisons predeclare, for example, `abs(spentA−spentB)/max(spentA,spentB) ≤ 0.02` on identical pose/layout. Report coverage and budget imbalance; do not generalize from a tiny matched subset. Also report all-frame equal-cap results as a deployment question.
- [ ] **Exact-cost control:** for the separated bench, run a small offline multiple-choice dynamic program. Starting from `{0: (0,[])}`, extend each reachable exact triangle sum by each next object's LOD, retaining only the lowest accumulated proxy loss for that sum and its choices. Run separately for screen and silhouette proxies. Evaluate the resulting actual masks at common reachable sums nearest the four target caps (choose sums before seeing image scores). This isolates the proxy at **identical triangle counts**, distinct from greedy runtime behavior. If the cost grid admits only one assignment at a chosen sum, report it as uninformative; select sums with multiple assignments or use a predeclared three-part controlled fixture with equal-cost ladders. Measure greedy separately.
- [ ] Lock test protocol, metrics, hashes and policy settings in `Experiments/config.json` before the final run. Keep quality runs and timing runs independent.

### E. Measure actual quality, popping and runtime — weeks 5–6

- [ ] **Quality pass:** at each test pose capture a full-resolution-mesh reference ID buffer and an ID buffer for each policy at the actual output resolution (start with 1280×720). From the ID images calculate per-visible-object `1−IoU`; average across objects, then frames within a trace. Label tiny references (e.g. fewer than 16 visible pixels) and report a sensitivity analysis with/without them. A fully hidden reference and candidate is not an error sample; a visible candidate with invisible reference is a false-exposure case, logged separately. For the primary separated layout require all parts visible.
- [ ] **Secondary metrics:** report area-weighted error `sum(XOR pixels)/sum(union pixels)` across object masks. For matched shaded captures, show both successes and the embossed plate: near-perfect masks can coexist with substantial lost surface detail. If reporting shaded image error, keep lighting, exposure, shadows and sampling deterministic; do not label it human preference.
- [ ] **Popping:** count swaps per object-second, but also measure magnitude. At each switch render the pre-switch and post-switch assignments **at the same new camera pose**. Their mask XOR divided by their union measures transition-induced image change without conflating camera motion. Report median and p95 magnitude and pixel counts; show a short replay of the worst switch. Evaluate optional hysteresis only as a separately labeled variant after primary results, with no budget overruns.
- [ ] **Timing pass:** standalone non-development build, fixed resolution and quality, VSync/frame cap disabled where supported; no ID captures, image writes, overlays or per-frame disk flushes. Warm up once and discard that run. Run each policy at least five times in interleaved order on the same machine, replaying the same frame indices. Report median and p95 frame time, selector time, and GPU time if supported. Record OS, GPU, CPU, graphics API, thermal/power conditions and timing availability; mark missing GPU timing unavailable, never zero. If GPU-bound improvements are too small to resolve, report quality at cost, not speedup.
- [ ] **Independent metric tests:** identical images = 0; missing visible object = 1 for that object; an unchanged mesh at a moving camera has no switch-induced pop; a completely covered object must contribute no visible mask. If buffered readbacks are used, encode frame IDs and test pose/image alignment before collecting data.
- [ ] **Storage:** begin with 120 sampled poses per trace for still-image evaluation, then evaluate every frame of shorter switch traces. Process one reference/candidate pair at a time; retain summaries plus representative masks, not all RGB renders. Benchmark timing on longer loops rather than timing these sampled captures.

### F. Analyze, reproduce and demonstrate — weeks 7–8

- [ ] Implement `python Tools/analyze.py --runs Results --config Experiments/config.json --out Reports` to reject mismatched settings/trace hashes, duplicate rows, missing reference frames, and overspending by budgeted policies before making figures. Run `python -m unittest discover -s Tools -p 'test_*.py'` for the metric fixtures. In Unity's Edit Mode Test Runner, run mask, lookup and allocator fixtures; then run a 120-pose smoke replay twice and compare decision CSVs, excluding timing columns.
- [ ] Produce: (1) silhouette error versus actual triangles with all policies, (2) paired error differences by part and trace, (3) popping versus quality, (4) CPU/GPU/selector timings, (5) annotated gear, occlusion and embossed-panel examples. Report bake duration and lookup bytes per asset in addition to runtime cost. For 32 directions × 4 levels × 4-byte floats, raw error scores are 512 bytes per asset, excluding direction vectors, mesh storage and engine overhead.
- [ ] Summarize the primary effect as `screen_error − proposed_error` (positive favors proposed). Show per-trace and per-part results, not only a grand average. Neighboring video frames are not independent samples. With few paths report paired differences and ranges descriptively; if estimating uncertainty, use sufficiently many independent paths/layouts with cluster resampling and state the sampling unit. Avoid claiming generalization to unseen *assets* when only camera poses were held out.
- [ ] Check the three likeliest explanations for a weak result: the view-averaged ablation already captures all benefit; perspective/occlusion breaks the isolated-mask predictor; or discrete LOD steps leave too much budget idle. Plot prediction versus measured error, split by viewpoint, visibility and actual spend. Publish negative cases.
- [ ] Deliver a desktop demo with pause/replay, keyboard policy toggle, budget slider, optional per-part LOD colors, and side-by-side captures at a **locked pose**. Display actual triangles, not merely the cap. Keep the demo HUD off in timing runs.
- [ ] Package `README.md` with exact editor version, build target, data licenses, bake steps, test instructions and analysis command. Archive frozen configuration, mesh hashes, trace CSVs, raw measurements and a compact sample of renders. A second operator should be able to regenerate one figure from logs without opening Unity.

## 4. Schedule, stop rules and paper outline

| Milestone | Acceptance gate | If behind |
|---|---|---|
| End week 1 | Three-part pilot meshes and aligned LOD swaps | Use procedural parts rather than searching asset stores |
| End week 3 | Cached errors agree with independently inspected masks; lookup responds to object rotation | Keep 32 directions and nearest lookup; skip interpolation |
| End week 4 | Eight assets, two scored policies and ablation; known allocator fixture passes; pilot makes distinct choices | Drop decorative scene art, retain baselines |
| End week 5 | Frozen traces, caps and metric definitions; exact-cost control verified on a small fixture | Reduce trace length before reducing experimental controls |
| End week 6 | Matched image results, switch diagnostics and standalone timing logs | Report GPU timing as unavailable if unsupported; keep CPU results |
| End week 8 | Rebuildable figures, honest limitations, desktop demo and draft | Omit extensions and present a scoped reproducibility study |

**Division for a team of three:** A owns assets and bake, B owns runtime selector and native baseline, C owns independent masks/metrics and replay design. Cross-check at the pilot: C computes one example error without the bake code; A verifies the native baseline actually renders the expected mesh; B verifies the replay manifest before timing. A solo student can follow A–F in order.

**Paper structure:** problem and comparison to prior work → assets and benchmark → error table and allocator → predeclared comparison protocol → quality/cost/stability results → failure cases, overhead and validity limits. A convincing first figure shows the same gear and toolbox from the same camera under matched triangle spend, with the full-resolution reference and a magnified difference overlay. Publication venue should follow the strength and novelty of the evidence, not precede it.

**Definition of done:** an independent reader can replay the same unseen camera path under both policies, verify the actual triangle counts, reproduce the comparison at an exact matched cost, and identify at least one condition where silhouette is an inadequate objective. A win for the proposed selector is not a completion criterion.
