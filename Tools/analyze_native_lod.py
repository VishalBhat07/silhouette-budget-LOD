#!/usr/bin/env python3
"""Report the measured quality and triangle spend of Unity's native LODGroup."""

import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path
from statistics import mean

from analyze_pilot_replay import analyze

PARTS = {"gear", "toolbox", "washer"}


def summarize(rows, min_pixels):
    poses = defaultdict(dict)
    for row in rows:
        key = (float(row["threshold_scale"]), row["trace"], int(row["frame"]))
        part = row["part_id"]
        if part in poses[key]:
            raise ValueError(f"Duplicate native observation: {key}/{part}")
        poses[key][part] = row
    if not poses:
        raise ValueError("Empty native replay")

    scales = defaultdict(list)
    reference_sizes = {}
    for (scale, trace, frame), parts in sorted(poses.items()):
        if parts.keys() != PARTS:
            raise ValueError(f"Missing or unexpected parts at {scale}/{trace}/{frame}")
        spend = sum(int(row["triangles"]) for row in parts.values())
        errors = []
        for part, row in parts.items():
            if spend != int(row["spent"]):
                raise ValueError(f"Triangle spend mismatch at {scale}/{trace}/{frame}")
            ref, candidate, overlap, union = (int(row[key]) for key in
                ("reference_pixels", "candidate_pixels", "intersection_pixels", "union_pixels"))
            if (ref < min_pixels or not 0 <= overlap <= min(ref, candidate) or
                    union != ref + candidate - overlap):
                raise ValueError(f"Invalid mask counts at {scale}/{trace}/{frame}/{part}")
            if reference_sizes.setdefault((trace, frame, part), ref) != ref:
                raise ValueError(f"Reference changed with thresholds at {trace}/{frame}/{part}")
            errors.append(1 - overlap / union)
        scales[scale].append((trace, spend, mean(errors)))

    def stats(poses):
        triangles = [item[1] for item in poses]
        return dict(poses=len(poses), mean_triangles=mean(triangles), min_triangles=min(triangles),
                    max_triangles=max(triangles), mean_loss=mean(item[2] for item in poses))

    return {
        scale: {
            "all": stats(poses),
            **{trace: stats([pose for pose in poses if pose[0] == trace])
               for trace in sorted({pose[0] for pose in poses})},
        }
        for scale, poses in scales.items()
    }


def match_exact(native_rows, exact_rows):
    exact = {(row["trace"], row["frame"], int(row["spent"]), row["part_id"]): row for row in exact_rows
             if row["policy"] == "Silhouette" and row["cap"] == row["spent"]}
    coverage = defaultdict(set)
    matched = defaultdict(lambda: defaultdict(list))
    for row in native_rows:
        scale = float(row["threshold_scale"])
        pose = (row["trace"], row["frame"])
        coverage[scale].add(pose)
        other = exact.get((*pose, int(row["spent"]), row["part_id"]))
        if other is not None:
            if row["reference_pixels"] != other["reference_pixels"]:
                raise ValueError(f"Reference mismatch at {pose}/{row['part_id']}")
            matched[scale][pose].append(tuple(1 - int(r["intersection_pixels"]) / int(r["union_pixels"])
                                                for r in (row, other)))
    result = {}
    for scale, poses in sorted(coverage.items()):
        pairs = [part_losses for part_losses in matched[scale].values() if len(part_losses) == len(PARTS)]
        result[scale] = {
            "matched": len(pairs), "poses": len(poses),
            "native_loss": mean(mean(pair[0] for pair in parts) for parts in pairs) if pairs else None,
            "silhouette_loss": mean(mean(pair[1] for pair in parts) for parts in pairs) if pairs else None,
        }
    return result


