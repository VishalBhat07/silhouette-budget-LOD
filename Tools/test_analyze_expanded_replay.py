import unittest

from analyze_expanded_replay import hidden_resources, lattice_constraints


class ExpandedReplayTests(unittest.TestCase):
    def test_hidden_spend_and_false_exposure_are_distinct_from_visible_loss(self):
        rows = [
            dict(trace="grazing", frame="0", cap="100", policy="Silhouette", part_id="gear",
                 triangles="40", reference_pixels="0", candidate_pixels="5"),
            dict(trace="grazing", frame="0", cap="100", policy="Silhouette", part_id="washer",
                 triangles="20", reference_pixels="10", candidate_pixels="12"),
            dict(trace="grazing", frame="0", cap="100", policy="Silhouette", part_id="toolbox",
                 triangles="40", reference_pixels="80", candidate_pixels="79"),
        ]
        group = hidden_resources(rows, 16, {"gear": 20, "washer": 10, "toolbox": 10})[100]["Silhouette"]
        self.assertEqual(group["hidden_triangles"], 60)
        self.assertEqual(group["hidden_upgrades"], 30)
        self.assertEqual(group["false_exposure_pixels"], 5)
        self.assertEqual(group["hidden_parts"], 2)

    def test_exact_sum_identifies_only_levels_forced_by_arithmetic(self):
        costs = {("gear", 0): 1, ("gear", 1): 2, ("washer", 0): 1, ("washer", 1): 2}
        result = lattice_constraints(costs, [2, 3, 7])
        self.assertEqual(result[2], {"assignments": 1, "fixed": {"gear": 0, "washer": 0}})
        self.assertEqual(result[3], {"assignments": 2, "fixed": {}})
        self.assertEqual(result[7], {"assignments": 0, "fixed": {}})
