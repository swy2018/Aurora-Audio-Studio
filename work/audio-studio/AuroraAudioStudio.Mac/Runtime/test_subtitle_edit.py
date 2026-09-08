import hashlib
import io
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

import subtitle_edit as editor


class Response(io.BytesIO):
    status = 200
    headers = {}


class SubtitleEditorTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="aurora-subtitle-editor-"))

    def tearDown(self):
        print("Test files retained:", self.root)

    def asset(self, data):
        return dict(name=editor.ASSET, browser_download_url="https://github.com/SubtitleEdit/subtitleedit/releases/download/v5.1.0/" + editor.ASSET,
                    digest="sha256:" + hashlib.sha256(data).hexdigest(), size=len(data))

    def test_release_requires_matching_arm64_digest_and_official_url(self):
        asset = self.asset(b"fixture")
        payload = dict(tag_name="v5.1.0", assets=[asset])
        with patch.object(editor.urllib.request, "urlopen", return_value=Response(json.dumps(payload).encode())):
            self.assertEqual(editor.release()[0], "v5.1.0")
        asset["browser_download_url"] = "https://example.com/other.dmg"
        with patch.object(editor.urllib.request, "urlopen", return_value=Response(json.dumps(payload).encode())), self.assertRaises(ValueError):
            editor.release()

    def test_incomplete_download_is_retained_for_resume(self):
        asset = self.asset(b"12345678")
        with patch.object(editor.urllib.request, "urlopen", return_value=Response(b"1234")), self.assertRaises(IOError):
            editor.fetch(self.root, asset)
        directory = self.root / "downloads" / asset["digest"].split(":")[1]
        self.assertEqual((directory / (editor.ASSET + ".partial")).read_bytes(), b"1234")
        response = Response(b"5678"); response.status = 206; response.headers = {"Content-Range": "bytes 4-7/8"}
        with patch.object(editor.urllib.request, "urlopen", return_value=response) as request:
            result = editor.fetch(self.root, asset)
        self.assertEqual(request.call_args.args[0].get_header("Range"), "bytes=4-")
        self.assertEqual(result.read_bytes(), b"12345678")

    def test_bad_digest_is_not_promoted(self):
        asset = self.asset(b"good")
        with patch.object(editor.urllib.request, "urlopen", return_value=Response(b"evil")), self.assertRaises(ValueError):
            editor.fetch(self.root, asset)
        self.assertFalse(list(self.root.rglob(editor.ASSET)))

    def test_gatekeeper_failure_is_explicit_and_stops_verification(self):
        responses = [types.SimpleNamespace(returncode=0, stdout="", stderr=""), types.SimpleNamespace(returncode=3, stdout="", stderr="Unnotarized Developer ID")]
        with patch.object(editor.subprocess, "run", side_effect=responses), self.assertRaisesRegex(RuntimeError, "不会绕过安全检查"):
            editor.verify(self.root / editor.APP)

    def test_running_editor_prevents_mutation(self):
        process = str(self.root / "current" / editor.APP / "Contents/MacOS/SubtitleEdit")
        with patch.object(editor.subprocess, "run", return_value=types.SimpleNamespace(stdout=process + "\n")), self.assertRaisesRegex(RuntimeError, "退出 Subtitle Edit"):
            editor.require_closed(self.root)

    def test_rejected_staged_app_never_replaces_current(self):
        current = self.root / "current"; current.mkdir(); (current / "keep.txt").write_text("original")
        dmg = self.root / "fixture.dmg"; dmg.write_bytes(b"fixture")
        def run(arguments, **kwargs):
            if "attach" in arguments:
                mount = Path(arguments[arguments.index("-mountpoint") + 1]); (mount / editor.APP).mkdir()
            return types.SimpleNamespace(returncode=0)
        with patch.object(editor, "release", return_value=("v5.1.0", self.asset(b"fixture"))), patch.object(editor, "fetch", return_value=dmg), \
             patch.object(editor.subprocess, "run", side_effect=run), patch.object(editor, "verify", side_effect=RuntimeError("not accepted")), self.assertRaises(RuntimeError):
            editor.install(self.root)
        self.assertEqual((current / "keep.txt").read_text(), "original")
        self.assertFalse((self.root / "previous").exists())


if __name__ == "__main__":
    unittest.main()
