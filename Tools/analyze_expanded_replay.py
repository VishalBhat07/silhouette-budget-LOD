#!/usr/bin/env python3
"""Compare paired expanded-workbench replays and account for occluded parts separately."""

import argparse
import csv
import gzip
import json
from collections import defaultdict
from itertools import product
from pathlib import Path
from statistics import mean

from analyze_pilot_replay import analyze

ROOT = Path(__file__).resolve().parents[1]
POLICIES = ("ScreenSize", "ViewAverage", "Silhouette")
LAYOUTS = ("separated", "clutter")


def hidden_resources(rows, min_pixels, coarsest):
    frames = defaultdict(lambda: [0, 0, 0, 0])
    for row in rows:
        key = (int(row["cap"]), row["policy"], row["trace"], int(row["frame"]))
        values = frames[key]
        if int(row["reference_pixels"]) < min_pixels:
            chosen = int(row["triangles"])
            values[0] += chosen
            values[1] += chosen - coarsest[row["part_id"]]
            values[2] += int(row["candidate_pixels"]) if int(row["reference_pixels"]) == 0 else 0
            values[3] += 1
    groups = defaultdict(lambda: defaultdict(list))
    for (cap, policy, _, _), values in frames.items():
        groups[cap][policy].append(values)
    names = ("hidden_triangles", "hidden_upgrades", "false_exposure_pixels", "hidden_parts")
    return {
        cap: {policy: dict(zip(names, map(mean, zip(*values)))) for policy, values in policies.items()}
        for cap, policies in groups.items()
    }


def mesh_costs():
    result = {}
    for name in ("pilot-meshes.csv", "expanded-meshes.csv"):
        with (ROOT / "Experiments" / name).open(newline="") as stream:
            for row in csv.DictReader(stream):
                result[row["part_id"], int(row["level"])] = int(row["triangles"])
    return result


def lattice_constraints(costs, targets):
    parts = sorted({part for part, _ in costs})
    ladders = [sorted(level for name, level in costs if name == part) for part in parts]
    counts = dict.fromkeys(targets, 0)
    used = {target: defaultdict(set) for target in targets}
    for levels in product(*ladders):
        spend = sum(costs[key] for key in zip(parts, levels))
        if spend in counts:
            counts[spend] += 1
            for part, level in zip(parts, levels):
                used[spend][part].add(level)
    return {target: {
        "assignments": counts[target],
        "fixed": {part: next(iter(levels)) for part, levels in used[target].items() if len(levels) == 1},
    } for target in targets}


def read_rows(folder):
    csv_path = folder / "observations.csv"
    stream = csv_path.open(newline="") if csv_path.is_file() else gzip.open(
        folder / "observations.csv.gz", "rt", encoding="utf-8", newline="")
    with stream:
        return list(csv.DictReader(stream))


def read_run(folder, layout, solver, costs):
    config = json.loads((folder / "config.json").read_text())
    run = json.loads((folder / "run.json").read_text())
    if run["layout"] != layout or run["allocator"] != ("exact-cost-dp" if solver == "exact" else "greedy"):
        raise ValueError(f"Wrong allocator or layout in {folder}")
    with (folder / "poses.csv").open(newline="") as stream:
        poses = [(row["trace"], int(row["frame"])) for row in csv.DictReader(stream)]
    expected = {(t["id"], frame) for t in config["traces"] for frame in range(config["samplesPerTrace"])}
    if set(poses) != expected or len(poses) != len(expected):
        raise ValueError(f"Missing or duplicate camera poses in {folder}")

    rows = read_rows(folder)
    groups = defaultdict(set)
    refs = {}
    for row in rows:
        trace, frame, cap, part = row["trace"], int(row["frame"]), int(row["cap"]), row["part_id"]
        groups[trace, frame, cap].add(part)
        if costs[part, int(row["level"])] != int(row["triangles"]):
            raise ValueError(f"Unexpected cost for {part} in {folder}")
        if solver == "exact" and int(row["spent"]) != cap:
            raise ValueError(f"Budget was not exactly matched in {folder}")
        if float(row["nearest_bake_angle_deg"]) < .05:
            raise ValueError(f"The pose intersects a baked direction in {folder}")
        ref_key = (trace, frame, part)
        if refs.setdefault(ref_key, row["reference_pixels"]) != row["reference_pixels"]:
            raise ValueError(f"Full-resolution reference changed between policies or caps in {folder}")
    if (groups.keys() != {(t, f, cap) for t, f in expected for cap in run["effectiveCaps"]} or
            any(parts != set(config["partIds"]) for parts in groups.values())):
        raise ValueError(f"Missing or inconsistent part measurements in {folder}")
    with (folder / "config.json").open("rb") as stream:
        protocol = stream.read()
    return config, run, protocol, (folder / "poses.csv").read_bytes(), refs, rows


