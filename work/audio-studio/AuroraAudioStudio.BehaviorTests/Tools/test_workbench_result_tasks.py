"""No models: verify result/task linkage with an isolated import path and cached output."""
import asyncio
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

source = Path(__file__).resolve().parents[2] / "AuroraAudioStudio/Tools/gradio_result_bridge.py"
spec = importlib.util.spec_from_file_location("result_tasks_test", source)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class ResultTaskTests(unittest.IsolatedAsyncioTestCase):
    async def test_embedded_import_and_cached_outputs_keep_each_job(self):
        root = Path(tempfile.mkdtemp(prefix="Aurora-ResultTasks-"))
        audio_path = root / "source.wav"
        audio_path.write_bytes(b"upstream-fixture")
        writes = []
        class Audio:
            label, streaming = "Generated Audio", False
            def postprocess(self, value):
                return dict(path=str(audio_path), value=value)
        class Blocks:
            fns = {0: types.SimpleNamespace(api_name="generate")}
            def get_config_file(self):
                return {}
            async def process_api(self, block_fn, inputs, event_id=None):
                return dict(data=[Audio().postprocess(inputs)], is_generating=False)
        def write(path, *args, **kwargs):
            Path(path).write_bytes(b"saved-fixture")
            writes.append(path)
        modules = dict(gradio=types.SimpleNamespace(Audio=Audio, Blocks=Blocks),
            soundfile=types.SimpleNamespace(read=lambda *a, **k: ([0.1], 24000), write=write),
            torch=types.SimpleNamespace(cuda=types.SimpleNamespace(is_available=lambda: False)))
        environment = dict(AURORA_OUTPUT_ROOT=str(root / "output"), AURORA_RESULT_RECEIPTS=str(root / "receipts"),
            AURORA_TASK_EVENTS=str(root / "events"), AURORA_WORKBENCH_INSTANCE="launch",
            AURORA_FEATURE="voice", AURORA_MODEL_ID="fixture")
        # Embedded Python can omit the bridge directory from sys.path entirely.
        isolated_path = [entry for entry in sys.path if Path(entry).resolve() != source.parent]
        with patch.dict(sys.modules, modules), patch.dict(os.environ, environment, clear=True), patch.object(sys, "path", isolated_path):
            bridge.install_bridge()
            blocks = Blocks()
            for event in ["first", "second"]:
                value = await blocks.process_api(0, event, event_id=event)
                self.assertEqual(value["data"][0]["value"], event)
            receipts = [json.loads(p.read_text(encoding="utf-8")) for p in (root / "receipts").glob("*.json")]
            self.assertEqual(len(receipts), 2)
            self.assertEqual(len({r["taskId"] for r in receipts}), 2)
            self.assertEqual(len(writes), 1)
            # Move our own fixture to emulate a user moving a saved output; retain it.
            saved = Path(receipts[0]["path"])
            saved.rename(saved.with_suffix(".retained"))
            await blocks.process_api(0, "third", event_id="third")
        events = [json.loads(p.read_text(encoding="utf-8")) for p in (root / "events").glob("*.json")]
        self.assertEqual(len(events), 3)
        self.assertTrue(all(e["status"] == "saving" for e in events))
        self.assertEqual(len(writes), 2)


if __name__ == "__main__":
    unittest.main()
