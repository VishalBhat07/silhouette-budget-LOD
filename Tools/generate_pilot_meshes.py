#!/usr/bin/env python3
"""Generate the original pilot or expanded parts and their OBJ triangle counts."""

import argparse
import csv
from dataclasses import dataclass
from math import cos, pi, sin, sqrt, tau
from pathlib import Path

import fast_simplification
import numpy as np


RATIOS = (0.125, 0.25, 0.5, 1.0)
EXPANDED_AGGRESSIVENESS = 8
PARTS = Path("Assets/Workbench/Parts")


@dataclass(frozen=True)
class Mesh:
    points: np.ndarray
    faces: np.ndarray


def join_loops(loops, *, tube=False):
    """Connect equally sized CCW (x, z) loops; a solid starts at the bottom."""
    n = len(loops[0])
    points = [point for loop in loops for point in loop]
    faces = []
    for i in range(len(loops) if tube else len(loops) - 1):
        lower, upper = i * n, ((i + 1) % len(loops)) * n
        for j in range(n):
            k = (j + 1) % n
            faces.extend(((lower + j, upper + j, upper + k), (lower + j, upper + k, lower + k)))
    if not tube:
        for ring, top in ((0, False), (len(loops) - 1, True)):
            center = len(points)
            points.append(tuple(np.mean(loops[ring], axis=0)))
            for j in range(n):
                a, b = ring * n + j, ring * n + (j + 1) % n
                faces.append((center, b, a) if top else (center, a, b))
    return Mesh(np.array(points, dtype=np.float64), np.array(faces, dtype=np.int32))


def ring_mesh(outline, bore, thickness, bevel, inner_radii=None):
    """Bored plate with chamfered outer and inner rims and a floor-level pivot."""
    n = len(outline)

    def ring(y, radii):
        return [(r * cos(a), y, r * sin(a)) for a, r in radii]

    inner = [(tau * i / n, bore if inner_radii is None else inner_radii[i]) for i in range(n)]
    inset = [(angle, radius - bevel) for angle, radius in outline]
    opened = [(angle, radius + bevel) for angle, radius in inner]
    return join_loops([
        ring(0, opened), ring(0, inset),
        ring(bevel, outline), ring(thickness - bevel, outline),
        ring(thickness, inset), ring(thickness, opened),
        ring(thickness - bevel, inner), ring(bevel, inner),
    ], tube=True)


def rounded_box(width, depth, low, high, radius, bevel, x=0, z=0):
    def loop(y, inset):
        hx, hz, r = width / 2 - inset, depth / 2 - inset, radius - inset
        corners = ((hx - r, hz - r), (-hx + r, hz - r),
                   (-hx + r, -hz + r), (hx - r, -hz + r))
        points = []
        for corner, (cx, cz) in enumerate(corners):
            for i in range(12):
                theta = (corner + i / 11) * pi / 2
                points.append((x + cx + r * cos(theta), y, z + cz + r * sin(theta)))
        return points

    return join_loops([loop(low, bevel), loop(low + bevel, 0),
                       loop(high - bevel, 0), loop(high, bevel)])


def combine(meshes):
    points, faces, offset = [], [], 0
    for mesh in meshes:
        points.append(mesh.points)
        faces.append(mesh.faces + offset)
        offset += len(mesh.points)
    return Mesh(np.concatenate(points), np.concatenate(faces))


def references():
    tooth = ((0, .255), (.20, .255), (.33, .310), (.67, .310), (.80, .255))
    gear = ring_mesh([(tau * (i + t) / 24, radius) for i in range(24) for t, radius in tooth],
                     bore=.103, thickness=.102, bevel=.007)
    washer = ring_mesh([(tau * i / 120, .214) for i in range(120)],
                       bore=.102, thickness=.050, bevel=.006)
    toolbox = combine([
        rounded_box(1.22, .57, 0, .300, .060, .027),
        rounded_box(1.234, .584, .3005, .408, .061, .016),
        rounded_box(.105, .105, .403, .506, .023, .010, x=-.192),
        rounded_box(.105, .105, .403, .506, .023, .010, x=.192),
        rounded_box(.481, .105, .493, .562, .035, .016),
    ])
    return {"gear": gear, "washer": washer, "toolbox": toolbox}


