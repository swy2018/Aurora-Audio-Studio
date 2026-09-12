import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

bridge_path = Path(__file__).resolve().parents[2] / "AuroraAudioStudio/Tools/gradio_result_bridge.py"
spec = importlib.util.spec_from_file_location("result_bridge", bridge_path)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class ResultBridgeTests(unittest.TestCase):
    def test_only_final_nonstreaming_result_components(self):
        self.assertTrue(bridge.is_result_audio("Synthesized Audio"))
        self.assertTrue(bridge.is_result_audio("完整输出"))
        self.assertFalse(bridge.is_result_audio("Reference Audio"))
        self.assertFalse(bridge.is_result_audio("Generated Audio", streaming=True))

    def test_final_audio_receipt_metadata_deduplication_and_upstream_passthrough(self):
        root = Path(tempfile.mkdtemp(prefix="aurora-final-result-"))
        source = root / "post-silence-trim-and-concatenation.wav"
        source.write_bytes(b"final-fixture")
        calls = []

        class Audio:
            label = "Synthesized Audio"
            streaming = False

            def postprocess(self, value):
                return dict(path=str(source), value=value)

        def write(path, audio, rate, subtype):
            calls.append((audio, rate, subtype))
            Path(path).write_bytes(b"pcm16-fixture")

        modules = dict(gradio=types.SimpleNamespace(Audio=Audio),
                       soundfile=types.SimpleNamespace(read=lambda *a, **k: ([0.1, 0.2, 0.3], 24000), write=write),
                       torch=types.SimpleNamespace(cuda=types.SimpleNamespace(is_available=lambda: False)))
        environment = dict(AURORA_OUTPUT_ROOT=str(root / "AI配音"), AURORA_RESULT_RECEIPTS=str(root / "receipts"),
                           AURORA_FEATURE="voice", AURORA_MODEL_ID="f5-tts", AURORA_DEVICE="mps",
                           AURORA_MODEL_VERSION="fixture-version", AURORA_RUNTIME_SIGNATURE="fixture-signature")
        with patch.dict(sys.modules, modules), patch.dict(os.environ, environment):
            bridge.install_bridge()
            audio = Audio()
            self.assertEqual(audio.postprocess("final-output")["value"], "final-output")
            audio.postprocess("same-output")
        receipts = list((root / "receipts").glob("*.json"))
        self.assertEqual(len(receipts), 1)
        receipt = json.loads(receipts[0].read_text(encoding="utf-8"))
        self.assertEqual(receipt["device"], "mps")
        self.assertEqual(receipt["modelVersion"], "fixture-version")
        self.assertEqual(receipt["runtimeSignature"], "fixture-signature")
        self.assertEqual(calls, [([0.1, 0.2, 0.3], 24000, "PCM_16")])
        self.assertTrue(Path(receipt["path"]).is_file())
        print("Test files retained:", root)


if __name__ == "__main__":
    unittest.main()
