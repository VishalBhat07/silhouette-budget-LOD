import unittest

from analyze_standalone_timing import block_summary, across_repeats


class TimingAnalysisTests(unittest.TestCase):
    def test_missing_gpu_timings_are_not_zero(self):
        self.assertIsNone(block_summary([]))
        self.assertIsNone(across_repeats([[], []]))

    def test_repeats_not_sample_counts_determine_the_reported_median(self):
        result = across_repeats([[1] * 10, [2] * 4, [10] * 3])
        self.assertEqual(result["median"], 2)
        self.assertEqual(result["p95"], 2)
        self.assertEqual(result["repeat_min"], 1)
        self.assertEqual(result["repeat_max"], 10)
        self.assertEqual(result["valid_repeats"], 3)
