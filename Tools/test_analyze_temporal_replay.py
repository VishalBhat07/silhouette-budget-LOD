import unittest

from analyze_temporal_replay import summarize


class TemporalAnalysisTests(unittest.TestCase):
    def test_counts_invisible_mesh_changes_but_not_as_visible_pops(self):
        rows = [
            dict(trace="orbit", frame="0", policy="Silhouette", spent="20", switched_parts="0",
                 changed_id_pixels="0", weighted_error="0"),
            dict(trace="orbit", frame="1", policy="Silhouette", spent="20", switched_parts="2",
                 changed_id_pixels="0", weighted_error="0"),
            dict(trace="orbit", frame="2", policy="Silhouette", spent="20", switched_parts="1",
                 changed_id_pixels="32", weighted_error=".02"),
        ]
        stats = summarize(rows, 2, 2)["Silhouette"]
        self.assertEqual(stats["switch_frames"], 2)
        self.assertEqual(stats["visible_frames"], 1)
        self.assertEqual(stats["switch_rate"], 1.5)
        self.assertEqual(stats["worst_frame"], "orbit/2")
