"""Offline regression tests for scripts/CreateNrkRssFeed.py.

For every podcast recorded under tests/fixtures/<pod>/ the script's create_feed() is
run against the saved NRK JSON (no network, no sleeping) and its output is compared
line by line with the .rss file that was current when the JSON was saved.

Record the JSON first (needs internet):  python tests/record_fixtures.py
Run the tests, from the repo root:       python -m unittest discover -s tests -v
"""
import unittest

import nrk_replay as nrk


recorded_pods = nrk.recorded_pods


class FeedMatchesRssFile(unittest.TestCase):
    """One test_<pod> method per recorded podcast is added below."""

    def test_fixtures_have_been_recorded(self):
        if not recorded_pods():
            self.skipTest(f"No fixtures in {nrk.FIXTURES_DIR}. Run: python tests/record_fixtures.py")


def _make_test(pod):
    def test(self):
        pod_dir = nrk.FIXTURES_DIR / pod
        expected = (pod_dir / nrk.EXPECTED_FILE).read_bytes().decode("utf-8")
        replay = nrk.ReplayNrk(pod_dir)

        actual = nrk.generate_feed(pod, replay)

        self.assertTrue(replay.requested, "the script made no requests")
        report = nrk.diff_report(expected, actual)
        if report is not None:
            self.fail(f"{pod}: {report}")

    test.__name__ = f"test_{pod}"
    return test


for _pod in recorded_pods():
    setattr(FeedMatchesRssFile, f"test_{_pod}", _make_test(_pod))


class ComparisonHelperTests(unittest.TestCase):
    """Guards against the line-by-line comparison being too lenient or too strict."""

    def test_line_endings_are_ignored(self):
        self.assertIsNone(nrk.diff_report("a\r\nb\r\n", "a\nb\n"))

    def test_trailing_whitespace_is_a_difference(self):
        self.assertIsNotNone(nrk.diff_report("a\nb", "a\nb "))

    def test_missing_final_newline_is_a_difference(self):
        self.assertIsNotNone(nrk.diff_report("a\nb\n", "a\nb"))

    def test_extra_blank_line_is_a_difference(self):
        self.assertIsNotNone(nrk.diff_report("a\nb", "a\n\nb"))

    def test_report_names_first_differing_line(self):
        self.assertIn("first difference at line 2", nrk.diff_report("a\nb\nc", "a\nX\nc"))


if __name__ == "__main__":
    unittest.main()
