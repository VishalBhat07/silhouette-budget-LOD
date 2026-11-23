#!/usr/bin/env python3
"""Summarize the non-development macOS player timings without merging repeats."""

import argparse
import csv
import gzip
import json
import math
from collections import defaultdict
from pathlib import Path
from statistics import median

from analyze_temporal_replay import POLICIES, p95

LAYOUTS = ("separated", "clutter")


def block_summary(values):
    return (median(values), p95(values)) if values else None


def across_repeats(blocks):
    stats = [s for values in blocks if (s := block_summary(values)) is not None]
    if not stats:
        return None
    medians, tails = zip(*stats)
    return dict(median=median(medians), p95=median(tails), repeat_min=min(medians),
                repeat_max=max(medians), valid_repeats=len(stats))


def rows(folder, name):
    path = folder / name
    stream = path.open(newline="") if path.is_file() else gzip.open(
        path.with_suffix(".csv.gz"), "rt", encoding="utf-8", newline="")
    with stream:
        return list(csv.DictReader(stream))


def report(folder):
    cfg = json.loads((folder / "config.json").read_text())
    env = json.loads((folder / "environment.json").read_text())
    if (env["development"] or env["vSync"] != 0 or env["targetFrameRate"] != -1 or
            (env["width"], env["height"]) != (cfg["width"], cfg["height"])):
        raise ValueError("Timing settings do not match the non-development, uncapped protocol")
    repeats = range(cfg["repeats"])
    poses = [(t["id"], frame) for t in cfg["traces"] for frame in range(cfg["framesPerTrace"])]
    scheduled = {(rep, layout, policy) for rep in repeats for layout in LAYOUTS for policy in POLICIES}
    data = defaultdict(list)
    observed_order = []
    previous = None
    for row in rows(folder, "frames.csv"):
        key = (int(row["repeat"]), row["layout"], row["policy"])
        if key != previous:
            observed_order.append(key)
            previous = key
        spent = int(row["spent"])
        values = tuple(float(row[metric]) for metric in ("frame_ms", "selector_ms", "swap_ms"))
        if (not 0 < spent <= cfg["triangleBudget"] or not all(math.isfinite(x) and x >= 0 for x in values)
                or values[0] == 0):
            raise ValueError(f"Invalid timing row: {row}")
        data[key].append(((row["trace"], int(row["frame"])), values, spent))
    order = ("ScreenSize", "Silhouette", "ViewAverage")
    expected_order = [(rep, LAYOUTS[layout], order[(slot + rep + layout) % len(order)])
                      for rep in repeats for position in range(len(LAYOUTS))
                      for layout in [(rep + position) % len(LAYOUTS)] for slot in range(len(order))]
    if data.keys() != scheduled or observed_order != expected_order or any(
            [row[0] for row in block] != poses for block in data.values()):
        raise ValueError("The timing runs don't share the specified interleaved block and pose order")
    for layout in LAYOUTS:
        for policy in POLICIES:
            spending = [[row[2] for row in data[rep, layout, policy]] for rep in repeats]
            if any(block != spending[0] for block in spending):
                raise ValueError("Repeated timing replays chose different triangle spend")

    gpu = defaultdict(list)
    for row in rows(folder, "gpu.csv"):
        key = (int(row["repeat"]), row["layout"], row["policy"])
        value = float(row["gpu_ms"])
        if key not in scheduled or not math.isfinite(value) or value <= 0:
            raise ValueError(f"Invalid GPU timing: {row}")
        gpu[key].append(value)
    power_path = folder / "power-source.txt"
    power = power_path.read_text().strip() if power_path.is_file() else "unrecorded"
    lines = [
        "# Standalone player timing", "",
        f"Unity `{env['unity']}` · {env['os']} · {env['device']} · {env['cpu']} / {env['gpu']} "
        f"(`{env['graphicsApi']}`).",
        f"Non-development windowed player, {env['width']}×{env['height']}; quality level {env['qualityLevel']}; "
        f"VSync off; target frame rate uncapped. Power source: {power}. Thermal state was not logged.", "",
        f"{len(poses)} rendered poses per block, {cfg['repeats']} interleaved repeats per layout and policy, "
        f"{cfg['warmupFrames']} unreported warm-up frames per block, shared cap {cfg['triangleBudget']:,}. "
        "No image readback, overlays or per-frame disk writes.",
        "",
        "Values are the median of per-block medians and the median of per-block p95s. "
        "The wall-frame time includes player scheduling; selector and mesh-apply times use CPU stopwatch "
        "intervals. GPU timings are delivered asynchronously and reported per block, not matched to poses.",
        "",
        "| Layout | Policy | Wall frame ms median / p95 | Repeat median range (ms) | "
        "Selection µs median / p95 | Apply µs median / p95 | GPU ms median / p95 | GPU samples |",
        "|---|---|---:|---:|---:|---:|---:|---:|",
    ]

    def formatted(value, scale):
        return ("Unavailable" if value is None else
                f"{value['median'] * scale:.3f} / {value['p95'] * scale:.3f}")

    for layout in LAYOUTS:
        for policy in POLICIES:
            frame, selector, swap = [
                across_repeats([[row[1][i] for row in data[rep, layout, policy]] for rep in repeats])
                for i in range(3)
            ]
            groups = [gpu[rep, layout, policy] for rep in repeats]
            gpu_stats = across_repeats(groups)
            lines.append(f"| {layout} | {policy} | {formatted(frame, 1)} | "
                         f"{frame['repeat_min']:.3f}–{frame['repeat_max']:.3f} | {formatted(selector, 1000)} | "
                         f"{formatted(swap, 1000)} | {formatted(gpu_stats, 1)} | "
                         f"{sum(map(len, groups)):,} ({gpu_stats['valid_repeats'] if gpu_stats else 0} repeats) |")
    lines.extend(["", "This is one host and a small workbench scene, at equal caps (not necessarily equal actual "
                  "spend). Do not read small frame-time differences as a hardware-independent speedup.", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, default=Path("Results/standalone/timing"))
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
