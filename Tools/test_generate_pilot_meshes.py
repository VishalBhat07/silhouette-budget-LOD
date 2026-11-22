import csv
import tempfile
import unittest
from collections import Counter
from pathlib import Path

import numpy as np

import generate_pilot_meshes as pilot


class PilotMeshTests(unittest.TestCase):
    def test_reference_meshes_are_closed_and_outward_wound(self):
        for name, mesh in pilot.references().items():
            with self.subTest(part=name):
                edges = Counter()
                for a, b, c in mesh.faces.tolist():
                    for start, end in ((a, b), (b, c), (c, a)):
                        edges[start, end] += 1
                self.assertTrue(all(n == 1 and edges[b, a] == 1 for (a, b), n in edges.items()))
                a, b, c = mesh.points[mesh.faces].transpose(1, 0, 2)
                self.assertGreater(np.einsum("ij,ij->", a, np.cross(b, c)), 0)
                self.assertAlmostEqual(mesh.points[:, 1].min(), 0)

    def test_same_simplification_preserves_shared_pivots_and_strict_cost_ladders(self):
        for name, reference in pilot.references().items():
            with self.subTest(part=name):
                chain = pilot.ladder(reference)
                counts = [len(m.faces) for m in chain]
                self.assertEqual(len(chain), 4)
                self.assertTrue(all(0 < a < b for a, b in zip(counts, counts[1:])), counts)
                self.assertIs(chain[-1], reference)
                center = (reference.points.min(0) + reference.points.max(0)) / 2
                size = np.ptp(reference.points, axis=0)
                for mesh in chain:
                    a, b, c = mesh.points[mesh.faces].transpose(1, 0, 2)
                    self.assertTrue(np.all(np.linalg.norm(np.cross(b - a, c - a), axis=1) > 1e-9))
                    # No per-LOD normalization/recentering. Tolerate small collapse drift.
                    low, high = mesh.points.min(0), mesh.points.max(0)
                    self.assertTrue(np.all(np.abs((low + high) / 2 - center) <= size * 0.10))
                    self.assertTrue(np.all(np.abs(high - low - size) <= size * 0.25))

    def test_written_objs_and_manifest_are_reproducible(self):
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            for output in (first, second):
                pilot.generate(Path(output))
            files = sorted(p.relative_to(first) for p in Path(first).rglob("*") if p.is_file())
            with (Path(first) / "Experiments/pilot-meshes.csv").open(newline="") as stream:
                rows = list(csv.DictReader(stream))
            self.assertEqual(len(files), 13)
            self.assertEqual(len(rows), 12)
            for file in files:
                self.assertEqual((Path(first) / file).read_bytes(), (Path(second) / file).read_bytes())
            for row in rows:
                lines = (Path(first) / "SilhouetteWorkbench" / row["asset_path"]).read_text().splitlines()
                self.assertEqual(sum(line.startswith("f ") for line in lines), int(row["triangles"]))
