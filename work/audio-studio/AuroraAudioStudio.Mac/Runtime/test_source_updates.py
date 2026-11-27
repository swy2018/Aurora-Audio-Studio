from pathlib import Path
import tempfile
import json
import unittest
from unittest.mock import patch
import urllib.error
import source_updates as source


class SourceUpdateTests(unittest.TestCase):
    def commit(self, letter):
        return dict(sha=letter * 40, commit=dict(committer=dict(date="2026-11-27T00:00:00Z")))

    def test_formal_release_is_selected_instead_of_unreleased_head(self):
        def metadata(url):
            if url.endswith("/releases/latest"):
                return dict(tag_name="v0.1.5", draft=False, prerelease=False)
            if url.endswith("/commits/v0.1.5"):
                return self.commit("a")
            return [self.commit("b")]
        root = Path(tempfile.mkdtemp(prefix="aurora-source-test-"))
        info = root / "lib/python3.11/site-packages/bs_roformer_infer-0.1.5.dist-info"
        info.mkdir(parents=True)
        (info / "METADATA").write_text("Version: 0.1.5\n")
        with patch.object(source, "metadata", side_effect=metadata):
            result = source.check(root, "roformer")
        self.assertFalse(result["available"])
        self.assertEqual(result["unreleased"], "b" * 8)
        self.assertEqual(result["target"]["commit"], "a" * 40)

    def test_no_formal_release_uses_dated_commit(self):
        with patch.object(source, "metadata", side_effect=[urllib.error.HTTPError("url", 404, "not found", {}, None), [self.commit("c")]]):
            result = source.release("mt3")
        self.assertEqual(result["tag"], "")
        self.assertEqual(result["version"], "2026.11.27+cccccccc")

    def test_manual_newer_and_divergent_source_never_downgrade(self):
        root = Path(tempfile.mkdtemp(prefix="aurora-source-test-"))
        info = root / "lib/python3.11/site-packages/bs_roformer_infer-0.1.5.dist-info"
        info.mkdir(parents=True)
        (info / "METADATA").write_text("Version: 0.1.5\n")
        (info / "direct_url.json").write_text(json.dumps(dict(url="https://github.com/openmirlab/bs-roformer-infer.git", vcs_info=dict(vcs="git", commit_id="b"*40))))
        for status in ("behind", "ahead", "diverged"):
            replies = [dict(tag_name="v0.1.5"), self.commit("a"), dict(status=status), [self.commit("b")]]
            with patch.object(source, "metadata", side_effect=replies):
                if status == "diverged":
                    with self.assertRaises(ValueError): source.check(root, "roformer")
                else:
                    result = source.check(root, "roformer")
                    self.assertEqual(result["available"], status == "ahead")
                    self.assertEqual(result["newer_local"], status == "behind")

    def test_metadata_failure_is_never_reported_current(self):
        with patch.object(source, "metadata", side_effect=urllib.error.HTTPError("url", 403, "rate limit", {}, None)):
            with self.assertRaises(urllib.error.HTTPError): source.release("mt3")

    def test_missing_formal_tag_does_not_silently_install_head(self):
        with patch.object(source, "metadata", side_effect=[dict(tag_name="v0.2.0"), urllib.error.HTTPError("url", 404, "missing tag", {}, None)]):
            with self.assertRaises(urllib.error.HTTPError): source.release("mt3")

    def test_prerelease_and_invalid_revision_are_rejected(self):
        with patch.object(source, "metadata", return_value=dict(tag_name="v0.3-beta", prerelease=True)):
            with self.assertRaises(ValueError): source.release("mt3")
        bad = self.commit("z")
        with patch.object(source, "metadata", side_effect=[dict(tag_name="v0.2.0"), bad]):
            with self.assertRaises(ValueError): source.release("mt3")


if __name__ == "__main__": unittest.main()
