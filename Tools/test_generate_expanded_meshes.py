import csv
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np

import generate_pilot_meshes as pilot


class ExpandedMeshTests(unittest.TestCase):
    def test_expansion_has_five_distinct_positive_triangle_ladders(self):
        parts = pilot.expanded_references()
        self.assertEqual(set(parts), {"hex_nut", "pulley", "slotted_bracket", "handwheel", "embossed_plate"})
        totals = set()
        for name, reference in parts.items():
            with self.subTest(part=name):
                self.assertAlmostEqual(reference.points[:, 1].min(), 0)
                chain = pilot.ladder(reference, aggressiveness=pilot.EXPANDED_AGGRESSIVENESS)
                sizes = [len(mesh.faces) for mesh in chain]
                self.assertEqual(len(sizes), 4)
                self.assertEqual(sorted(set(sizes)), sizes)
                self.assertGreater(sizes[0], 0)
                self.assertIs(chain[-1], reference)
                totals.add(sizes[-1])
                for mesh in chain:
                    a, b, c = mesh.points[mesh.faces].transpose(1, 0, 2)
                    self.assertTrue(np.all(np.linalg.norm(np.cross(b - a, c - a), axis=1) > 1e-9))
        self.assertGreater(len(totals), 2, "The expanded experiment must use heterogeneous costs.")

    def test_expansion_does_not_rewrite_any_original_pilot_mesh(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            pilot.generate(root)
            original = {p: p.read_bytes() for p in root.rglob("*") if p.is_file()}
            pilot.generate(root, expanded=True)
            self.assertTrue(all(p.read_bytes() == data for p, data in original.items()))
            with (root / "Experiments/expanded-meshes.csv").open(newline="") as stream:
                rows = list(csv.DictReader(stream))
            self.assertEqual(len(rows), 20)
            self.assertTrue(all((root / "SilhouetteWorkbench" / row["asset_path"]).is_file() for row in rows))

    def test_both_layouts_share_all_eight_parts_without_physical_intersections(self):
        root = Path(__file__).resolve().parents[1]
        config = json.loads((root / "Experiments/expanded-layouts.json").read_text())
        meshes = pilot.references() | pilot.expanded_references()
        self.assertEqual({p["id"] for p in config["parts"]}, meshes.keys())
        self.assertEqual({item["id"] for item in config["layouts"]}, {"separated", "clutter"})
        for layout in config["layouts"]:
            with self.subTest(layout=layout["id"]):
                self.assertEqual(len(layout["placements"]), 8)
                self.assertEqual({p["id"] for p in layout["placements"]}, meshes.keys())
                bounds = []
                for p in layout["placements"]:
                    x, _, z = meshes[p["id"]].points.T
                    yaw = np.radians(p["yaw"])
                    c, s = np.cos(yaw), np.sin(yaw)
                    world = np.array((p["x"] + x * c + z * s, p["z"] - x * s + z * c))
                    low, high = world.min(axis=1), world.max(axis=1)
                    self.assertTrue(np.all(low >= (-2, -.9)) and np.all(high <= (2, .9)))
                    for other_low, other_high in bounds:
                        self.assertFalse(np.all(np.maximum(low, other_low) < np.minimum(high, other_high)))
                    bounds.append((low, high))
