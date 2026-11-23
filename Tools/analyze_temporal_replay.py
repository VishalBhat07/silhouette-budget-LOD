#!/usr/bin/env python3
"""Report temporal mesh switches and their same-pose silhouette changes."""

import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path
from statistics import median

POLICIES = ("ScreenSize", "ViewAverage", "Silhouette")


def p95(values):
    ordered = sorted(values)
    return ordered[math.ceil(.95 * len(ordered)) - 1] if ordered else None


def summarize(rows, fps, parts):
    inputs = defaultdict(list)
    for row in rows:
        frame, switched, pixels = (int(row[k]) for k in ("frame", "switched_parts", "changed_id_pixels"))
        error = float(row["weighted_error"])
        if (not 0 <= switched <= parts or pixels < 0 or not 0 <= error <= 1 or
                (pixels == 0) != (error == 0) or (frame == 0 and switched) or (not switched and pixels)):
            raise ValueError("Invalid or unpaired transition")
        if frame > 0:
            inputs[row["policy"]].append((row["trace"], frame, switched, pixels, error))

    result = {}
    for policy, samples in inputs.items():
        switches = [row for row in samples if row[2]]
        worst = max(switches, key=lambda row: row[4], default=None)
        result[policy] = {
            "switch_rate": sum(row[2] for row in switches) * fps / (len(samples) * parts),
            "switch_frames": len(switches),
            "visible_frames": sum(row[3] > 0 for row in switches),
            "median_magnitude": median(row[4] for row in switches) if switches else None,
            "p95_magnitude": p95([row[4] for row in switches]),
            "median_pixels": median(row[3] for row in switches) if switches else None,
            "p95_pixels": p95([row[3] for row in switches]),
            "worst_frame": f"{worst[0]}/{worst[1]}" if worst and worst[4] > 0 else "—",
            "worst_magnitude": worst[4] if worst and worst[4] > 0 else None,
        }
    return result


def report(folder):
    lines = ["# LOD transition replay", ""]
    expected_config = None
    expected_environment = None
    for layout in ("separated", "clutter"):
        run = folder / layout
        config = json.loads((run / "config.json").read_text())
        unity, graphics, parts, exponent = (run / "run.txt").read_text().splitlines()
        environment = (unity, graphics, int(parts), exponent)
        with (run / "transitions.csv").open(newline="") as stream:
            rows = list(csv.DictReader(stream))
        expected = {(trace["id"], frame, policy) for trace in config["traces"]
                    for frame in range(config["framesPerTrace"]) for policy in POLICIES}
        measured = {(row["trace"], int(row["frame"]), row["policy"]) for row in rows}
        if measured != expected or len(rows) != len(expected) or any(
                not 0 < int(row["spent"]) <= config["triangleBudget"] for row in rows):
            raise ValueError(f"Incomplete or over-budget transitions in {layout}")
        if expected_config is None:
            expected_config = config
            expected_environment = environment
            lines.extend([
                f"Unity `{unity}` · `{graphics}` · {config['width']}×{config['height']} · "
                f"screen-size exponent {exponent} · cap {config['triangleBudget']:,}.",
                f"{len(config['traces'])} traces with {config['framesPerTrace']} poses each, indexed at "
                f"{config['framesPerSecond']} poses per second. Trace resets are not counted.",
                "",
                "For each LOD switch, the old and new assignments are rendered at the **same new camera pose**. "
                "Magnitude is summed XOR divided by summed union across visible part masks. An object-ID "
                "change counts once in the changed-pixel column. Invisible swaps count toward the switch "
                "rate and have zero magnitude. Median and p95 are over switch frames, not all frames.",
                "", "| Layout | Policy | Swaps / part-second | Switch frames | Visible switch frames | "
                "Median / p95 magnitude | Median / p95 changed pixels | Worst pose (magnitude) |",
                "|---|---|---:|---:|---:|---:|---:|---|",
            ])
        if config != expected_config or environment != expected_environment:
            raise ValueError("Layouts used different transition protocols or graphics settings")
        stats = summarize(rows, config["framesPerSecond"], int(parts))
        percent = lambda value: "—" if value is None else f"{value:.3%}"
        count = lambda value: "—" if value is None else f"{value:,.0f}"
        for policy in POLICIES:
            row = stats[policy]
            lines.append(f"| {layout} | {policy} | {row['switch_rate']:.3f} | {row['switch_frames']} | "
                         f"{row['visible_frames']} | {percent(row['median_magnitude'])} / "
                         f"{percent(row['p95_magnitude'])} | {count(row['median_pixels'])} / "
                         f"{count(row['p95_pixels'])} | {row['worst_frame']} "
                         f"({percent(row['worst_magnitude'])}) |")
    lines.extend(["", "This is a geometric transition metric, not a perceptual study or a frame-time benchmark.", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, default=Path("Results/temporal-replay"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    text = report(args.run)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    else:
        print(text, end="")


if __name__ == "__main__":
    main()
