import asyncio
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

source = Path(__file__).resolve().parents[2] / "AuroraAudioStudio/Tools/gradio_task_bridge.py"
spec = importlib.util.spec_from_file_location("task_bridge_test", source)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class BridgeTests(unittest.IsolatedAsyncioTestCase):
    def environment(self, root):
        return patch.dict(os.environ, dict(AURORA_TASK_EVENTS=root, AURORA_WORKBENCH_INSTANCE="instance",
            AURORA_FEATURE="voice", AURORA_MODEL_ID="qwen3-tts-base"))

    async def test_return_and_generator_identity_preserved(self):
        root = tempfile.mkdtemp(prefix="Aurora-TaskBridge-")
        result = {"data": ["unchanged"], "is_generating": True}
        class Blocks:
            fns = {0: types.SimpleNamespace(api_name="run_voice_clone")}
            async def process_api(self, block_fn, inputs, event_id=None):
                bridge.active_task.get()["has_output"] = True
                return result
        with self.environment(root):
            bridge.install(types.SimpleNamespace(Blocks=Blocks))
            blocks = Blocks()
            self.assertIs(await blocks.process_api(0, ["input"], event_id="one"), result)
            first = json.loads(next(Path(root).glob("*.json")).read_text(encoding="utf-8"))
            self.assertEqual(first["status"], "running")
            result["is_generating"] = False
            self.assertIs(await blocks.process_api(0, ["input"], event_id="one"), result)
            final = json.loads(next(Path(root).glob("*.json")).read_text(encoding="utf-8"))
            self.assertEqual(final["id"], first["id"])
            self.assertEqual(final["status"], "saving")
            self.assertEqual(len(list(Path(root).glob("*.json"))), 1)
            self.assertIsNone(bridge.active_task.get())

    async def test_exception_identity_preserved(self):
        root = tempfile.mkdtemp(prefix="Aurora-TaskBridge-")
        error = ValueError("original")
        class Blocks:
            fns = {0: types.SimpleNamespace(api_name="predict")}
            async def process_api(self, block_fn, inputs, event_id=None):
                raise error
        with self.environment(root):
            bridge.install(types.SimpleNamespace(Blocks=Blocks))
            with self.assertRaises(ValueError) as raised:
                await Blocks().process_api(0, [], event_id="one")
            self.assertIs(raised.exception, error)
            self.assertEqual(json.loads(next(Path(root).glob("*.json")).read_text())["status"], "failed")

    async def test_unobserved_actions_are_unchanged(self):
        root = tempfile.mkdtemp(prefix="Aurora-TaskBridge-")
        class Blocks:
            fns = {0: types.SimpleNamespace(api_name="clear")}
            async def process_api(self, block_fn, inputs, event_id=None):
                return inputs
        with self.environment(root):
            bridge.install(types.SimpleNamespace(Blocks=Blocks))
            inputs = [object()]
            self.assertIs(await Blocks().process_api(0, inputs, event_id="clear"), inputs)
            self.assertEqual(list(Path(root).iterdir()), [])


if __name__ == "__main__":
    unittest.main()