def report(folder):
    costs = mesh_costs()
    tables = {}
    hidden = {}
    protocol_and_poses = None
    environment = None
    exact_costs = None
    for layout in LAYOUTS:
        scene_and_reference = None
        for solver in ("greedy", "exact"):
            config, run, protocol, poses, refs, rows = read_run(folder / layout / solver, layout, solver, costs)
            inputs = (protocol, poses)
            platform = tuple(run[k] for k in ("unityVersion", "graphicsApi", "screenSizeExponent"))
            scene = (run["sceneDependencyHash"], refs)
            if protocol_and_poses is not None and inputs != protocol_and_poses:
                raise ValueError("The layouts do not share identical camera poses and protocol")
            if environment is not None and platform != environment:
                raise ValueError("The replays do not share the graphics backend and screen-size settings")
            if scene_and_reference is not None and scene != scene_and_reference:
                raise ValueError("Greedy and exact runs disagree on the scene or visible reference masks")
            if solver == "exact":
                if exact_costs is not None and run["effectiveCaps"] != exact_costs:
                    raise ValueError("The layouts do not share exact costs")
                exact_costs = run["effectiveCaps"]
            protocol_and_poses = inputs
            environment = platform
            scene_and_reference = scene
            limit = config["minimumReferencePixels"]
            tables[layout, solver] = analyze(rows, limit)
            hidden[layout, solver] = hidden_resources(rows, limit, {
                part: costs[part, 0] for part in config["partIds"]})

    points = lambda x: "—" if x is None else f"{x * 100:+.3f} pp"
    lines = [
        "# Eight-part layout replay", "",
        f"Unity `{environment[0]}` · `{environment[1]}` · {config['width']}×{config['height']} · "
        f"screen-size exponent {environment[2]}.",
        f"The two layouts share {len(config['traces']) * config['samplesPerTrace']} unseen camera poses. "
        f"Requested costs: {', '.join(map(str, config['caps']))}; "
        f"matched reachable costs: {', '.join(map(str, exact_costs))}.", "",
        f"Mean error is per-part `1 − IoU`, averaged first over parts with at least {limit} visible "
        "reference pixels and then over poses. Positive gains favor the silhouette-aware policy. "
        "Hidden objects are retained in the budget; their visibility is not fed to any policy.", "",
        "## Exact-cost control", "",
        "| Layout | Exact triangles | Screen loss | View-averaged loss | Directional loss | Gain vs. screen | Gain vs. view average |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for layout in LAYOUTS:
        for cap, pair in tables[layout, "exact"].items():
            s, a = pair["ScreenSize"], pair["ViewAverage"]
            lines.append(f"| {layout} | {cap:,} | {s['baseline_mean']:.3%} | {a['baseline_mean']:.3%} | "
                         f"{s['silhouette_mean']:.3%} | {points(s['exact_delta'])} | {points(a['exact_delta'])} |")
    lines.extend(["", "### Which assignments can spend exactly that much?", "",
                  "Requiring an exact total can force some parts to coarse levels even at a higher cost. "
                  "Quality differences between rows are therefore not a monotonic budget curve.", "",
                  "| Exact triangles | Feasible assignments | Levels forced by cost alone |",
                  "|---:|---:|---|"])
    for cap, data in lattice_constraints(costs, exact_costs).items():
        fixed = ", ".join(f"{part}=L{level}" for part, level in data["fixed"].items()) or "None"
        lines.append(f"| {cap:,} | {data['assignments']} | {fixed} |")
    lines.extend(["", "### Exact-cost gains by trace", "",
                  "| Layout | Exact triangles | Trace | Gain vs. screen | Gain vs. view average |",
                  "|---|---:|---|---:|---:|"])
    for layout in LAYOUTS:
        for cap, pair in tables[layout, "exact"].items():
            for trace, s in pair["ScreenSize"]["by_trace"].items():
                a = pair["ViewAverage"]["by_trace"][trace]
                lines.append(f"| {layout} | {cap:,} | {trace} | {points(s['exact_delta'])} | {points(a['exact_delta'])} |")
    lines.extend(["", "## Runtime greedy allocation", "",
                  "| Layout | Cap | Comparator | Mean spend (comparator / directional) | Shared-cap gain | Matched gain | Exact pairs |",
                  "|---|---:|---|---:|---:|---:|---:|"])
    for layout in LAYOUTS:
        for cap, pair in tables[layout, "greedy"].items():
            for policy, s in pair.items():
                lines.append(f"| {layout} | {cap:,} | {policy} | {s['baseline_spent']:,.1f} / "
                             f"{s['silhouette_spent']:,.1f} | {points(s['all_delta'])} | "
                             f"{points(s['exact_delta'])} | {s['exact_count']}/{s['all_count']} |")
    lines.extend(["", "## Occlusion cost in the exact-cost control", "",
                  f"Hidden means fewer than {limit} visible reference pixels, including fully occluded objects. "
                  "Extra triangles are those above the coarsest mesh for those objects. False exposure "
                  "counts candidate pixels only for objects with **zero** visible reference pixels.", "",
                  "| Layout | Exact triangles | Policy | Hidden parts / pose | Triangles on hidden parts / pose | "
                  "Extra above coarsest / pose | False-exposure pixels / pose |",
                  "|---|---:|---|---:|---:|---:|---:|"])
    for layout in LAYOUTS:
        for cap, data in sorted(hidden[layout, "exact"].items()):
            for policy in POLICIES:
                h = data[policy]
                lines.append(f"| {layout} | {cap:,} | {policy} | {h['hidden_parts']:.2f} | "
                             f"{h['hidden_triangles']:.1f} | {h['hidden_upgrades']:.1f} | "
                             f"{h['false_exposure_pixels']:.1f} |")
    lines.extend(["", "This is a held-out-view test on the same eight mesh ladders in two arrangements, "
                  "not a test on unseen assets or independent human observations. Timing and temporal popping "
                  "are not measured here. Native LODGroup results remain in the original three-part pilot.", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, default=Path("Results/expanded-replay"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    markdown = report(args.run)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(markdown, encoding="utf-8")
    else:
        print(markdown, end="")


if __name__ == "__main__":
    main()
