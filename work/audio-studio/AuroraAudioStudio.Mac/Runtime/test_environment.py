import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import install_environment as e


class EnvironmentTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="aurora-environment-test-"))
        self.old = self.root / "envs/qwen"
        (self.old / "bin").mkdir(parents=True)
        (self.old / "bin/python").write_text("fixture")
        (self.old / "aurora-runtime.json").write_text('{"family":"qwen","at":1}')
        (self.old / "package-state.txt").write_text("working")

    def test_failed_repair_preserves_active_environment(self):
        def run(args, **kwargs):
            if args[1] == "venv":
                target = Path(args[-1]); (target / "bin").mkdir(parents=True)
                (target / "bin/python").write_text("fixture")
            elif "install" in args:
                target = Path(args[args.index("--python") + 1]).parent.parent
                (target / "package-state.txt").write_text("broken")
                raise subprocess.CalledProcessError(1, args)
        with patch.object(e.subprocess, "run", side_effect=run):
            with self.assertRaises(subprocess.CalledProcessError): e.install(self.root, "qwen", repair=True)
        self.assertEqual((self.old / "package-state.txt").read_text(), "working")
        self.assertEqual(json.loads((self.old / "aurora-runtime.json").read_text())["at"], 1)
        self.assertFalse((self.root / "envs/qwen.active").exists())

    def test_successful_repair_activates_checked_candidate_without_moving_old(self):
        def run(args, **kwargs):
            if args[1] == "venv":
                target = Path(args[-1]); (target / "bin").mkdir(parents=True)
                (target / "bin/python").write_text("new interpreter")
        with patch.object(e.subprocess, "run", side_effect=run) as process, patch.object(e.subprocess, "check_output", return_value="package==1\n"):
            e.install(self.root, "qwen", repair=True)
        active = e.environment_path(self.root, "qwen")
        self.assertNotEqual(active, self.old)
        self.assertTrue((active / "aurora-runtime.json").is_file())
        self.assertEqual((self.old / "package-state.txt").read_text(), "working")
        self.assertTrue(any(call.args[0][1:] == ["-c", "import qwen_tts"] for call in process.call_args_list))

    def test_legacy_environment_and_unsafe_selector(self):
        self.assertEqual(e.environment_path(self.root, "qwen"), self.old)
        (self.root / "envs/qwen.active").write_text("../../user-data")
        with self.assertRaises(ValueError): e.environment_path(self.root, "qwen")

    def test_cancel_during_install_keeps_previous_selection(self):
        with patch.object(e.subprocess, "run", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt): e.install(self.root, "qwen", repair=True)
        self.assertEqual(e.environment_path(self.root, "qwen"), self.old)
        self.assertEqual((self.old / "package-state.txt").read_text(), "working")


if __name__ == "__main__": unittest.main()
