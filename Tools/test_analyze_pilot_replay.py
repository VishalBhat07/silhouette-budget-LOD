import unittest

from analyze_pilot_replay import analyze


def fixture():
    rows = []
    for frame, trace in enumerate(("orbit", "dolly")):
        for policy, spent, xor in (
            ("ScreenSize", 960, 30),
            ("Silhouette", 960 if frame == 0 else 720, 10),
            ("ViewAverage", 960, 20),
        ):
            rows.append(dict(
                trace=trace, frame=str(frame), cap="960", policy=policy, spent=str(spent),
                part_id="gear", triangles=str(spent), reference_pixels="100",
                candidate_pixels=str(100 - xor), intersection_pixels=str(100 - xor),
                union_pixels="100", nearest_bake_angle_deg="5.00",
            ))
    return rows


class ReplayAnalysisTests(unittest.TestCase):
    def test_exact_spend_is_paired_by_pose_and_not_confused_with_cap(self):
        result = analyze(fixture(), min_pixels=16)
        screen = result[960]["ScreenSize"]
        self.assertEqual(screen["all_count"], 2)
        self.assertEqual(screen["exact_count"], 1)
        self.assertEqual(screen["baseline_spent"], 960)
        self.assertEqual(screen["silhouette_spent"], 840)
        self.assertAlmostEqual(screen["exact_delta"], .2)
        self.assertEqual(result[960]["ViewAverage"]["exact_count"], 1)

    def test_mismatched_or_missing_rows_are_rejected(self):
        rows = fixture()
        rows[0]["spent"] = "950"
        with self.assertRaises(ValueError):
            analyze(rows, min_pixels=16)
        with self.assertRaises(ValueError):
            analyze(fixture()[1:], min_pixels=16)
