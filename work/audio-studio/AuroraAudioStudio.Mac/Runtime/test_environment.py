import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import install_environment as e


class EnvironmentTests(unittest.TestCase):
    def setUp(self):
        self.uv = patch.object(e.shutil, "which", return_value="/Aurora.app/Runtime/bin/uv")
        self.uv.start()
        self.addCleanup(self.uv.stop)
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

    def test_missing_bundle_never_launches_homebrew_or_system_installer(self):
        with patch.object(e.shutil, "which", return_value=None), patch.object(e.subprocess, "run") as run:
            with self.assertRaisesRegex(RuntimeError, "内置 uv"):
                e.install(self.root, "qwen")
            run.assert_not_called()

    def test_installer_uses_app_provided_uv(self):
        with patch.object(e.subprocess, "run", side_effect=KeyboardInterrupt) as run:
            with self.assertRaises(KeyboardInterrupt): e.install(self.root, "qwen")
        self.assertEqual(run.call_args.args[0][0], "/Aurora.app/Runtime/bin/uv")

    def test_model_native_dependency_constraints(self):
        def create_environment(args, **kwargs):
            if args[1] == "venv":
                target = Path(args[-1]); (target / "bin").mkdir(parents=True)
                (target / "bin/python").write_text("fixture")
        required = {"f5": ["datasets>=3", "torchcodec==0.10.0"],
                    "transkun": ["ncls==0.0.70", "--only-binary=ncls", "numpy<2"],
                    "mt3": ["transformers==4.45.2", "numpy<2", "setuptools<81"],
                    "demucs": ["numpy<2"]}
        for family, constraints in required.items():
            with self.subTest(family=family), patch.object(e.subprocess, "run", side_effect=create_environment) as run, patch.object(e.subprocess, "check_output", return_value="package==1\n"):
                e.install(self.root, family)
            command = next(call.args[0] for call in run.call_args_list if call.args[0][1:3] == ["pip", "install"])
            for constraint in constraints:
                self.assertIn(constraint, command)
            if family == "mt3":
                self.assertTrue(any(call.args[0][1:] == ["-c", "import mt3_infer.models.yourmt3.inference_loader"] for call in run.call_args_list))
            if family == "demucs":
                self.assertTrue(any(call.args[0][1:] == ["-c", "import demucs.api"] for call in run.call_args_list))


if __name__ == "__main__": unittest.main()