def expanded_references():
    def angles(n):
        return [tau * i / n for i in range(n)]

    hex_angles = angles(96)
    nut = ring_mesh([(a, .158 / cos((a + pi / 6) % (pi / 3) - pi / 6)) for a in hex_angles],
                    bore=.073, thickness=.136, bevel=.008)

    pulley_angles = angles(100)
    profile = ((0, .077), (0, .232), (.012, .244), (.055, .244), (.070, .178),
               (.137, .178), (.155, .244), (.195, .244), (.208, .232),
               (.208, .077), (.200, .070), (.009, .070))
    pulley = join_loops([[(r * cos(a), y, r * sin(a)) for a in pulley_angles] for y, r in profile], tube=True)

    def capsule_radius(angle, center=.132, radius=.041):
        x, z = abs(cos(angle)), abs(sin(angle))
        if z * center >= radius * x:
            return radius / z
        return center * x + sqrt(max(0, radius * radius - (center * z) ** 2))

    slot_angles = angles(108)
    slot_outline = [(a, min(.265 / max(abs(cos(a)), 1e-9), .133 / max(abs(sin(a)), 1e-9)))
                    for a in slot_angles]
    bracket = ring_mesh(slot_outline, 0, .073, .005, [capsule_radius(a) for a in slot_angles])

    wheel = ring_mesh([(a, .255) for a in angles(88)], bore=.217, thickness=.076, bevel=.008)
    hub = ring_mesh([(a, .087) for a in angles(64)], bore=.032, thickness=.088, bevel=.007)
    spoke = rounded_box(.195, .054, .011, .065, .016, .006, x=.144)
    spokes = []
    for angle in angles(5):
        x, y, z = spoke.points.T
        points = np.column_stack((x * cos(angle) - z * sin(angle), y, x * sin(angle) + z * cos(angle)))
        spokes.append(Mesh(points, spoke.faces))
    handwheel = combine([wheel, hub, *spokes])

    plate = combine([
        rounded_box(.558, .404, 0, .049, .037, .008),
        *(rounded_box(.030, depth, .047, .068, .008, .005, x=x)
          for x, depth in zip((-.148, -.074, 0, .074, .148), (.10, .18, .252, .18, .10))),
    ])
    return {"hex_nut": nut, "pulley": pulley, "slotted_bracket": bracket,
            "handwheel": handwheel, "embossed_plate": plate}


def ladder(reference, *, aggressiveness=7):
    meshes = []
    for ratio in RATIOS[:-1]:
        points, faces = fast_simplification.simplify(
            reference.points.copy(), reference.faces.copy(),
            target_count=round(ratio * len(reference.faces)), agg=aggressiveness,
        )
        meshes.append(Mesh(points, faces))
    return [*meshes, reference]


def save_obj(path, mesh):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="ascii", newline="\n") as stream:
        stream.write("# Original procedural geometry. See Tools/generate_pilot_meshes.py.\n")
        stream.write(f"o {path.stem}\n")
        for x, y, z in mesh.points:
            stream.write(f"v {x:.9f} {y:.9f} {z:.9f}\n")
        for a, b, c in mesh.faces.tolist():
            stream.write(f"f {a + 1} {b + 1} {c + 1}\n")


def generate(root, *, expanded=False):
    manifest = root / "Experiments" / ("expanded-meshes.csv" if expanded else "pilot-meshes.csv")
    manifest.parent.mkdir(parents=True, exist_ok=True)
    with manifest.open("w", newline="", encoding="ascii") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(("part_id", "level", "target_ratio", "triangles", "asset_path", "source"))
        for name, ref in (expanded_references() if expanded else references()).items():
            chain = ladder(ref, aggressiveness=EXPANDED_AGGRESSIVENESS if expanded else 7)
            for level, (ratio, mesh) in enumerate(zip(RATIOS, chain)):
                path = PARTS / name / f"{name}_level{level}.obj"
                save_obj(root / "SilhouetteWorkbench" / path, mesh)
                writer.writerow((name, level, ratio, len(mesh.faces), path.as_posix(), "original procedural"))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--expanded", action="store_true", help="Generate only the five additional parts.")
    args = parser.parse_args()
    generate(args.root, expanded=args.expanded)
