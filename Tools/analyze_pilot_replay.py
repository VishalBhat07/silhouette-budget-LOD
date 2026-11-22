#!/usr/bin/env python3
"""Score paired pilot poses and separate shared-cap results from exact-spend matches."""

import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path
from statistics import mean

POLICIES = {"ScreenSize", "Silhouette", "ViewAverage"}


def analyze(rows, min_pixels):
    data = defaultdict(dict)
    for row in rows:
        pose = (int(row["cap"]), row["trace"], int(row["frame"]))
        key = (row["policy"], row["part_id"])
        if key in data[pose]:
            raise ValueError(f"Duplicate observation {pose}/{key}")
        data[pose][key] = row
    if not data:
        raise ValueError("Empty replay")

    pairs = defaultdict(lambda: defaultdict(list))
    for (cap, trace, frame), observations in sorted(data.items()):
        parts = {part for _, part in observations}
        if observations.keys() != {(policy, part) for policy in POLICIES for part in parts}:
            raise ValueError(f"Missing policy or part at {trace}/{frame}/{cap}")
        counts = {}
        scores = {}
        reference_sizes = {}
        for policy in sorted(POLICIES):
            selected = [observations[policy, part] for part in sorted(parts)]
            spent = int(selected[0]["spent"])
            if (spent > cap or any(int(row["spent"]) != spent for row in selected)
                    or sum(int(row["triangles"]) for row in selected) != spent):
                raise ValueError(f"Invalid triangle spend at {trace}/{frame}/{cap}/{policy}")
            counts[policy] = spent
            errors = []
            for row in selected:
                ref, cand, overlap, union = (int(row[key]) for key in
                    ("reference_pixels", "candidate_pixels", "intersection_pixels", "union_pixels"))
                if not 0 <= overlap <= min(ref, cand) or union != ref + cand - overlap:
                    raise ValueError(f"Invalid mask counts at {trace}/{frame}/{row['part_id']}")
                part = row["part_id"]
                if reference_sizes.setdefault(part, ref) != ref:
                    raise ValueError(f"Reference mismatch at {trace}/{frame}/{part}")
                if ref >= min_pixels:
                    errors.append(1 - overlap / union)
            if not errors:
                raise ValueError(f"No eligible reference parts at {trace}/{frame}")
            scores[policy] = mean(errors)

        for baseline in ("ScreenSize", "ViewAverage"):
            pairs[cap][baseline].append({
                "trace": trace,
                "baseline": scores[baseline],
                "silhouette": scores["Silhouette"],
                "delta": scores[baseline] - scores["Silhouette"],
                "baseline_spent": counts[baseline],
                "silhouette_spent": counts["Silhouette"],
                "exact": counts[baseline] == counts["Silhouette"],
                "differs": any(observations[baseline, part]["triangles"] !=
                               observations["Silhouette", part]["triangles"] for part in parts),
            })

    def summary(samples):
        exact = [row for row in samples if row["exact"]]
        return {
            "baseline_mean": mean(row["baseline"] for row in samples),
            "silhouette_mean": mean(row["silhouette"] for row in samples),
            "baseline_spent": mean(row["baseline_spent"] for row in samples),
            "silhouette_spent": mean(row["silhouette_spent"] for row in samples),
            "all_count": len(samples),
            "all_delta": mean(row["delta"] for row in samples),
            "exact_count": len(exact),
            "exact_delta": mean(row["delta"] for row in exact) if exact else None,
            "different_allocations": sum(row["differs"] for row in samples),
        }

    return {
        cap: {
            baseline: {
                **summary(samples),
                "by_trace": {
                    trace: summary([s for s in samples if s["trace"] == trace])
                    for trace in sorted({s["trace"] for s in samples})
                },
            }
            for baseline, samples in baselines.items()
        }
        for cap, baselines in pairs.items()
    }


def render_report(summary, config, run, angles):
    lines = [
        "# Pilot perspective replay",
        "",
        f"Unity `{run['unityVersion']}` · `{run['graphicsApi']}` · "
        f"{config['width']}×{config['height']} · screen-size exponent {run['screenSizeExponent']}.",
        f"{len(config['traces'])} traces × {config['samplesPerTrace']} poses; "
        f"nearest baked view was {min(angles):.2f}–{max(angles):.2f}° away.",
        "",
        "Loss is mean per-object `1 − IoU`, then averaged across poses. A positive "
        "difference means silhouette-aware allocation had lower loss. "
        "Exact-spend matching is paired per pose, not on average.",
        "",
        "| Cap | Comparator | Mean spend (comparator / silhouette) | Comparator loss | Silhouette loss | Shared-cap gain | Exact-spend gain | Exact pairs | Different assignments |",
        "|---:|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    pp = lambda n: "—" if n is None else f"{n * 100:+.3f} pp"
    for cap, baselines in summary.items():
        for name, row in baselines.items():
            lines.append(f"| {cap} | {name} | {row['baseline_spent']:.1f} / {row['silhouette_spent']:.1f} | "
                         f"{row['baseline_mean']:.3%} | {row['silhouette_mean']:.3%} | "
                         f"{pp(row['all_delta'])} | {pp(row['exact_delta'])} | "
                         f"{row['exact_count']}/{row['all_count']} | {row['different_allocations']}/{row['all_count']} |")
    lines.extend(["", "## Exact-spend gain by trace", "",
                  "| Cap | Comparator | Trace | Gain | Exact pairs |", "|---:|---|---|---:|---:|"])
    for cap, baselines in summary.items():
        for name, row in baselines.items():
            for trace, stats in row["by_trace"].items():
                lines.append(f"| {cap} | {name} | {trace} | {pp(stats['exact_delta'])} | "
                             f"{stats['exact_count']}/{stats['all_count']} |")
    lines.extend(["", "Descriptive pilot only: the same three meshes recur in every trace. "
                  "Nearby poses are not independent samples, and the replay does not measure frame time, "
                  "popping, or human recognition.", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, default=Path("Results/pilot-replay"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    config = json.loads((args.run / "config.json").read_text())
    run = json.loads((args.run / "run.json").read_text())
    with (args.run / "observations.csv").open(newline="") as stream:
        rows = list(csv.DictReader(stream))
    with (args.run / "poses.csv").open(newline="") as stream:
        poses = [(row["trace"], int(row["frame"])) for row in csv.DictReader(stream)]
    expected = {(trace["id"], i) for trace in config["traces"] for i in range(config["samplesPerTrace"])}
    scored = {(row["trace"], int(row["frame"]), int(row["cap"])) for row in rows}
    if set(poses) != expected or len(poses) != len(expected) or scored != {
            (trace, frame, cap) for trace, frame in expected for cap in config["caps"]}:
        raise ValueError("Incomplete or inconsistent replay traces")
    if any({row["part_id"] for row in rows if (row["trace"], int(row["frame"]), int(row["cap"])) == pose}
           != {"gear", "washer", "toolbox"} for pose in scored):
        raise ValueError("Expected all three pilot parts at each pose and cap")
    angles = [float(row["nearest_bake_angle_deg"]) for row in rows]
    if min(angles) < .05:
        raise ValueError("A view is not held out from the bake")
    report = render_report(analyze(rows, config["minimumReferencePixels"]), config, run, angles)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(report)
    else:
        print(report, end="")


if __name__ == "__main__":
    main()
