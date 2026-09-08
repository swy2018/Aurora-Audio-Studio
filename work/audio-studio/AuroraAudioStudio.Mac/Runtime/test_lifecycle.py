import hashlib
import io
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch
from filelock import FileLock, Timeout
import download_models as d
import manage


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="aurora-lifecycle-"))
        self.data = b"original weights"
        self.revision = "revision-one"
        self.corrupt_remote = False
        self.lfs = True
        d.CATALOG["unit-model"] = dict(family="unit", gb=0, assets=[dict(repo="unit/repo", target="weights")])
        def info(*args, **kwargs):
            s = SimpleNamespace(rfilename="model.bin", size=len(self.data), lfs=SimpleNamespace(sha256=hashlib.sha256(self.data).hexdigest()) if self.lfs else None)
            return SimpleNamespace(sha=self.revision, siblings=[s])
        def fetch(repo, filename, **kwargs):
            dest = Path(kwargs["local_dir"]) / filename
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(b"bad" if self.corrupt_remote else self.data)
            return str(dest)
        self.patches = [patch.object(d, "HfApi", return_value=SimpleNamespace(model_info=info)), patch.object(d,"hf_hub_download",side_effect=fetch), patch.object(d,"emit")]
        for p in self.patches: p.start()

    def tearDown(self):
        for p in reversed(self.patches): p.stop()
        d.CATALOG.pop("unit-model")
        print("Test files retained:", self.root)

    @property
    def base(self): return self.root / "models/unit-model"

    def test_install_receipt_and_reuse(self):
        d.download(self.root,"unit-model")
        receipt = json.loads((self.base/"current/receipt.json").read_text())
        self.assertEqual(receipt["files"][0]["sha256"], hashlib.sha256(self.data).hexdigest())
        d.download(self.root,"unit-model")
        self.assertFalse((self.base/"previous").exists())

    def test_download_only_lifecycle_does_not_install_or_import_engine(self):
        d.CATALOG["unit-model"].update(download_only=True, required=["weights/model.bin"])
        with patch.object(manage, "environment") as environment, patch.object(manage.subprocess, "run") as process:
            manage.action(self.root, "unit-model", "install")
            self.data = b"updated weights"
            self.revision = "revision-two"
            manage.action(self.root, "unit-model", "update")
            manage.action(self.root, "unit-model", "rollback")
            environment.assert_not_called()
            process.assert_not_called()
        self.assertEqual((self.base / "current/weights/model.bin").read_bytes(), b"original weights")
        health = json.loads((self.base / "current/health.json").read_text())
        self.assertFalse(health["imports"])
        self.assertFalse(health["inference"])

    def test_missing_required_file_never_replaces_current(self):
        d.download(self.root, "unit-model")
        d.CATALOG["unit-model"].update(download_only=True, required=["weights/missing.json"])
        self.revision = "revision-two"
        with self.assertRaisesRegex(RuntimeError, "必需文件"):
            d.download(self.root, "unit-model", update=True)
        self.assertEqual(json.loads((self.base / "current/receipt.json").read_text())["assets"][0]["revision"], "revision-one")
        self.assertFalse((self.base / "previous").exists())

    def test_download_only_index_requires_every_weight_shard(self):
        candidate = self.root / "candidate"
        candidate.mkdir()
        index = candidate / "model.safetensors.index.json"
        index.write_text(json.dumps({"weight_map": {"layer": "shard.safetensors"}}))
        spec = dict(download_only=True, required=[index.name])
        with self.assertRaisesRegex(RuntimeError, "必需文件"):
            d.validate_required(candidate, spec)
        (candidate / "shard.safetensors").write_bytes(b"weights")
        d.validate_required(candidate, spec)
        index.write_text(json.dumps({"weight_map": {"layer": "../escape.safetensors"}}))
        (self.root / "escape.safetensors").write_bytes(b"outside")
        with self.assertRaisesRegex(RuntimeError, "必需文件"):
            d.validate_required(candidate, spec)

    def test_failed_update_preserves_working_version(self):
        d.download(self.root,"unit-model")
        self.data = b"new model content"
        self.revision = "revision-two"
        self.corrupt_remote = True
        with self.assertRaisesRegex(RuntimeError,"SHA-256"):
            d.download(self.root,"unit-model",update=True)
        self.assertEqual((self.base/"current/weights/model.bin").read_bytes(),b"original weights")

    def test_repair_corrupt_file(self):
        d.download(self.root,"unit-model")
        (self.base/"current/weights/model.bin").write_bytes(b"corruption")
        d.download(self.root,"unit-model")
        self.assertEqual((self.base/"current/weights/model.bin").read_bytes(),self.data)

    def test_recover_interrupted_promotion(self):
        d.download(self.root,"unit-model")
        (self.base/"current").rename(self.base/"previous")
        d.download(self.root,"unit-model")
        self.assertTrue((self.base/"current/receipt.json").exists())

    def test_exclusive_download(self):
        self.base.mkdir(parents=True)
        with FileLock(str(self.base/".lock")):
            with self.assertRaises(Timeout): d.download(self.root,"unit-model")

    def test_same_size_config_update_replaces_previous_content(self):
        self.lfs = False
        d.download(self.root,"unit-model")
        self.data = b"modified weights"
        self.revision = "revision-two"
        d.download(self.root,"unit-model",update=True)
        self.assertEqual((self.base/"current/weights/model.bin").read_bytes(),self.data)

    def test_failed_health_check_replaces_old_success(self):
        d.download(self.root, "unit-model")
        (self.base/"current/health.json").write_text('{"files":true,"imports":true,"at":0}')
        with patch.object(manage, "_check", side_effect=RuntimeError("broken environment")):
            with self.assertRaises(RuntimeError): manage.check(self.root, "unit-model")
        self.assertTrue(json.loads((self.base/"current/health.json").read_text())["failed"])

    def test_repeated_updates_retain_older_versions_without_deletion(self):
        d.download(self.root, "unit-model")
        self.data = b"second version"; self.revision = "revision-two"
        d.download(self.root, "unit-model", update=True)
        self.data = b"third version"; self.revision = "revision-three"
        d.download(self.root, "unit-model", update=True)
        older = list((self.base/"history").glob("*/weights/model.bin"))
        self.assertEqual(len(older), 1)
        self.assertEqual(older[0].read_bytes(), b"original weights")
        self.assertEqual((self.base/"previous/weights/model.bin").read_bytes(), b"second version")

    def test_failed_post_update_check_keeps_retry_and_previous(self):
        d.download(self.root, "unit-model")
        self.data = b"new model content"; self.revision = "revision-two"
        with patch.object(manage, "environment"), patch.object(manage, "check", side_effect=RuntimeError("environment failed")):
            with self.assertRaises(RuntimeError): manage.action(self.root, "unit-model", "update")
        self.assertTrue(json.loads((self.base/"current/update-status.json").read_text())["available"])
        self.assertEqual((self.base/"previous/weights/model.bin").read_bytes(), b"original weights")

    def test_successful_update_clears_retry(self):
        d.download(self.root, "unit-model")
        with patch.object(manage, "environment"), patch.object(manage, "check"):
            manage.action(self.root, "unit-model", "update")
        self.assertFalse(json.loads((self.base/"current/update-status.json").read_text())["available"])

    def test_failed_rollback_restores_current(self):
        d.download(self.root, "unit-model")
        self.data = b"new model content"; self.revision = "revision-two"
        d.download(self.root, "unit-model", update=True)
        with patch.object(manage, "check", side_effect=RuntimeError("bad previous")):
            with self.assertRaises(RuntimeError): manage.action(self.root, "unit-model", "rollback")
        self.assertEqual((self.base/"current/weights/model.bin").read_bytes(), self.data)
        self.assertEqual((self.base/"previous/weights/model.bin").read_bytes(), b"original weights")

    def test_download_rejects_staging_symlink(self):
        outside = self.root/"user-data"; outside.mkdir()
        self.base.mkdir(parents=True)
        (self.base/"staging").symlink_to(outside,target_is_directory=True)
        with self.assertRaises(ValueError): d.download(self.root,"unit-model")

    def test_fixed_url_resume_and_checksum(self):
        spec = d.CATALOG["unit-model"]
        spec["assets"] = []
        spec["urls"] = [dict(url="https://example.test/weights", target="weights/model.bin", size=len(self.data), md5=hashlib.md5(self.data).hexdigest())]
        partial = self.base / "staging/weights/model.bin.partial"
        partial.parent.mkdir(parents=True)
        partial.write_bytes(self.data[:4])
        response = io.BytesIO(self.data[4:]); response.status = 206
        with patch.object(d.urllib.request,"urlopen",return_value=response) as fetch:
            receipt = d.download(self.root,"unit-model")
            self.assertEqual(fetch.call_args.args[0].get_header("Range"),"bytes=4-")
        self.assertEqual((self.base/"current/weights/model.bin").read_bytes(),self.data)
        self.assertEqual(receipt["files"][0]["sha256"],hashlib.sha256(self.data).hexdigest())

    def test_fixed_url_corruption_does_not_promote(self):
        spec = d.CATALOG["unit-model"]
        spec["assets"] = []
        spec["urls"] = [dict(url="https://example.test/weights", target="weights/model.bin", size=len(self.data), md5=hashlib.md5(self.data).hexdigest())]
        response = io.BytesIO(b"corrupt"); response.status = 200
        with patch.object(d.urllib.request,"urlopen",return_value=response):
            with self.assertRaisesRegex(RuntimeError,"MD5"): d.download(self.root,"unit-model")
        self.assertFalse((self.base/"current").exists())

    def test_uninstall_preserves_results(self):
        d.download(self.root,"unit-model")
        result = self.root/"output.wav"; result.write_bytes(b"user result")
        with patch.object(manage,"trash_model",side_effect=lambda base: base.rename(self.root/"trash")):
            manage.action(self.root,"unit-model","uninstall")
        self.assertFalse((self.base/"current").exists())
        self.assertEqual(result.read_bytes(),b"user result")

    def test_uninstall_rejects_symlink(self):
        outside = self.root/"user-data"; outside.mkdir()
        self.base.mkdir(parents=True)
        (self.base/"current").symlink_to(outside,target_is_directory=True)
        with self.assertRaises(ValueError): manage.action(self.root,"unit-model","uninstall")
        self.assertTrue(outside.exists())


if __name__ == "__main__": unittest.main()
