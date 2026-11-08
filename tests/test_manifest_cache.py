"""Tests for the manifest cache in scripts/CreateNrkRssFeed.py, run against the recorded fixtures.

The cache must (a) save requests: a manifest is only fetched for episodes it does not know,
and (b) never change the output: the feed is still identical to expected.rss, and edits to
old episodes (which come from the episode pages, not the manifests) still show up.

Run from the repo root:  python -m unittest discover -s tests -v
"""
import json
import os
import re
import tempfile
import unittest

import nrk_replay as nrk

PODS = nrk.recorded_pods()


def expected_text(pod):
    return (nrk.FIXTURES_DIR / pod / nrk.EXPECTED_FILE).read_bytes().decode("utf-8")


def read_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def write_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f)


def episode_count(pod):
    return len(json.loads((nrk.FIXTURES_DIR / pod / nrk.MANIFESTS_FILE).read_text("utf-8")))


@unittest.skipUnless(PODS, f"No fixtures in {nrk.FIXTURES_DIR}. Run: python tests/record_fixtures.py")
class ManifestCacheTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.tmp = tmp.name

    def cache_path(self, pod):
        return os.path.join(self.tmp, ".cache", f"{pod}.json")

    def run_feed(self, pod, handler=None):
        """One 'run' of the script: load the cache file, build the feed, save the cache."""
        replay = nrk.ReplayNrk(nrk.FIXTURES_DIR / pod)
        cache = nrk.load_script().ManifestCache(self.cache_path(pod))
        feed = nrk.generate_feed(pod, handler or replay, cache=cache)
        cache.save()
        return feed, replay

    def assert_matches_expected(self, pod, feed):
        report = nrk.diff_report(expected_text(pod), feed)
        if report is not None:
            self.fail(f"{pod}: {report}")

    def test_second_run_fetches_no_manifests_and_gives_identical_output(self):
        for pod in PODS:
            with self.subTest(pod=pod):
                cold_feed, cold = self.run_feed(pod)
                self.assertEqual(cold.manifest_requests, episode_count(pod), "cold run should fetch every manifest")
                self.assert_matches_expected(pod, cold_feed)

                warm_feed, warm = self.run_feed(pod)
                self.assertEqual(warm.manifest_requests, 0, "warm run should not fetch any manifest")
                self.assertGreater(len(warm.requested), 0, "episode pages must still be fetched")
                self.assert_matches_expected(pod, warm_feed)

    def test_only_missing_episodes_are_fetched(self):
        pod = min(PODS, key=episode_count)
        self.run_feed(pod)

        path = self.cache_path(pod)
        urls = read_json(path)
        dropped = sorted(urls)[0]
        del urls[dropped]
        write_json(path, urls)

        feed, replay = self.run_feed(pod)

        self.assertEqual(replay.manifest_requests, 1)
        self.assertIn(dropped, replay.requested[-1] if replay.manifest_requests else "")
        self.assert_matches_expected(pod, feed)
        self.assertIn(dropped, read_json(path), "the fetched url should be cached again")

    def test_edited_old_episode_shows_up_without_any_manifest_request(self):
        pod = min(PODS, key=episode_count)
        self.run_feed(pod)                                   # fill the cache

        replay = nrk.ReplayNrk(nrk.FIXTURES_DIR / pod)

        def nrk_fixes_a_description(url, session=None, **kwargs):
            response = replay(url, session=session, **kwargs)
            kind, number = nrk.classify_url(url)
            if kind == "page" and number == 1:
                data = json.loads(response.content)
                data["_embedded"]["episodes"][0]["titles"]["subtitle"] = "EDITED BY NRK"
                return nrk.FakeResponse(json.dumps(data, ensure_ascii=False).encode("utf-8"), url)
            return response

        cache = nrk.load_script().ManifestCache(self.cache_path(pod))
        feed = nrk.generate_feed(pod, nrk_fixes_a_description, cache=cache)

        expected = expected_text(pod)
        first_item = expected.index("<item>")
        edited_expected = expected[:first_item] + re.sub(
            r"<description>.*?</description>", lambda m: "<description>EDITED BY NRK</description>",
            expected[first_item:], count=1, flags=re.S)
        self.assertEqual(replay.manifest_requests, 0)
        self.assertNotEqual(edited_expected, expected)
        self.assertIsNone(nrk.diff_report(edited_expected, feed))

    def test_unreadable_cache_file_is_ignored_and_rewritten(self):
        pod = min(PODS, key=episode_count)
        os.makedirs(os.path.dirname(self.cache_path(pod)))
        with open(self.cache_path(pod), "w", encoding="utf-8") as f:
            f.write("{ this is not json")

        feed, replay = self.run_feed(pod)

        self.assertEqual(replay.manifest_requests, episode_count(pod))
        self.assert_matches_expected(pod, feed)
        self.assertEqual(len(read_json(self.cache_path(pod))), episode_count(pod))

    def test_cache_file_is_sorted_with_one_episode_per_line(self):
        pod = min(PODS, key=episode_count)
        self.run_feed(pod)

        with open(self.cache_path(pod), encoding="utf-8", newline="") as f:
            lines = f.read().split("\n")
        self.assertEqual(lines[0], "{")
        self.assertEqual(lines[-2:], ["}", ""])
        self.assertNotIn("\r", "".join(lines))
        entries = lines[1:-2]
        self.assertEqual(len(entries), episode_count(pod))
        keys = [json.loads("{" + line.rstrip(",") + "}").popitem()[0] for line in entries]
        self.assertEqual(keys, sorted(keys))

    def test_nothing_is_written_when_nothing_changed(self):
        pod = min(PODS, key=episode_count)
        self.run_feed(pod)
        before = os.stat(self.cache_path(pod)).st_mtime_ns

        self.run_feed(pod)

        self.assertEqual(os.stat(self.cache_path(pod)).st_mtime_ns, before)


if __name__ == "__main__":
    unittest.main()
