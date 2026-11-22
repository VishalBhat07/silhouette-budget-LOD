import unittest

from analyze_native_lod import match_exact, summarize


def fixture():
    return [
        dict(trace="orbit", frame="0", threshold_scale="1", spent="1680", part_id=part,
             triangles=str(triangles), reference_pixels="100", candidate_pixels=str(100 - loss),
             intersection_pixels=str(100 - loss), union_pixels="100")
        for part, triangles, loss in (("gear", 480, 3), ("toolbox", 960, 1), ("washer", 240, 2))
    ]


class NativeLodAnalysisTests(unittest.TestCase):
    def test_reports_measured_spend_and_mean_per_part_error(self):
        result = summarize(fixture(), 16)[1.0]["all"]
        self.assertEqual(result["mean_triangles"], 1680)
        self.assertAlmostEqual(result["mean_loss"], .02)
        self.assertEqual(result["poses"], 1)

    def test_rejects_missing_parts_and_incorrect_spend(self):
        with self.assertRaises(ValueError):
            summarize(fixture()[1:], 16)
        rows = fixture()
        rows[0]["spent"] = "1440"
        with self.assertRaises(ValueError):
            summarize(rows, 16)

    def test_compares_same_pose_and_spend_only(self):
        native = fixture()
        exact = [dict(row, cap="1680", policy="Silhouette", intersection_pixels="100",
                      candidate_pixels="100") for row in native]
        perfect = match_exact(native, exact)[1.0]
        self.assertEqual(perfect["matched"], 1)
        self.assertAlmostEqual(perfect["native_loss"], .02)
        self.assertEqual(perfect["silhouette_loss"], 0.0)
        self.assertEqual(match_exact(native, [dict(row, frame="1") for row in exact])[1.0]["matched"], 0)