def render_report(results, replay, native, run, matched):
    lines = [
        "# Native Unity LODGroup pilot",
        "",
        f"Unity `{run['unityVersion']}` · `{run['graphicsApi']}` · "
        f"{replay['width']}×{replay['height']} · {len(replay['traces']) * replay['samplesPerTrace']} unseen poses.",
        "",
        "Unity selected each mesh in a real URP render with level-coded IDs. The observed meshes were then "
        "scored using the same perspective ID-mask evaluator as the other policies. Cross-fades were off, "
        "LOD bias was 1, maximum LOD level was 0, and each group used its finest mesh's local bounds "
        "(center and largest dimension). The last threshold was zero; no whole-part culling was requested.",
        "",
        "| Threshold scale | Fine-to-coarse thresholds | Trace | Mean triangles | Triangles min–max | Mean silhouette error | Poses |",
        "|---:|---|---|---:|---:|---:|---:|",
    ]
    for scale, by_trace in results.items():
        thresholds = ", ".join(f"{scale * t:g}" for t in native["finestToCoarsestThresholds"])
        for trace, row in by_trace.items():
            lines.append(f"| {scale:g}× | {thresholds} | {trace} | {row['mean_triangles']:,.1f} | "
                         f"{row['min_triangles']:,}–{row['max_triangles']:,} | {row['mean_loss']:.3%} | {row['poses']} |")

    lines.extend(["", "These are quality-versus-actual-cost points, not comparisons at the same budget. "
                  "The thresholds were swept rather than picked after seeing the scores. This pilot does not "
                  "measure time, account for render-pass triangle amplification, or establish generalization to new meshes.",
                  ""])
    if matched is not None:
        lines.extend(["## Same-pose, same-spend subset", "",
                      "Native poses are included only when their observed spend matches a target in the "
                      "offline silhouette-aware exact-cost replay. This subset is not the full native sweep.", "",
                      "| Threshold scale | Matched poses | Native loss | Silhouette-aware exact-cost loss |",
                      "|---:|---:|---:|---:|"])
        for scale, entry in matched.items():
            a, b = entry["native_loss"], entry["silhouette_loss"]
            losses = f"{a:.3%} | {b:.3%}" if a is not None else "— | —"
            lines.append(f"| {scale:g}× | {entry['matched']}/{entry['poses']} | {losses} |")
        lines.append("")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, default=Path("Results/pilot-native-replay"))
    parser.add_argument("--exact", type=Path, help="Optional same-pose exact-cost replay to pair")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    replay = json.loads((args.run / "config.json").read_text())
    native = json.loads((args.run / "native-config.json").read_text())
    run = json.loads((args.run / "run.json").read_text())
    with (args.run / "observations.csv").open(newline="") as stream:
        rows = list(csv.DictReader(stream))
    with (args.run / "poses.csv").open(newline="") as stream:
        poses = [(row["trace"], int(row["frame"])) for row in csv.DictReader(stream)]
    expected_poses = {(trace["id"], i) for trace in replay["traces"] for i in range(replay["samplesPerTrace"])}
    actual_cells = {(float(row["threshold_scale"]), row["trace"], int(row["frame"])) for row in rows}
    if (set(poses) != expected_poses or len(poses) != len(expected_poses) or
            actual_cells != {(s, t, f) for s in native["multipliers"] for t, f in expected_poses} or
            run.get("allocator") != "native-lodgroup" or any(row["policy"] != "NativeLODGroup" for row in rows)):
        raise ValueError("Invalid or incomplete native replay")
    matched = None
    if args.exact:
        exact_info = json.loads((args.exact / "run.json").read_text())
        if (exact_info.get("allocator") != "exact-cost-dp" or
                any(exact_info[k] != run[k] for k in ("unityVersion", "graphicsApi", "sceneDependencyHash")) or
                any((args.exact / file).read_bytes() != (args.run / file).read_bytes()
                    for file in ("poses.csv", "config.json"))):
            raise ValueError("Exact-cost replay does not share the native scene, poses and settings")
        with (args.exact / "observations.csv").open(newline="") as stream:
            exact_rows = list(csv.DictReader(stream))
        analyze(exact_rows, replay["minimumReferencePixels"])
        matched = match_exact(rows, exact_rows)
    report = render_report(summarize(rows, replay["minimumReferencePixels"]), replay, native, run, matched)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(report)
    else:
        print(report, end="")


if __name__ == "__main__":
    main()
